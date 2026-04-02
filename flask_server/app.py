# server.py
from flask import Flask, request, jsonify
import sqlite3
import time
from datetime import datetime

from db import get_db_connection
# IMPORTANT: update your ticker_download.py to expose load_top_n_tickers_range(start, end, top_n)
from ticker_download import load_top_n_tickers_range

app = Flask(__name__)
START_TIME = time.time()

# ------------------------------------------------------------
# Helpers
# ------------------------------------------------------------

def _drop_all_tables(conn):
    """
    DEV-ONLY: Drops every table in the SQLite DB.
    Keeps SQLite internal tables safe.
    """
    cur = conn.cursor()
    cur.execute("SELECT name FROM sqlite_master WHERE type='table';")
    tables = [r[0] if not isinstance(r, dict) else r["name"] for r in cur.fetchall()]

    for name in tables:
        # skip SQLite internal tables
        if name.startswith("sqlite_"):
            continue
        cur.execute(f'DROP TABLE IF EXISTS "{name}";')

    conn.commit()


def _require_fields(data, fields):
    missing = [f for f in fields if f not in data or data[f] is None]
    if missing:
        return jsonify({"status": "error", "message": f"Missing fields: {missing}"}), 400
    return None

def _row_to_dict(row):
    return dict(row) if row is not None else None

def _ensure_entity_map(conn):
    conn.execute(
        """
        CREATE TABLE IF NOT EXISTS entity_map (
            external_id TEXT PRIMARY KEY,
            entity_db_id INTEGER NOT NULL
        );
        """
    )

def _get_close_price(conn, ticker_id: str, date_iso: str):
    cur = conn.cursor()
    cur.execute(
        """
        SELECT close_price
        FROM ticker_prices
        WHERE ticker_id = ? AND date = ?
        """,
        (ticker_id, date_iso),
    )
    r = cur.fetchone()
    return float(r["close_price"]) if r else None

def _get_entity(conn, entity_id: int):
    cur = conn.cursor()
    cur.execute(
        "SELECT entity_id, isPlayer, available_cash FROM entity WHERE entity_id = ?",
        (entity_id,),
    )
    return cur.fetchone()

def _get_total_shares(conn, entity_id: int, ticker_id: str):
    cur = conn.cursor()
    cur.execute(
        """
        SELECT COALESCE(SUM(shares_held), 0) AS total
        FROM portfolio
        WHERE entity_id = ? AND ticker_id = ?
        """,
        (entity_id, ticker_id),
    )
    r = cur.fetchone()
    return float(r["total"]) if r else 0.0

def _sell_fifo(conn, entity_id: int, ticker_id: str, shares_to_sell: float):
    """
    Reduce lots FIFO in portfolio. Assumes caller already validated enough shares exist.
    Returns list of lot reductions:
      [{"portfolio_id":..., "sold":..., "lot_price":..., "purchase_date":...}, ...]
    """
    cur = conn.cursor()
    reductions = []

    cur.execute(
        """
        SELECT portfolio_id, shares_held, purchase_date, price
        FROM portfolio
        WHERE entity_id = ? AND ticker_id = ?
        ORDER BY purchase_date ASC, portfolio_id ASC
        """,
        (entity_id, ticker_id),
    )
    lots = cur.fetchall()

    remaining = shares_to_sell
    for lot in lots:
        if remaining <= 0:
            break

        lot_id = int(lot["portfolio_id"])
        lot_shares = float(lot["shares_held"])
        lot_price = float(lot["price"]) if lot["price"] is not None else None
        lot_date = lot["purchase_date"]

        take = min(lot_shares, remaining)
        new_shares = lot_shares - take

        if new_shares <= 0:
            cur.execute("DELETE FROM portfolio WHERE portfolio_id = ?", (lot_id,))
        else:
            cur.execute(
                "UPDATE portfolio SET shares_held = ? WHERE portfolio_id = ?",
                (new_shares, lot_id),
            )

        reductions.append(
            {
                "portfolio_id": lot_id,
                "sold": take,
                "lot_price": lot_price,
                "purchase_date": lot_date,
            }
        )
        remaining -= take

    if remaining > 1e-9:
        raise RuntimeError("FIFO sell failed: not enough shares in lots.")

    return reductions

def _parse_date_yyyy_mm_dd(date_str: str):
    datetime.strptime(date_str, "%Y-%m-%d")
    return date_str

# ------------------------------------------------------------
# Compatibility Endpoints (Unity expects these)
# ------------------------------------------------------------

@app.route("/ping", methods=["GET"])
def ping():
    return jsonify({"status": "ok", "server_time": time.time()})

@app.route("/status", methods=["GET"])
def status():
    db_ok = True
    try:
        conn = get_db_connection()
        conn.execute("SELECT 1;")
        conn.close()
    except Exception:
        db_ok = False

    return jsonify(
        {
            "status": "ok",
            "server_time": time.time(),
            "uptime_seconds": time.time() - START_TIME,
            "db_connected": db_ok,
            "version": "local-dev",
        }
    )

@app.route("/init_db", methods=["POST"])
def init_db():
    """
    Unity expects: { status, message, initialized }
    """
    try:
        conn = get_db_connection()
        with open("schema.sql", "r", encoding="utf-8") as f:
            conn.executescript(f.read())
        conn.close()
        return jsonify({"status": "ok", "message": "Database initialized", "initialized": True})
    except Exception as e:
        return jsonify({"status": "error", "message": str(e), "initialized": False}), 500

from datetime import datetime, timedelta, timezone

def _to_utc_midnight(date_str: str) -> datetime:
    d = datetime.strptime(date_str, "%Y-%m-%d").date()
    return datetime(d.year, d.month, d.day, tzinfo=timezone.utc)

import traceback

@app.route("/load_tickers", methods=["POST"])
def load_tickers():
    data = request.get_json(silent=True) or {}

    start_date = data.get("start_date", "2010-01-01")  # pick a Yahoo-supported range
    end_date   = data.get("end_date",   "2011-12-31")
    top_n      = int(data.get("top_n", 50))

    # basic validation
    try:
        datetime.strptime(start_date, "%Y-%m-%d")
        datetime.strptime(end_date, "%Y-%m-%d")
        if end_date <= start_date:
            return jsonify({"status": "error", "message": "end_date must be after start_date", "ticker_count": 0}), 400
    except ValueError:
        return jsonify({"status": "error", "message": "Dates must be YYYY-MM-DD", "ticker_count": 0}), 400

    conn = None
    try:
        # Use one connection so we can reset the same DB file on failure
        conn = get_db_connection()

        # Optional: lock early so nothing else writes during load
        conn.execute("BEGIN IMMEDIATE;")

        # Call your loader (it should use this same DB file; that's fine)
        tickers = load_top_n_tickers_range(conn, start_date, end_date, top_n)

        # Commit the load transaction
        conn.commit()

        return jsonify({
            "status": "ok",
            "message": "Tickers loaded",
            "ticker_count": len(tickers),
        })

    except Exception as e:
        tb = traceback.format_exc()
        print("\n=== ERROR in /load_tickers ===\n", tb)

        # rollback current tx if possible
        try:
            if conn:
                conn.rollback()
        except Exception:
            pass

        # DEV behavior you requested: wipe everything for a clean reset
        try:
            if conn is None:
                conn = get_db_connection()
            _drop_all_tables(conn)
        except Exception as reset_err:
            # If even reset fails, surface that too
            reset_tb = traceback.format_exc()
            print("\n=== ERROR during reset ===\n", reset_tb)
            return jsonify({
                "status": "error",
                "message": f"load_tickers failed: {str(e)}; then reset failed: {str(reset_err)}",
                "traceback": tb,
                "ticker_count": 0,
            }), 500

        return jsonify({
            "status": "error",
            "message": f"load_tickers failed and DB was reset: {str(e)}",
            "traceback": tb,
            "ticker_count": 0,
        }), 500

    finally:
        try:
            if conn:
                conn.close()
        except Exception:
            pass


@app.route("/register_entity", methods=["POST"])
def register_entity():
    """
    Unity sends:
      { entity_id: "player_001", entity_type: "player", display_name: "Player" }

    Unity expects:
      { status, message, entity_db_id, already_exists }
    """
    data = request.get_json(force=True) or {}
    err = _require_fields(data, ["entity_id", "entity_type", "display_name"])
    if err:
        return err

    external_id = str(data["entity_id"]).strip()
    entity_type = str(data["entity_type"]).strip().lower()

    is_player = 1 if entity_type == "player" else 0

    # Optional override from client (if you want it)
    starting_cash = float(data.get("starting_cash", 10000.0))

    conn = None
    try:
        conn = get_db_connection()
        conn.execute("BEGIN IMMEDIATE;")
        _ensure_entity_map(conn)

        cur = conn.cursor()
        cur.execute("SELECT entity_db_id FROM entity_map WHERE external_id = ?", (external_id,))
        row = cur.fetchone()
        if row:
            conn.commit()
            return jsonify(
                {
                    "status": "ok",
                    "message": "Entity already exists",
                    "entity_db_id": int(row["entity_db_id"]),
                    "already_exists": True,
                }
            )

        cur.execute(
            "INSERT INTO entity (isPlayer, available_cash) VALUES (?, ?)",
            (is_player, starting_cash),
        )
        entity_db_id = cur.lastrowid

        cur.execute(
            "INSERT INTO entity_map (external_id, entity_db_id) VALUES (?, ?)",
            (external_id, entity_db_id),
        )

        conn.commit()
        return jsonify(
            {
                "status": "ok",
                "message": "Entity created",
                "entity_db_id": int(entity_db_id),
                "already_exists": False,
            }
        )
    except Exception as e:
        if conn:
            try:
                conn.rollback()
            except Exception:
                pass
        return jsonify({"status": "error", "message": str(e)}), 500
    finally:
        if conn:
            try:
                conn.close()
            except Exception:
                pass

@app.route("/get_daily_data", methods=["GET"])
def get_daily_data():
    """
    Unity expects:
      { data: [ {ticker,date,open,high,low,close,volume,sma20,sma50,sma200}, ... ] }

    Your DB currently has open_price/high_price/low_price/close_price, and likely no volume/sma columns.
    We return zeros for missing fields for now.
    """
    ticker = request.args.get("ticker")  # optional
    limit = int(request.args.get("limit", "500"))

    conn = get_db_connection()
    cur = conn.cursor()

    if ticker:
        t = ticker.upper().strip()
        cur.execute(
            """
            SELECT ticker_id, date, open_price, high_price, low_price, close_price
            FROM ticker_prices
            WHERE ticker_id = ?
            ORDER BY date DESC
            LIMIT ?
            """,
            (t, limit),
        )
    else:
        cur.execute(
            """
            SELECT ticker_id, date, open_price, high_price, low_price, close_price
            FROM ticker_prices
            ORDER BY date DESC
            LIMIT ?
            """,
            (limit,),
        )

    rows = cur.fetchall()
    conn.close()

    data = []
    for r in rows:
        data.append(
            {
                "ticker": r["ticker_id"],
                "date": r["date"],
                "open": float(r["open_price"]) if r["open_price"] is not None else 0.0,
                "high": float(r["high_price"]) if r["high_price"] is not None else 0.0,
                "low": float(r["low_price"]) if r["low_price"] is not None else 0.0,
                "close": float(r["close_price"]) if r["close_price"] is not None else 0.0,
                "volume": 0.0,
                "sma20": 0.0,
                "sma50": 0.0,
                "sma200": 0.0,
            }
        )

    return jsonify({"data": data})

# ------------------------------------------------------------
# Existing Routes (kept as-is, plus small polish)
# ------------------------------------------------------------

@app.route("/")
def hello_world():
    return "<p>Hello World</p>"

@app.route("/db_create", methods=["POST"])
def db_create():
    conn = get_db_connection()
    with open("schema.sql", "r", encoding="utf-8") as f:
        conn.executescript(f.read())
    conn.close()
    return jsonify({"ok": True, "message": "Database Initialized"})

@app.route("/db_reset", methods=["POST"])
def db_reset():
    conn = get_db_connection()
    cur = conn.cursor()

    cur.execute("SELECT name FROM sqlite_master WHERE type='table';")
    tables = cur.fetchall()

    for t in tables:
        table_name = t[0]
        cur.execute(f"DROP TABLE IF EXISTS {table_name};")

    conn.commit()
    conn.close()
    return jsonify({"ok": True, "message": "Database Reset (tables dropped). Call /db_create to recreate schema."})

# ------------------------------------------------------------
# Market / data loading (existing)
# ------------------------------------------------------------

@app.route("/load_universe", methods=["POST"])
def load_universe():
    """
    POST JSON:
    {
      "start_date": "YYYY-MM-DD",
      "end_date": "YYYY-MM-DD",
      "top_n": 50
    }
    """
    data = request.get_json(force=True) or {}
    missing = [k for k in ["start_date", "end_date", "top_n"] if k not in data or data[k] is None]
    if missing:
        return jsonify({"ok": False, "error": f"Missing fields: {missing}"}), 400

    start_date = data["start_date"]
    end_date = data["end_date"]
    top_n = int(data["top_n"])

    try:
        _parse_date_yyyy_mm_dd(start_date)
        _parse_date_yyyy_mm_dd(end_date)
    except ValueError:
        return jsonify({"ok": False, "error": "Dates must be YYYY-MM-DD"}), 400

    tickers = load_top_n_tickers_range(start_date, end_date, top_n)

    return jsonify(
        {
            "ok": True,
            "start_date": start_date,
            "end_date": end_date,
            "top_n": top_n,
            "tickers_loaded": len(tickers),
            "tickers": tickers,
        }
    )

@app.route("/tickers", methods=["GET"])
def list_tickers():
    conn = get_db_connection()
    cur = conn.cursor()
    cur.execute(
        """
        SELECT ticker_id, company_name, description
        FROM loaded_ticker_list
        ORDER BY ticker_id
        """
    )
    rows = cur.fetchall()
    conn.close()
    return jsonify({"ok": True, "tickers": [dict(r) for r in rows]})

@app.route("/prices", methods=["GET"])
def get_prices():
    """
    GET params:
      ticker_id=...
      start_date=YYYY-MM-DD (optional)
      end_date=YYYY-MM-DD (optional)
    """
    ticker_id = request.args.get("ticker_id")
    if not ticker_id:
        return jsonify({"ok": False, "error": "ticker_id is required"}), 400

    start_date = request.args.get("start_date")
    end_date = request.args.get("end_date")

    conn = get_db_connection()
    cur = conn.cursor()

    if start_date and end_date:
        cur.execute(
            """
            SELECT ticker_id, date, open_price, high_price, low_price, close_price
            FROM ticker_prices
            WHERE ticker_id = ? AND date BETWEEN ? AND ?
            ORDER BY date
            """,
            (ticker_id, start_date, end_date),
        )
    else:
        cur.execute(
            """
            SELECT ticker_id, date, open_price, high_price, low_price, close_price
            FROM ticker_prices
            WHERE ticker_id = ?
            ORDER BY date
            """,
            (ticker_id,),
        )

    rows = cur.fetchall()
    conn.close()
    return jsonify({"ok": True, "ticker_id": ticker_id, "rows": [dict(r) for r in rows]})

# ------------------------------------------------------------
# Entities (existing)
# ------------------------------------------------------------

@app.route("/entity_create", methods=["POST"])
def entity_create():
    """
    POST JSON:
    { "isPlayer": 1, "starting_cash": 10000.0 }
    """
    data = request.get_json(force=True) or {}
    missing = [k for k in ["isPlayer", "starting_cash"] if k not in data or data[k] is None]
    if missing:
        return jsonify({"ok": False, "error": f"Missing fields: {missing}"}), 400

    is_player = int(data["isPlayer"])
    starting_cash = float(data["starting_cash"])

    conn = get_db_connection()
    cur = conn.cursor()
    cur.execute(
        """
        INSERT INTO entity (isPlayer, available_cash)
        VALUES (?, ?)
        """,
        (is_player, starting_cash),
    )
    entity_id = cur.lastrowid
    conn.commit()
    conn.close()

    return jsonify({"ok": True, "entity_id": entity_id, "isPlayer": is_player, "available_cash": starting_cash})

@app.route("/entity", methods=["GET"])
def entity_get():
    """
    GET params: entity_id=...
    """
    entity_id = request.args.get("entity_id")
    if not entity_id:
        return jsonify({"ok": False, "error": "entity_id is required"}), 400

    conn = get_db_connection()
    ent = _get_entity(conn, int(entity_id))
    conn.close()

    if not ent:
        return jsonify({"ok": False, "error": "Entity not found"}), 404

    return jsonify({"ok": True, "entity": dict(ent)})

# ------------------------------------------------------------
# Portfolio / Trade (existing)
# ------------------------------------------------------------

@app.route("/portfolio", methods=["GET"])
def portfolio_get():
    """
    GET params: entity_id=...
    Returns:
      - lots in portfolio table
      - aggregated totals per ticker
    """
    entity_id = request.args.get("entity_id")
    if not entity_id:
        return jsonify({"ok": False, "error": "entity_id is required"}), 400

    conn = get_db_connection()
    cur = conn.cursor()

    cur.execute(
        """
        SELECT portfolio_id, entity_id, ticker_id, shares_held, purchase_date, price
        FROM portfolio
        WHERE entity_id = ?
        ORDER BY ticker_id, purchase_date
        """,
        (int(entity_id),),
    )
    lots = [dict(r) for r in cur.fetchall()]

    cur.execute(
        """
        SELECT ticker_id, COALESCE(SUM(shares_held), 0) AS shares_held
        FROM portfolio
        WHERE entity_id = ?
        GROUP BY ticker_id
        ORDER BY ticker_id
        """,
        (int(entity_id),),
    )
    totals = [dict(r) for r in cur.fetchall()]

    ent = _get_entity(conn, int(entity_id))
    conn.close()

    if not ent:
        return jsonify({"ok": False, "error": "Entity not found"}), 404

    return jsonify({"ok": True, "entity": dict(ent), "lots": lots, "totals": totals})

@app.route("/trade_history", methods=["GET"])
def trade_history_get():
    """
    GET params:
      entity_id=... (optional)
      ticker_id=... (optional)
    """
    entity_id = request.args.get("entity_id")
    ticker_id = request.args.get("ticker_id")

    conn = get_db_connection()
    cur = conn.cursor()

    clauses = []
    params = []
    if entity_id:
        clauses.append("entity_id = ?")
        params.append(int(entity_id))
    if ticker_id:
        clauses.append("ticker_id = ?")
        params.append(ticker_id)

    where = ("WHERE " + " AND ".join(clauses)) if clauses else ""
    cur.execute(
        f"""
        SELECT history_id, entity_id, ticker_id, price_paid, shares, trade_date
        FROM trade_history
        {where}
        ORDER BY trade_date, history_id
        """,
        tuple(params),
    )
    rows = [dict(r) for r in cur.fetchall()]
    conn.close()

    return jsonify({"ok": True, "rows": rows})

# trade [entity_id] [ticker_id] [shares] [date]
@app.route("/trade", methods=["POST"])
def trade():
    """
    POST JSON:
    {
      "entity_id": 1,
      "ticker_id": "AAPL",
      "shares": 10,          # positive = BUY, negative = SELL
      "date": "1930-01-02"
    }
    """
    data = request.get_json(force=True) or {}
    missing = [k for k in ["entity_id", "ticker_id", "shares", "date"] if k not in data or data[k] is None]
    if missing:
        return jsonify({"ok": False, "error": f"Missing fields: {missing}"}), 400

    entity_id = int(data["entity_id"])
    ticker_id = str(data["ticker_id"]).upper().strip()
    shares = float(data["shares"])
    date_iso = data["date"]

    if abs(shares) < 1e-12:
        return jsonify({"ok": False, "error": "shares cannot be 0"}), 400

    try:
        _parse_date_yyyy_mm_dd(date_iso)
    except ValueError:
        return jsonify({"ok": False, "error": "date must be YYYY-MM-DD"}), 400

    conn = get_db_connection()
    try:
        conn.execute("BEGIN IMMEDIATE;")

        ent = _get_entity(conn, entity_id)
        if not ent:
            conn.rollback()
            conn.close()
            return jsonify({"ok": False, "error": "Entity not found"}), 404

        price = _get_close_price(conn, ticker_id, date_iso)
        if price is None:
            conn.rollback()
            conn.close()
            return jsonify({"ok": False, "error": f"No price for {ticker_id} on {date_iso}"}), 400

        cur = conn.cursor()
        current_cash = float(ent["available_cash"])

        if shares > 0:
            cost = price * shares
            if current_cash + 1e-9 < cost:
                conn.rollback()
                conn.close()
                return jsonify(
                    {
                        "ok": False,
                        "error": "Insufficient cash",
                        "available_cash": current_cash,
                        "required_cash": cost,
                        "price": price,
                    }
                ), 400

            new_cash = current_cash - cost
            cur.execute(
                "UPDATE entity SET available_cash = ? WHERE entity_id = ?",
                (new_cash, entity_id),
            )

            cur.execute(
                """
                INSERT INTO portfolio (entity_id, ticker_id, shares_held, purchase_date, price)
                VALUES (?, ?, ?, ?, ?)
                """,
                (entity_id, ticker_id, shares, date_iso, price),
            )

            cur.execute(
                """
                INSERT INTO trade_history (entity_id, ticker_id, price_paid, shares, trade_date)
                VALUES (?, ?, ?, ?, ?)
                """,
                (entity_id, ticker_id, price, shares, date_iso),
            )

            conn.commit()
            conn.close()

            return jsonify(
                {
                    "ok": True,
                    "side": "BUY",
                    "entity_id": entity_id,
                    "ticker_id": ticker_id,
                    "shares": shares,
                    "price": price,
                    "cash_before": current_cash,
                    "cash_after": new_cash,
                }
            )

        else:
            sell_qty = -shares
            held = _get_total_shares(conn, entity_id, ticker_id)
            if held + 1e-9 < sell_qty:
                conn.rollback()
                conn.close()
                return jsonify(
                    {
                        "ok": False,
                        "error": "Insufficient shares",
                        "shares_held": held,
                        "shares_requested_to_sell": sell_qty,
                    }
                ), 400

            proceeds = price * sell_qty
            new_cash = current_cash + proceeds

            cur.execute(
                "UPDATE entity SET available_cash = ? WHERE entity_id = ?",
                (new_cash, entity_id),
            )

            reductions = _sell_fifo(conn, entity_id, ticker_id, sell_qty)

            cur.execute(
                """
                INSERT INTO trade_history (entity_id, ticker_id, price_paid, shares, trade_date)
                VALUES (?, ?, ?, ?, ?)
                """,
                (entity_id, ticker_id, price, shares, date_iso),
            )

            conn.commit()
            conn.close()

            return jsonify(
                {
                    "ok": True,
                    "side": "SELL",
                    "entity_id": entity_id,
                    "ticker_id": ticker_id,
                    "shares": shares,
                    "price": price,
                    "cash_before": current_cash,
                    "cash_after": new_cash,
                    "lot_reductions": reductions,
                }
            )

    except sqlite3.Error as e:
        try:
            conn.rollback()
        except Exception:
            pass
        conn.close()
        return jsonify({"ok": False, "error": f"DB error: {str(e)}"}), 500
    except Exception as e:
        try:
            conn.rollback()
        except Exception:
            pass
        conn.close()
        return jsonify({"ok": False, "error": str(e)}), 500

# ------------------------------------------------------------
# Save state (simple key/value) (existing)
# ------------------------------------------------------------

@app.route("/save_state", methods=["POST"])
def save_state_set():
    """
    POST JSON: { "key": "...", "value": "..." }
    """
    data = request.get_json(force=True) or {}
    missing = [k for k in ["key", "value"] if k not in data or data[k] is None]
    if missing:
        return jsonify({"ok": False, "error": f"Missing fields: {missing}"}), 400

    key = str(data["key"])
    value = str(data["value"])

    conn = get_db_connection()
    cur = conn.cursor()
    cur.execute(
        """
        INSERT INTO save_state (key, value)
        VALUES (?, ?)
        ON CONFLICT(key) DO UPDATE SET value=excluded.value
        """,
        (key, value),
    )
    conn.commit()
    conn.close()
    return jsonify({"ok": True, "key": key, "value": value})

@app.route("/save_state", methods=["GET"])
def save_state_get():
    """
    GET params: key=...
    """
    key = request.args.get("key")
    if not key:
        return jsonify({"ok": False, "error": "key is required"}), 400

    conn = get_db_connection()
    cur = conn.cursor()
    cur.execute("SELECT key, value FROM save_state WHERE key = ?", (key,))
    row = cur.fetchone()
    conn.close()

    if not row:
        return jsonify({"ok": False, "error": "Key not found"}), 404
    return jsonify({"ok": True, "item": dict(row)})

# ------------------------------------------------------------
# Run
# ------------------------------------------------------------
if __name__ == "__main__":
    app.run(debug=True)
