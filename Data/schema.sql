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

-- Indexes for common query patterns
CREATE INDEX IF NOT EXISTS idx_ticker_prices_ticker_date ON ticker_prices(ticker_id, date);
CREATE INDEX IF NOT EXISTS idx_portfolio_entity_ticker ON portfolio(entity_id, ticker_id);
CREATE INDEX IF NOT EXISTS idx_trade_history_entity ON trade_history(entity_id, ticker_id);
