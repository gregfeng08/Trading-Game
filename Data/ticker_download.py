"""
Standalone data ingestion script.
Downloads historical OHLC from Yahoo Finance and populates the SQLite DB
used by the .NET Trading Game server.

Usage:
    python ticker_download.py --start 2010-01-01 --end 2025-12-31 --top_n 250
    python ticker_download.py --start 2010-01-01 --end 2025-12-31  # defaults to 250
    python ticker_download.py --skip-metadata                      # skip slow metadata fetch

Requirements:
    pip install yfinance pandas
"""

import os
import time
import argparse
import sqlite3
from datetime import datetime, timedelta

import pandas as pd
import yfinance as yf


# Curated universe — already roughly sorted by market cap (largest first).
# No need for expensive per-symbol market cap API calls.
DEFAULT_UNIVERSE = [
    "AAPL","MSFT","NVDA","AMZN","GOOGL","META","TSLA","BRK-B","JPM","V",
    "XOM","UNH","PG","MA","HD","COST","AVGO","LLY","KO","PEP",
    "MRK","ABBV","ORCL","WMT","ADBE","CRM","BAC","CVX","NFLX","AMD",
    "INTC","CSCO","TMO","MCD","NKE","LIN","ACN","WFC","DHR","VZ",
    "CMCSA","DIS","QCOM","TXN","PM","NEE","RTX","HON","IBM","CAT",
    "SPGI","GE","AMGN","INTU","ISRG","BKNG","AXP","SYK","PLD","BLK",
    "MDLZ","GILD","ADI","LRCX","MMC","CB","SCHW","VRTX","REGN","ETN",
    "MO","ZTS","PANW","KLAC","CI","SO","CME","BSX","DUK","HUM",
    "CL","ITW","SHW","PGR","ICE","SNPS","CDNS","EQIX","MCK","GD",
    "MSI","PYPL","USB","PNC","AON","NOC","APD","TGT","EMR","ORLY",
    "AJG","TFC","ROP","CTAS","ECL","ADSK","NSC","SLB","WM","FDX",
    "COF","GM","JCI","TT","SPG","AFL","AEP","CARR","PSA","HLT",
    "OXY","KMB","MPC","F","SRE","AIG","D","FTNT","ALL","CCI",
    "WELL","GIS","PSX","MCHP","DHI","LHX","KMI","AMP","TEL","FAST",
    "KR","BK","CTSH","CMI","PAYX","EA","MSCI","A","OTIS","IQV",
    "EW","PRU","MNST","YUM","GLW","VRSK","HPQ","RSG","AME","KEYS",
    "IDXX","PEG","EXC","XEL","IT","DD","STZ","ED","WEC","FANG",
    "GEHC","EL","MTD","FIS","RMD","DOV","WTW","CBRE","WAB","ANSS",
    "HIG","GPN","AWK","ODFL","CHD","WY","EFX","BR","DTE","TSCO",
    "BAX","IFF","PPG","CDW","AVB","LUV","ROK","TRGP","CPRT","WBA",
    "HAL","FTV","EQR","VICI","AEE","ES","LYB","LH","STT","STE",
    "INVH","VMC","MLM","IR","K","GPC","SBAC","NTRS","RJF","PKI",
    "MAA","TER","BALL","TRMB","TYL","COO","WST","DGX","HOLX","ALGN",
    "SWK","MKC","CINF","J","POOL","FICO","SNA","BRO","IEX","JBHT",
    "LDOS","TXT","PFG","DPZ","CLX","ATO","CNP","NI","NDSN","PODD",
]


def get_symbols(top_n: int) -> list[str]:
    """Use env override or the curated list directly — no API calls needed."""
    env = os.getenv("UNIVERSE_SYMBOLS", "").strip()
    if env:
        syms = [x.strip().upper() for x in env.split(",") if x.strip()]
        return syms[:top_n] if top_n > 0 else syms
    return DEFAULT_UNIVERSE[:top_n] if top_n > 0 else DEFAULT_UNIVERSE


def load_tickers(db_path: str, start_date: str, end_date: str, top_n: int, skip_metadata: bool = False):
    datetime.strptime(start_date, "%Y-%m-%d")
    datetime.strptime(end_date, "%Y-%m-%d")
    if end_date <= start_date:
        raise ValueError("end_date must be after start_date")

    conn = sqlite3.connect(db_path)
    conn.execute("PRAGMA journal_mode=WAL;")
    conn.execute("PRAGMA foreign_keys=ON;")
    cur = conn.cursor()

    # Init schema
    schema_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "schema.sql")
    with open(schema_path, "r", encoding="utf-8") as f:
        conn.executescript(f.read())

    # Take top_n directly from the curated list — no market cap ranking needed
    symbols = get_symbols(top_n)
    print(f"Using {len(symbols)} symbols: {symbols[:10]}...")

    # Insert ticker metadata (optional — this is the slow part)
    if not skip_metadata:
        print("Fetching ticker metadata (use --skip-metadata to skip)...")
        # Batch metadata: fetch in groups to reduce API calls
        for i, sym in enumerate(symbols):
            company_name = None
            desc = None
            try:
                info = yf.Ticker(sym).info
                company_name = info.get("shortName") or info.get("longName")
                desc = info.get("longBusinessSummary")
            except Exception:
                pass

            cur.execute(
                "INSERT OR IGNORE INTO loaded_ticker_list (ticker_id, company_name, description) VALUES (?, ?, ?)",
                (sym, company_name, desc),
            )

            if (i + 1) % 25 == 0:
                print(f"  metadata: {i + 1}/{len(symbols)}")
                conn.commit()
                time.sleep(0.1)
        conn.commit()
    else:
        print("Skipping metadata fetch. Inserting ticker IDs only...")
        for sym in symbols:
            cur.execute(
                "INSERT OR IGNORE INTO loaded_ticker_list (ticker_id, company_name, description) VALUES (?, NULL, NULL)",
                (sym,),
            )
        conn.commit()

    # Download prices in batches
    # yfinance end date is exclusive, so add 1 day
    end_dt = datetime.strptime(end_date, "%Y-%m-%d") + timedelta(days=1)
    yf_end = end_dt.strftime("%Y-%m-%d")

    all_rows = []
    batch_size = 25
    for i in range(0, len(symbols), batch_size):
        batch = symbols[i:i + batch_size]
        batch_num = i // batch_size + 1
        total_batches = (len(symbols) + batch_size - 1) // batch_size
        print(f"Downloading prices batch {batch_num}/{total_batches} ({len(batch)} tickers)...")

        try:
            df = yf.download(
                tickers=" ".join(batch),
                start=start_date,
                end=yf_end,
                interval="1d",
                group_by="ticker",
                auto_adjust=False,
                threads=True,
                progress=False,
            )
        except Exception as e:
            print(f"  WARNING: batch {batch_num} failed: {e}")
            continue

        if df is None or df.empty:
            continue

        if isinstance(df.columns, pd.MultiIndex):
            for sym in batch:
                if sym not in df.columns.get_level_values(0):
                    continue
                sub = df[sym].dropna()
                if sub.empty:
                    continue
                sub = sub.reset_index()
                sub["ticker_id"] = sym
                all_rows.append(sub)
        else:
            sub = df.dropna().reset_index()
            sub["ticker_id"] = batch[0]
            all_rows.append(sub)

        time.sleep(0.25)

    if not all_rows:
        print("No price data downloaded.")
        conn.close()
        return symbols

    dfp = pd.concat(all_rows, ignore_index=True)
    dfp["date"] = pd.to_datetime(dfp["Date"]).dt.date.astype(str)

    print(f"Inserting {len(dfp)} price rows...")
    inserted = 0
    for _, row in dfp.iterrows():
        cur.execute(
            "INSERT OR IGNORE INTO ticker_prices (ticker_id, open_price, high_price, low_price, close_price, date) VALUES (?, ?, ?, ?, ?, ?)",
            (
                str(row["ticker_id"]).upper(),
                float(row["Open"]),
                float(row["High"]),
                float(row["Low"]),
                float(row["Close"]),
                row["date"],
            ),
        )
        inserted += 1
        if inserted % 50000 == 0:
            conn.commit()
            print(f"  {inserted}/{len(dfp)} rows inserted...")

    conn.commit()
    conn.close()
    print(f"Done. Loaded {len(symbols)} tickers, {len(dfp)} price rows.")
    return symbols


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Download ticker data into the Trading Game DB")
    parser.add_argument("--start", default="2010-01-01", help="Start date (YYYY-MM-DD)")
    parser.add_argument("--end", default="2025-12-31", help="End date (YYYY-MM-DD)")
    parser.add_argument("--top_n", type=int, default=250, help="Number of tickers to load")
    parser.add_argument("--skip-metadata", action="store_true", help="Skip fetching company names/descriptions (much faster)")
    parser.add_argument("--db", default=None, help="Path to database.db (defaults to Data/database.db next to this script)")
    args = parser.parse_args()

    db_path = args.db or os.path.join(os.path.dirname(os.path.abspath(__file__)), "database.db")
    print(f"DB: {db_path}")
    print(f"Range: {args.start} to {args.end}, top {args.top_n}")

    load_tickers(db_path, args.start, args.end, args.top_n, skip_metadata=args.skip_metadata)
