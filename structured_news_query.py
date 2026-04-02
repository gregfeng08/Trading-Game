"""
Windowed NYT -> Rank (macro > crash > movers > single) -> Compose exactly 5 items
-> OpenAI generates: "HEADLINE": "DETAIL [LINK]" ...

Requirements:
  pip install requests openai

Env vars:
  export NYT_API_KEY="..."
  export OPENAI_API_KEY="..."

Notes:
- Article Search API returns 10 results/page. We pull multiple pages per query.
- We keep q as plain terms; use boolean logic in fq.
- We use a rolling window (not exact-day) and exponential recency weighting.

The feed composition is:
  2 macro/systemic (window 14d)
  1 crash/stress (window 7d)
  1 movers-aligned (window 3d)
  1 company/portfolio/watchlist (window 3d; prefers your tickers)
If a bucket is missing, we backfill from the next most relevant pool.
"""

from __future__ import annotations

import os
import re
import math
import time
import requests
from dataclasses import dataclass
from datetime import date, datetime, timedelta
from typing import Any, Dict, List, Optional, Tuple, Iterable

from openai import OpenAI


# -----------------------------
# Config
# -----------------------------

NYT_API_KEY = os.environ.get("NYT_API_KEY")
OPENAI_API_KEY = os.environ.get("OPENAI_API_KEY")

DEFAULT_MODEL = "gpt-4o-mini"


# -----------------------------
# Keyword sets
# -----------------------------

MACRO_PHRASES = [
    "federal reserve", "the fed", "fomc", "central bank", "fed chair",
    "interest rate", "rate hike", "rate cut", "inflation", "cpi", "pce",
    "unemployment", "jobs report", "nonfarm payroll", "payrolls", "gdp",
    "treasury", "bond market", "yield", "yield curve", "deficit", "debt ceiling",
    "banking system", "bank capital", "stress test", "systemic risk",
    "regulation", "sec", "doj", "antitrust", "sanctions", "tariffs",
    "imf", "world bank", "ecb", "bank of england", "boj",
    "recession", "credit conditions", "financial conditions",
]

CRASH_PHRASES = [
    "selloff", "sell-off", "crash", "panic", "rout", "plunge", "tumbles", "free fall",
    "liquidity", "margin call", "contagion", "default", "bankruptcy",
    "bailout", "rescue", "emergency", "bank run", "volatility", "vix",
    "credit crunch", "turmoil", "meltdown", "collapse",
]

MOVERS_PHRASES = [
    "oil", "crude", "brent", "wti",
    "gold", "silver", "copper",
    "yen", "dollar", "euro", "fx", "currency",
    "commodities", "futures",
    "s&p", "s&p 500", "dow", "nasdaq",
    "treasury yields", "bond yields",
]

BREADTH_PHRASES = [
    "economy", "markets", "global", "worldwide", "banking system", "credit",
    "recession", "inflation", "financial system", "systemic", "broad",
    "treasury", "central bank", "federal reserve", "ecb", "imf",
]

SEVERITY_STRONG = ["crash", "collapse", "meltdown", "emergency", "rescue", "bailout"]
SEVERITY_MED = ["selloff", "sell-off", "plunge", "rout", "turmoil", "panic", "bank run", "default"]
SEVERITY_MILD = ["volatility", "slides", "dips", "jitters", "uncertainty", "worries"]


def _norm(s: str) -> str:
    return re.sub(r"\s+", " ", (s or "").strip().lower())


def _contains_any(text: str, phrases: Iterable[str]) -> bool:
    t = _norm(text)
    return any(p in t for p in phrases)


def _count_any(text: str, phrases: Iterable[str]) -> int:
    t = _norm(text)
    return sum(1 for p in phrases if p in t)


def _parse_pub_date(d: Any) -> Optional[date]:
    if not d:
        return None
    if isinstance(d, date) and not isinstance(d, datetime):
        return d
    if isinstance(d, datetime):
        return d.date()
    if isinstance(d, str):
        s = d.strip().replace("Z", "+00:00")
        try:
            return datetime.fromisoformat(s).date()
        except ValueError:
            return None
    return None


@dataclass(frozen=True)
class ScoredDoc:
    score: float
    bucket: str
    title: str
    snippet: str
    url: str
    pub_date: Optional[date]
    section: Optional[str]
    why: List[str]
    raw: Dict[str, Any]


# -----------------------------
# NYT fetch
# -----------------------------

def nyt_article_search(
    begin: date,
    end: date,
    q: Optional[str],
    fq: Optional[str],
    pages: int = 6,
    sleep_s: float = 0.15,
) -> List[Dict[str, Any]]:
    """
    NYT Article Search API. 10 results/page. Pull multiple pages.
    Keep q as plain terms; use boolean logic in fq.
    """
    if not NYT_API_KEY:
        raise RuntimeError("Missing NYT_API_KEY env var.")

    url = "https://api.nytimes.com/svc/search/v2/articlesearch.json"
    begin_s = begin.strftime("%Y%m%d")
    end_s = end.strftime("%Y%m%d")

    docs: List[Dict[str, Any]] = []
    for page in range(pages):
        params = {
            "api-key": NYT_API_KEY,
            "begin_date": begin_s,
            "end_date": end_s,
            "page": page,
        }
        if q:
            params["q"] = q
        if fq:
            params["fq"] = fq

        r = requests.get(url, params=params, timeout=30)
        r.raise_for_status()
        batch = r.json().get("response", {}).get("docs", []) or []
        docs.extend(batch)

        # stop early if fewer than 10 returned
        if len(batch) < 10:
            break
        time.sleep(sleep_s)

    return docs


def _dedupe_docs(docs: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    seen = set()
    out = []
    for d in docs:
        key = d.get("_id") or d.get("web_url") or str(d.get("headline", "")) + str(d.get("pub_date", ""))
        if key in seen:
            continue
        seen.add(key)
        out.append(d)
    return out


# -----------------------------
# Scoring
# -----------------------------

def _bucket_for_text(fulltext: str) -> Tuple[str, float, str]:
    if _contains_any(fulltext, MACRO_PHRASES):
        return "macro", 100.0, "bucket=macro"
    if _contains_any(fulltext, CRASH_PHRASES):
        return "crash", 70.0, "bucket=crash"
    if _contains_any(fulltext, MOVERS_PHRASES):
        return "movers", 40.0, "bucket=movers"
    return "single", 10.0, "bucket=single"


def _exp_recency_boost(pub: Optional[date], target: date, max_boost: float, half_life_days: float) -> float:
    """
    Exponential decay:
      boost = max_boost * exp(-delta / half_life)
    If pub missing -> 0.
    """
    if not pub:
        return 0.0
    delta = abs((target - pub).days)
    return float(max_boost * math.exp(-delta / max(1e-6, half_life_days)))


def score_doc(
    doc: Dict[str, Any],
    target_day: date,
    mover_terms: List[str],
    # recency tuning per bucket (macro fades slower than crash)
    macro_half_life: float = 7.0,
    crash_half_life: float = 3.0,
    movers_half_life: float = 2.5,
    single_half_life: float = 2.0,
) -> ScoredDoc:
    headline = doc.get("headline", "")
    if isinstance(headline, dict):
        title = headline.get("main", "") or headline.get("print_headline", "") or ""
    else:
        title = str(headline or doc.get("title", "") or "")

    snippet = doc.get("snippet", "") or doc.get("abstract", "") or ""
    url = doc.get("web_url", "") or doc.get("url", "") or ""
    pub = _parse_pub_date(doc.get("pub_date") or doc.get("published_date") or doc.get("date"))
    section = doc.get("section_name") or doc.get("news_desk") or doc.get("section") or None

    fulltext = f"{title} {snippet}"
    why: List[str] = []

    bucket, bucket_weight, bucket_why = _bucket_for_text(fulltext)
    why.append(bucket_why)

    # Breadth boost (0..15)
    breadth_hits = _count_any(fulltext, BREADTH_PHRASES)
    breadth_boost = min(15.0, breadth_hits * 3.0)
    if breadth_boost:
        why.append(f"breadth=+{breadth_boost:.0f} ({breadth_hits} hits)")

    # Severity boost (0..15)
    severity = 0.0
    if _contains_any(fulltext, SEVERITY_STRONG):
        severity = 15.0
        why.append("severity=+15 (strong)")
    elif _contains_any(fulltext, SEVERITY_MED):
        severity = 8.0
        why.append("severity=+8 (med)")
    elif _contains_any(fulltext, SEVERITY_MILD):
        severity = 3.0
        why.append("severity=+3 (mild)")

    # Recency boost (0..10), bucket-dependent half-life
    if bucket == "macro":
        recency = _exp_recency_boost(pub, target_day, max_boost=10.0, half_life_days=macro_half_life)
    elif bucket == "crash":
        recency = _exp_recency_boost(pub, target_day, max_boost=10.0, half_life_days=crash_half_life)
    elif bucket == "movers":
        recency = _exp_recency_boost(pub, target_day, max_boost=10.0, half_life_days=movers_half_life)
    else:
        recency = _exp_recency_boost(pub, target_day, max_boost=10.0, half_life_days=single_half_life)

    if recency:
        why.append(f"recency=+{recency:.1f} (exp decay)")

    # Movers alignment boost (0..10 capped) – tie-breaker only
    alignment = 0.0
    if mover_terms:
        hits = _count_any(fulltext, mover_terms)
        if hits > 0:
            alignment = min(10.0, 6.0 + 2.0 * (hits - 1))
            why.append(f"movers=+{alignment:.0f} ({hits} hits, cap 10)")

    # Score = BucketWeight + BreadthBoost + SeverityBoost + RecencyBoost + MoversAlignmentBoost (capped at 10)
    score = bucket_weight + breadth_boost + severity + recency + alignment

    return ScoredDoc(
        score=score,
        bucket=bucket,
        title=title.strip(),
        snippet=snippet.strip(),
        url=url,
        pub_date=pub,
        section=section,
        why=why,
        raw=doc,
    )


# -----------------------------
# Composition logic (exactly 5)
# -----------------------------

def _terms_from_movers(movers: Optional[List[Dict[str, Any]]]) -> List[str]:
    terms: List[str] = []
    if not movers:
        return terms
    for m in movers:
        sym = _norm(str(m.get("symbol", "")))
        name = _norm(str(m.get("name", "")))
        if len(sym) >= 2:
            terms.append(sym)
        if len(name) >= 3:
            terms.append(name)
    # unique
    return list(dict.fromkeys(terms))


def _terms_from_company_focus(company_focus: List[str]) -> List[str]:
    # Accept tickers or names; we just match literal substring
    return [_norm(x) for x in company_focus if x and len(x) >= 2]


def compose_daily_pack(
    scored: List[ScoredDoc],
    target_day: date,
    company_focus: List[str],
    desired_total: int = 5,
) -> List[ScoredDoc]:
    """
    Build exactly 5 items in this structure:
      2 macro
      1 crash
      1 movers
      1 company-focused (prefer those tickers/names)
    Backfill from highest score if any bucket is empty.
    """
    # Pre-split pools
    macro = [s for s in scored if s.bucket == "macro"]
    crash = [s for s in scored if s.bucket == "crash"]
    movers_pool = [s for s in scored if s.bucket == "movers"]
    single = [s for s in scored if s.bucket == "single"]

    # Company-focused means: mentions any company_focus term in title/snippet
    focus_terms = _terms_from_company_focus(company_focus)
    def is_focus(s: ScoredDoc) -> bool:
        txt = _norm(s.title + " " + s.snippet)
        return any(t in txt for t in focus_terms)

    focus = [s for s in scored if is_focus(s)]

    # Sort each pool
    for pool in (macro, crash, movers_pool, single, focus):
        pool.sort(key=lambda x: x.score, reverse=True)

    chosen: List[ScoredDoc] = []
    used_urls = set()

    def pick_from(pool: List[ScoredDoc], k: int):
        nonlocal chosen
        for s in pool:
            if len(chosen) >= desired_total or k <= 0:
                break
            if not s.url or s.url in used_urls:
                continue
            chosen.append(s)
            used_urls.add(s.url)
            k -= 1

    # Target structure
    pick_from(macro, 2)
    pick_from(crash, 1)
    pick_from(movers_pool, 1)
    pick_from(focus, 1)

    # Backfill to exactly 5 from overall best
    if len(chosen) < desired_total:
        scored_sorted = sorted(scored, key=lambda x: x.score, reverse=True)
        pick_from(scored_sorted, desired_total - len(chosen))

    # Hard cap
    return chosen[:desired_total]


# -----------------------------
# High-level pipeline
# -----------------------------

def fetch_and_rank_windowed(
    target_day: date,
    movers: Optional[List[Dict[str, Any]]] = None,
    company_focus: Optional[List[str]] = None,
) -> Tuple[List[ScoredDoc], List[ScoredDoc]]:
    """
    Fetch candidates using multiple windows:
      macro/systemic: 14d
      crash/stress: 7d
      movers/company: 3d
    Union + dedupe -> score -> return (all_scored_sorted, daily_pack_of_5)
    """
    company_focus = company_focus or ["GOOG", "NVDA", "META", "TSLA"]

    mover_terms = _terms_from_movers(movers)

    # Finance-ish filter (include Business Day etc.)
    fq_financeish = (
        'source:("The New York Times") AND ('
        'section_name:("Business" "Business Day") OR '
        'news_desk:("Business" "Financial" "Finance" "Economy")'
        ')'
    )

    # Windows
    macro_begin = target_day - timedelta(days=14)
    crash_begin = target_day - timedelta(days=7)
    short_begin = target_day - timedelta(days=3)

    # Keep q plain terms; use fq for boolean constraints.
    # We do a few broad q’s to avoid missing items by wording.
    macro_qs = ["federal reserve economy markets", "rates inflation treasury", "regulation tariffs sanctions"]
    crash_qs = ["selloff crash volatility", "liquidity default bailout", "bank run contagion"]
    movers_qs = ["oil gold silver commodities", "dollar yen euro currency", "bond yields treasury"]
    company_qs = [" ".join(company_focus)]

    docs: List[Dict[str, Any]] = []

    for q in macro_qs:
        docs.extend(nyt_article_search(macro_begin, target_day, q=q, fq=fq_financeish, pages=6))
    for q in crash_qs:
        docs.extend(nyt_article_search(crash_begin, target_day, q=q, fq=fq_financeish, pages=6))
    for q in movers_qs:
        docs.extend(nyt_article_search(short_begin, target_day, q=q, fq=fq_financeish, pages=6))
    for q in company_qs:
        docs.extend(nyt_article_search(short_begin, target_day, q=q, fq=fq_financeish, pages=6))

    docs = _dedupe_docs(docs)

    # Score all
    scored = [score_doc(d, target_day=target_day, mover_terms=mover_terms) for d in docs]
    scored.sort(key=lambda s: s.score, reverse=True)

    # Compose exactly 5
    pack = compose_daily_pack(scored, target_day=target_day, company_focus=company_focus, desired_total=5)

    return scored, pack


def generate_briefing_with_openai(
    target_day: date,
    pack: List[ScoredDoc],
    company_focus: List[str],
    model: str = DEFAULT_MODEL,
) -> str:
    """
    Use OpenAI to generate 5 headline/detail items using ONLY the provided sources.
    """
    if not OPENAI_API_KEY:
        raise RuntimeError("Missing OPENAI_API_KEY env var.")

    client = OpenAI(api_key=OPENAI_API_KEY)

    evidence_lines = []
    for i, s in enumerate(pack, start=1):
        pd = s.pub_date.isoformat() if s.pub_date else "unknown-date"
        evidence_lines.append(
            f"{i}. [{s.bucket} score={s.score:.1f} date={pd}] {s.title}\n"
            f"   snippet: {s.snippet}\n"
            f"   link: {s.url}"
        )
    evidence = "\n".join(evidence_lines)

    prompt = f"""
You are generating a historical "daily market briefing" for {target_day.isoformat()}.

Use ONLY the NYT sources below. Do not invent facts, numbers, or events not supported by the snippets/titles.
Output EXACTLY 5 items, each formatted as:
"HEADLINE": "DETAIL (include 1–3 numbers/stats if present; otherwise be qualitative) [SOURCE_LINK]"

Composition goals (already mostly satisfied by the selection):
- 2 macro/systemic
- 1 crash/stress
- 1 movers/cross-asset
- 1 company-focused; prefer these companies if present: {", ".join(company_focus)}

NYT SOURCES:
{evidence}
""".strip()

    resp = client.chat.completions.create(
        model=model,
        messages=[
            {"role": "system", "content": "You are a financial news summarizer."},
            {"role": "user", "content": prompt},
        ],
        temperature=0.4,
    )
    return resp.choices[0].message.content


# -----------------------------
# Example run
# -----------------------------

if __name__ == "__main__":
    target = date.fromisoformat("2025-04-09")

    # Optional: movers from your market engine (tie-breaker only; capped at +10)
    movers = [
        {"symbol": "SILVER", "name": "silver", "pct_move": -0.50},
        {"symbol": "SPY", "name": "S&P 500", "pct_move": -0.03},
    ]

    company_focus = ["GOOG", "NVDA", "META", "TSLA"]

    all_scored, pack = fetch_and_rank_windowed(
        target_day=target,
        movers=movers,
        company_focus=company_focus,
    )

    print(f"Fetched candidates: {len(all_scored)}")
    print("Selected pack:")
    for s in pack:
        print(f"  - {s.score:.1f} [{s.bucket}] {s.title} ({s.url})")

    print("\n--- OpenAI Briefing ---\n")
    briefing = generate_briefing_with_openai(target, pack, company_focus=company_focus, model=DEFAULT_MODEL)
    print(briefing)