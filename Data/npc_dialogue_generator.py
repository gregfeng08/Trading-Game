"""
NPC Dialogue Generator for Trading Game.

Generates LLM-powered dialogue for game NPCs based on:
  - Historical price data (always available from the DB)
  - NYT news articles (optional, if NYT_API_KEY is set)

NPC types and their personalities:
  analyst  — technical/fundamental analysis, measured and data-driven
  broker   — market sentiment, rumors, persuasive
  trader   — gut-feel, street-level, colorful language
  anchor   — neutral news summary, formal

Usage:
    # Generate dialogue for a single date
    python npc_dialogue_generator.py --date 2010-01-04

    # Generate for a date range (one entry per trading day)
    python npc_dialogue_generator.py --start 2010-01-04 --end 2010-01-31

    # Skip news fetching (use price data only)
    python npc_dialogue_generator.py --start 2010-01-04 --end 2010-01-31 --no-news

Requirements:
    pip install openai requests

Env vars:
    OPENAI_API_KEY  — required
    NYT_API_KEY     — optional (enables news-enriched dialogue)
"""

import os
import sys
import json
import time
import argparse
import sqlite3
import requests
from datetime import date, datetime, timedelta
from typing import Optional

from openai import OpenAI


# ---- Config ----

OPENAI_API_KEY = os.environ.get("OPENAI_API_KEY")
NYT_API_KEY = os.environ.get("NYT_API_KEY")
DEFAULT_MODEL = "gpt-4o-mini"

NPC_TYPES = {
    "analyst": {
        "personality": "You are a Wall Street equity analyst. You are measured, data-driven, and reference specific numbers (prices, percent changes, P/E ratios). You speak in a professional but accessible way.",
        "categories": ["market_overview", "ticker_specific"],
    },
    "broker": {
        "personality": "You are a veteran stockbroker. You're persuasive, talk about market sentiment and 'what the smart money is doing'. You use colorful Wall Street jargon and are always trying to get clients excited about opportunities.",
        "categories": ["market_overview", "ticker_specific"],
    },
    "trader": {
        "personality": "You are a floor trader. You speak in short, punchy sentences with street-level slang. You trust your gut and talk about momentum, volume, and what 'feels right'. Occasionally sarcastic.",
        "categories": ["market_overview"],
    },
    "anchor": {
        "personality": "You are a financial news anchor. You're neutral, formal, and summarize events clearly. You always cite sources when possible and present facts without editorializing.",
        "categories": ["market_overview"],
    },
}


# ---- DB helpers ----

def get_db_connection(db_path: str) -> sqlite3.Connection:
    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA journal_mode=WAL;")
    return conn


def get_trading_days(conn: sqlite3.Connection, start: str, end: str) -> list[str]:
    """Get all unique trading days in the given range that have price data."""
    cur = conn.cursor()
    cur.execute(
        "SELECT DISTINCT date FROM ticker_prices WHERE date BETWEEN ? AND ? ORDER BY date",
        (start, end),
    )
    return [row["date"] for row in cur.fetchall()]


def get_market_summary(conn: sqlite3.Connection, target_date: str) -> dict:
    """
    Build a market summary for the given date:
    - Top gainers and losers
    - Overall market breadth
    - Notable price levels
    """
    cur = conn.cursor()

    # Get previous trading day
    cur.execute(
        "SELECT MAX(date) FROM ticker_prices WHERE date < ?",
        (target_date,),
    )
    row = cur.fetchone()
    prev_date = row[0] if row and row[0] else None

    if prev_date is None:
        return {"date": target_date, "note": "First trading day — no prior data for comparison."}

    # Get price changes
    cur.execute(
        """
        SELECT
            t.ticker_id,
            t.close_price AS today_close,
            p.close_price AS prev_close,
            CASE WHEN p.close_price > 0
                THEN ROUND((t.close_price - p.close_price) / p.close_price * 100, 2)
                ELSE 0
            END AS pct_change
        FROM ticker_prices t
        JOIN ticker_prices p ON t.ticker_id = p.ticker_id AND p.date = ?
        WHERE t.date = ?
        ORDER BY pct_change DESC
        """,
        (prev_date, target_date),
    )
    rows = cur.fetchall()

    if not rows:
        return {"date": target_date, "note": "No price data for this date."}

    gainers = [{"ticker": r["ticker_id"], "close": r["today_close"], "pct": r["pct_change"]} for r in rows[:5]]
    losers = [{"ticker": r["ticker_id"], "close": r["today_close"], "pct": r["pct_change"]} for r in rows[-5:]]

    up = sum(1 for r in rows if r["pct_change"] > 0)
    down = sum(1 for r in rows if r["pct_change"] < 0)
    flat = sum(1 for r in rows if r["pct_change"] == 0)
    avg_change = sum(r["pct_change"] for r in rows) / len(rows) if rows else 0

    return {
        "date": target_date,
        "prev_date": prev_date,
        "total_tickers": len(rows),
        "advancing": up,
        "declining": down,
        "flat": flat,
        "avg_pct_change": round(avg_change, 2),
        "top_gainers": gainers,
        "top_losers": losers,
    }


def get_ticker_detail(conn: sqlite3.Connection, ticker_id: str, target_date: str) -> Optional[dict]:
    """Get recent price history for a specific ticker around the target date."""
    cur = conn.cursor()
    cur.execute(
        """
        SELECT date, open_price, high_price, low_price, close_price
        FROM ticker_prices
        WHERE ticker_id = ? AND date <= ?
        ORDER BY date DESC
        LIMIT 10
        """,
        (ticker_id, target_date),
    )
    rows = cur.fetchall()
    if not rows:
        return None

    prices = [
        {"date": r["date"], "open": r["open_price"], "high": r["high_price"],
         "low": r["low_price"], "close": r["close_price"]}
        for r in rows
    ]

    # Simple trend
    if len(prices) >= 2:
        latest = prices[0]["close"]
        oldest = prices[-1]["close"]
        trend = "up" if latest > oldest else "down" if latest < oldest else "flat"
        trend_pct = round((latest - oldest) / oldest * 100, 2) if oldest > 0 else 0
    else:
        trend = "unknown"
        trend_pct = 0

    return {
        "ticker_id": ticker_id,
        "date": target_date,
        "latest_close": prices[0]["close"],
        "trend_10d": trend,
        "trend_10d_pct": trend_pct,
        "recent_prices": prices[:5],
    }


# ---- NYT News (optional) ----

def fetch_nyt_articles(target_date: str, max_articles: int = 5) -> list[dict]:
    """Fetch financial news from NYT around the target date. Returns [] if no API key."""
    if not NYT_API_KEY:
        return []

    d = datetime.strptime(target_date, "%Y-%m-%d").date()
    begin = (d - timedelta(days=3)).strftime("%Y%m%d")
    end_s = d.strftime("%Y%m%d")

    fq = (
        'source:("The New York Times") AND ('
        'section_name:("Business" "Business Day") OR '
        'news_desk:("Business" "Financial" "Finance" "Economy")'
        ')'
    )

    try:
        r = requests.get(
            "https://api.nytimes.com/svc/search/v2/articlesearch.json",
            params={
                "api-key": NYT_API_KEY,
                "begin_date": begin,
                "end_date": end_s,
                "q": "stocks markets economy",
                "fq": fq,
                "page": 0,
            },
            timeout=15,
        )
        r.raise_for_status()
        docs = r.json().get("response", {}).get("docs", [])
    except Exception as e:
        print(f"  NYT fetch warning: {e}")
        return []

    articles = []
    for doc in docs[:max_articles]:
        headline = doc.get("headline", {})
        title = headline.get("main", "") if isinstance(headline, dict) else str(headline)
        snippet = doc.get("snippet", "") or doc.get("abstract", "")
        articles.append({"title": title, "snippet": snippet})

    return articles


# ---- LLM Dialogue Generation ----

def generate_dialogue(
    npc_type: str,
    category: str,
    market_summary: dict,
    ticker_detail: Optional[dict],
    news_articles: list[dict],
    target_date: str,
    model: str = DEFAULT_MODEL,
) -> str:
    """Generate a single piece of NPC dialogue using OpenAI."""
    if not OPENAI_API_KEY:
        raise RuntimeError("OPENAI_API_KEY is required for dialogue generation.")

    npc_config = NPC_TYPES[npc_type]
    client = OpenAI(api_key=OPENAI_API_KEY)

    # Build context
    context_parts = [f"Date: {target_date}"]
    context_parts.append(f"Market summary: {json.dumps(market_summary, indent=None)}")

    if ticker_detail:
        context_parts.append(f"Ticker focus: {json.dumps(ticker_detail, indent=None)}")

    if news_articles:
        news_text = "\n".join(f"- {a['title']}: {a['snippet']}" for a in news_articles)
        context_parts.append(f"Recent news:\n{news_text}")

    context = "\n\n".join(context_parts)

    if category == "ticker_specific" and ticker_detail:
        task = f"Give your take on {ticker_detail['ticker_id']} specifically."
    else:
        task = "Give your take on the overall market today."

    prompt = f"""{task}

Market data and context:
{context}

Rules:
- Stay in character as described.
- Keep it to 2-4 sentences — this is game dialogue, not an essay.
- Reference specific numbers from the data when relevant (prices, percent changes).
- If news is provided, you may reference it, but don't fabricate facts.
- Do not break the fourth wall or mention that you are an AI/NPC.
- Speak directly to the player as if they just walked up to you."""

    resp = client.chat.completions.create(
        model=model,
        messages=[
            {"role": "system", "content": npc_config["personality"]},
            {"role": "user", "content": prompt},
        ],
        temperature=0.8,
        max_tokens=200,
    )
    return resp.choices[0].message.content.strip()


# ---- Main pipeline ----

def generate_for_date(
    conn: sqlite3.Connection,
    target_date: str,
    focus_tickers: list[str],
    use_news: bool = True,
    model: str = DEFAULT_MODEL,
):
    """Generate all NPC dialogue for a single trading day and store in DB."""
    cur = conn.cursor()

    # Check if we already generated for this date
    cur.execute("SELECT COUNT(*) FROM static_npc_dialogue WHERE date = ?", (target_date,))
    if cur.fetchone()[0] > 0:
        print(f"  {target_date}: dialogue already exists, skipping.")
        return

    market_summary = get_market_summary(conn, target_date)
    news_articles = fetch_nyt_articles(target_date) if use_news else []

    generated = 0
    for npc_type, config in NPC_TYPES.items():
        for category in config["categories"]:
            if category == "ticker_specific":
                # Generate for top movers + focus tickers
                tickers_to_cover = set(focus_tickers[:3])
                if "top_gainers" in market_summary:
                    tickers_to_cover.add(market_summary["top_gainers"][0]["ticker"])
                if "top_losers" in market_summary:
                    tickers_to_cover.add(market_summary["top_losers"][0]["ticker"])

                for ticker_id in tickers_to_cover:
                    ticker_detail = get_ticker_detail(conn, ticker_id, target_date)
                    if not ticker_detail:
                        continue

                    text = generate_dialogue(
                        npc_type, category, market_summary,
                        ticker_detail, news_articles, target_date, model,
                    )
                    cur.execute(
                        "INSERT INTO static_npc_dialogue (date, ticker_id, npc_type, category, text) VALUES (?, ?, ?, ?, ?)",
                        (target_date, ticker_id, npc_type, category, text),
                    )
                    generated += 1
            else:
                # market_overview — no specific ticker
                text = generate_dialogue(
                    npc_type, category, market_summary,
                    None, news_articles, target_date, model,
                )
                cur.execute(
                    "INSERT INTO static_npc_dialogue (date, ticker_id, npc_type, category, text) VALUES (?, ?, ?, ?, ?)",
                    (target_date, None, npc_type, category, text),
                )
                generated += 1

    conn.commit()
    print(f"  {target_date}: generated {generated} dialogue entries.")


def main():
    parser = argparse.ArgumentParser(description="Generate NPC dialogue for the Trading Game")
    parser.add_argument("--date", help="Single date (YYYY-MM-DD)")
    parser.add_argument("--start", help="Start date for range (YYYY-MM-DD)")
    parser.add_argument("--end", help="End date for range (YYYY-MM-DD)")
    parser.add_argument("--no-news", action="store_true", help="Skip NYT news fetching")
    parser.add_argument("--model", default=DEFAULT_MODEL, help=f"OpenAI model (default: {DEFAULT_MODEL})")
    parser.add_argument("--focus-tickers", default="AAPL,MSFT,NVDA,GOOGL,TSLA",
                        help="Comma-separated tickers to always generate ticker-specific dialogue for")
    parser.add_argument("--db", default=None, help="Path to database.db")
    args = parser.parse_args()

    if not OPENAI_API_KEY:
        print("ERROR: Set OPENAI_API_KEY environment variable.")
        sys.exit(1)

    db_path = args.db or os.path.join(os.path.dirname(os.path.abspath(__file__)), "database.db")
    focus_tickers = [t.strip().upper() for t in args.focus_tickers.split(",") if t.strip()]

    conn = get_db_connection(db_path)

    if args.date:
        dates = [args.date]
    elif args.start and args.end:
        dates = get_trading_days(conn, args.start, args.end)
        print(f"Found {len(dates)} trading days in range {args.start} to {args.end}")
    else:
        print("ERROR: Provide --date or both --start and --end")
        sys.exit(1)

    use_news = not args.no_news
    if use_news and not NYT_API_KEY:
        print("NOTE: NYT_API_KEY not set. Generating dialogue from price data only.")
        use_news = False

    for i, d in enumerate(dates):
        print(f"[{i+1}/{len(dates)}] Generating dialogue for {d}...")
        try:
            generate_for_date(conn, d, focus_tickers, use_news=use_news, model=args.model)
        except Exception as e:
            print(f"  ERROR on {d}: {e}")
            continue

        # Rate limiting — be nice to APIs
        if use_news:
            time.sleep(1.0)
        else:
            time.sleep(0.5)

    conn.close()
    print("Done.")


if __name__ == "__main__":
    main()
