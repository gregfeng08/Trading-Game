CREATE TABLE IF NOT EXISTS loaded_ticker_list (
               ticker_id TEXT PRIMARY KEY, 
               company_name TEXT, 
               description TEXT,
               UNIQUE(ticker_id)
);

CREATE TABLE IF NOT EXISTS ticker_prices (
    ticker_id TEXT,
    open_price REAL,
    high_price REAL,
    low_price REAL,
    close_price REAL,
    date TEXT,
    UNIQUE(ticker_id, date)
);

CREATE TABLE IF NOT EXISTS entity (
    entity_id INTEGER PRIMARY KEY AUTOINCREMENT,
    isPlayer INTEGER,
    available_cash REAL
);

CREATE TABLE IF NOT EXISTS trade_history (
    history_id INTEGER PRIMARY KEY AUTOINCREMENT,
    entity_id INTEGER,
    ticker_id TEXT,
    price_paid REAL,
    shares REAL,
    trade_date TEXT,
    FOREIGN KEY(entity_id) REFERENCES entity(entity_id),
    FOREIGN KEY(ticker_id) REFERENCES loaded_ticker_list(ticker_id)
);

CREATE TABLE IF NOT EXISTS portfolio (
    portfolio_id INTEGER PRIMARY KEY AUTOINCREMENT,
    entity_id INTEGER,
    ticker_id TEXT,
    shares_held REAL,
    purchase_date TEXT,
    price REAL,
    FOREIGN KEY(entity_id) REFERENCES entity(entity_id),
    FOREIGN KEY(ticker_id) REFERENCES loaded_ticker_list(ticker_id)
);

CREATE TABLE IF NOT EXISTS save_state (
    key TEXT PRIMARY KEY,
    value TEXT
);

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