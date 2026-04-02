import os
import time
import pandas as pd
import yfinance as yf
from datetime import datetime
from db import get_db_connection

def _validate_date(s: str) -> None:
    datetime.strptime(s, "%Y-%m-%d")

def _get_universe_symbols(top_n: int) -> list[str]:
    """
    Pragmatic universe:
    - Uses a static list if provided via env var UNIVERSE_SYMBOLS (comma-separated)
    - Otherwise defaults to a reasonable large-cap set (you can expand later)
    """
    env = os.getenv("UNIVERSE_SYMBOLS", "").strip()
    if env:
        syms = [x.strip().upper() for x in env.split(",") if x.strip()]
        return syms[:top_n] if top_n > 0 else syms

    # Default universe (edit this list as you like)
    default = [
        "AAPL","MSFT","NVDA","AMZN","GOOGL","META","TSLA","BRK-B","JPM","V",
        "XOM","UNH","PG","MA","HD","COST","AVGO","LLY","KO","PEP",
        "MRK","ABBV","ORCL","WMT","ADBE","CRM","BAC","CVX","NFLX","AMD",
        "INTC","CSCO","TMO","MCD","NKE","LIN","ACN","WFC","DHR","VZ",
        "CMCSA","DIS","QCOM","TXN","PM","NEE","RTX","HON","IBM","CAT"
    ]
    return default[:top_n] if top_n > 0 else default

def _market_cap(symbol: str) -> int:
    """
    yfinance market cap is CURRENT market cap, not historical.
    Used only to pick a "top N" universe for prototyping.
    """
    try:
        info = yf.Ticker(symbol).fast_info  # fast_info is lighter than info
        # fast_info doesn't always include marketCap; fallback to .info if needed
        mc = getattr(info, "market_cap", None)
        if mc is None:
            mc = yf.Ticker(symbol).info.get("marketCap", None)
        return int(mc) if mc else 0
    except Exception:
        return 0

def load_top_n_tickers_range(conn, start_date: str, end_date: str, top_n: int):
    """
    1) Choose a universe (large-cap modern symbols)
    2) Rank by current market cap (approximation)
    3) Download daily OHLC for start_date..end_date
    4) Insert into loaded_ticker_list + ticker_prices
    """
    _validate_date(start_date)
    _validate_date(end_date)
    if end_date <= start_date:
        raise ValueError("end_date must be after start_date")

    cur = conn.cursor()

    # 1) Universe
    universe = _get_universe_symbols(max(top_n * 3, top_n))  # oversample, then rank
    if not universe:
        return []

    # 2) Rank by market cap (current)
    caps = []
    for sym in universe:
        caps.append((sym, _market_cap(sym)))
        time.sleep(0.05)  # be nice to Yahoo

    caps.sort(key=lambda x: x[1], reverse=True)
    top_symbols = [s for (s, mc) in caps[:top_n]]

    # Insert ticker metadata (best-effort)
    for sym in top_symbols:
        company_name = None
        desc = None
        try:
            t = yf.Ticker(sym)
            # .info can be heavy; use sparingly
            info = t.info
            company_name = info.get("shortName") or info.get("longName")
            desc = info.get("longBusinessSummary")
            time.sleep(0.05)
        except Exception:
            pass

        cur.execute(
            """
            INSERT OR IGNORE INTO loaded_ticker_list
              (ticker_id, company_name, description)
            VALUES (?, ?, ?)
            """,
            (sym, company_name, desc),
        )

    # 3) Download prices in batches (yfinance limits URL size / rate)
    all_rows = []
    batch_size = 25
    for i in range(0, len(top_symbols), batch_size):
        batch = top_symbols[i:i+batch_size]
        # yfinance end is exclusive-ish; add one day via pandas date_range? simplest: let it be and accept slight behavior.
        df = yf.download(
            tickers=" ".join(batch),
            start=start_date,
            end=end_date,
            interval="1d",
            group_by="ticker",
            auto_adjust=False,
            threads=True,
            progress=False,
        )

        if df is None or df.empty:
            continue

        # df shape differs between single vs multiple tickers
        if isinstance(df.columns, pd.MultiIndex):
            for sym in batch:
                if sym not in df.columns.get_level_values(0):
                    continue
                sub = df[sym].dropna()
                if sub.empty:
                    continue
                sub = sub.reset_index()  # Date becomes column
                sub["ticker_id"] = sym
                all_rows.append(sub)
        else:
            # single ticker case
            sub = df.dropna().reset_index()
            sub["ticker_id"] = batch[0]
            all_rows.append(sub)

        time.sleep(0.25)

    if not all_rows:
        return top_symbols

    dfp = pd.concat(all_rows, ignore_index=True)

    # Normalize columns
    # yfinance uses 'Date' + 'Open High Low Close Adj Close Volume'
    dfp["date"] = pd.to_datetime(dfp["Date"]).dt.date.astype(str)

    # 4) Insert into DB
    for _, row in dfp.iterrows():
        cur.execute(
            """
            INSERT OR IGNORE INTO ticker_prices
              (ticker_id, open_price, high_price, low_price, close_price, date)
            VALUES (?, ?, ?, ?, ?, ?)
            """,
            (
                str(row["ticker_id"]).upper(),
                float(row["Open"]),
                float(row["High"]),
                float(row["Low"]),
                float(row["Close"]),
                row["date"],
            ),
        )

    return top_symbols
