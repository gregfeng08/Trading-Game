-- Ticker universe
CREATE TABLE IF NOT EXISTS loaded_ticker_list (
    ticker_id TEXT PRIMARY KEY,
    company_name TEXT,
    description TEXT
);

-- Daily OHLC price data
CREATE TABLE IF NOT EXISTS ticker_prices (
    ticker_id TEXT NOT NULL,
    open_price REAL,
    high_price REAL,
    low_price REAL,
    close_price REAL,
    date TEXT NOT NULL,
    UNIQUE(ticker_id, date),
    FOREIGN KEY(ticker_id) REFERENCES loaded_ticker_list(ticker_id)
);

-- Game entities (player + NPCs)
CREATE TABLE IF NOT EXISTS entity (
    entity_id INTEGER PRIMARY KEY AUTOINCREMENT,
    is_player INTEGER NOT NULL,
    available_cash REAL NOT NULL
);

-- Maps external IDs (e.g. Unity "player_001") to internal entity_id
CREATE TABLE IF NOT EXISTS entity_map (
    external_id TEXT PRIMARY KEY,
    entity_db_id INTEGER NOT NULL,
    FOREIGN KEY(entity_db_id) REFERENCES entity(entity_id)
);

-- Trade history log
CREATE TABLE IF NOT EXISTS trade_history (
    history_id INTEGER PRIMARY KEY AUTOINCREMENT,
    entity_id INTEGER NOT NULL,
    ticker_id TEXT NOT NULL,
    price_paid REAL NOT NULL,
    shares REAL NOT NULL,
    trade_date TEXT NOT NULL,
    FOREIGN KEY(entity_id) REFERENCES entity(entity_id),
    FOREIGN KEY(ticker_id) REFERENCES loaded_ticker_list(ticker_id)
);

-- Portfolio lots (FIFO tracking)
CREATE TABLE IF NOT EXISTS portfolio (
    portfolio_id INTEGER PRIMARY KEY AUTOINCREMENT,
    entity_id INTEGER NOT NULL,
    ticker_id TEXT NOT NULL,
    shares_held REAL NOT NULL,
    purchase_date TEXT NOT NULL,
    price REAL NOT NULL,
    FOREIGN KEY(entity_id) REFERENCES entity(entity_id),
    FOREIGN KEY(ticker_id) REFERENCES loaded_ticker_list(ticker_id)
);

-- Simple key/value save state
CREATE TABLE IF NOT EXISTS save_state (
    key TEXT PRIMARY KEY,
    value TEXT
);

-- NPC dialogue tables
CREATE TABLE IF NOT EXISTS static_npc_dialogue (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    date TEXT,
    ticker_id TEXT,
    npc_type TEXT,
    category TEXT,
    text TEXT
);

CREATE TABLE IF NOT EXISTS dynamic_npc_dialogue (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    date TEXT,
    ticker_id TEXT,
    npc_type TEXT,
    category TEXT,
    text TEXT
);

-- Knowledge graph progress tracking
CREATE TABLE IF NOT EXISTS knowledge_node_progress (
    entity_id   INTEGER NOT NULL,
    node_id     TEXT NOT NULL,
    status      TEXT NOT NULL DEFAULT 'locked',
    unlocked_at TEXT,
    completed_at TEXT,
    PRIMARY KEY (entity_id, node_id),
    FOREIGN KEY (entity_id) REFERENCES entity(entity_id)
);

-- Pending orders (survive client crashes, executed atomically on market open)
CREATE TABLE IF NOT EXISTS pending_orders (
    order_id INTEGER PRIMARY KEY AUTOINCREMENT,
    entity_id INTEGER NOT NULL,
    ticker_id TEXT NOT NULL,
    side TEXT NOT NULL,
    quantity INTEGER NOT NULL,
    order_type TEXT NOT NULL DEFAULT 'market',
    limit_price REAL,
    queued_at TEXT NOT NULL,
    FOREIGN KEY(entity_id) REFERENCES entity(entity_id),
    FOREIGN KEY(ticker_id) REFERENCES loaded_ticker_list(ticker_id)
);

-- Cached daily newspaper (generated via LLM)
CREATE TABLE IF NOT EXISTS newspaper (
    date TEXT PRIMARY KEY,
    headline TEXT NOT NULL,
    articles_json TEXT NOT NULL,
    market_recap TEXT NOT NULL,
    generated_at TEXT NOT NULL
);

-- Arc (season) grading history
CREATE TABLE IF NOT EXISTS arc_grades (
    arc_id TEXT NOT NULL,
    entity_id INTEGER NOT NULL,
    start_date TEXT NOT NULL,
    end_date TEXT NOT NULL,
    start_value REAL NOT NULL,
    end_value REAL NOT NULL,
    return_pct REAL NOT NULL,
    grade TEXT NOT NULL,
    cash_multiplier REAL NOT NULL,
    completed_at TEXT NOT NULL,
    PRIMARY KEY (arc_id, entity_id),
    FOREIGN KEY (entity_id) REFERENCES entity(entity_id)
);

-- Net worth history (snapshot at each market close)
CREATE TABLE IF NOT EXISTS net_worth_history (
    entity_id INTEGER NOT NULL,
    date TEXT NOT NULL,
    cash REAL NOT NULL,
    holdings_value REAL NOT NULL,
    net_worth REAL NOT NULL,
    PRIMARY KEY (entity_id, date),
    FOREIGN KEY (entity_id) REFERENCES entity(entity_id)
);

-- Indexes for common query patterns
CREATE INDEX IF NOT EXISTS idx_ticker_prices_ticker_date ON ticker_prices(ticker_id, date);
CREATE INDEX IF NOT EXISTS idx_portfolio_entity_ticker ON portfolio(entity_id, ticker_id);
CREATE INDEX IF NOT EXISTS idx_trade_history_entity ON trade_history(entity_id, ticker_id);
CREATE INDEX IF NOT EXISTS idx_knowledge_progress_entity ON knowledge_node_progress(entity_id);
CREATE INDEX IF NOT EXISTS idx_pending_orders_entity ON pending_orders(entity_id);
