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
    trade_phase TEXT,
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
    text TEXT,
    mood TEXT,
    phase TEXT,
    priority TEXT DEFAULT 'low',
    line_order INTEGER DEFAULT 0
);

CREATE TABLE IF NOT EXISTS dynamic_npc_dialogue (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    date TEXT,
    ticker_id TEXT,
    npc_type TEXT,
    category TEXT,
    text TEXT,
    phase TEXT,
    priority TEXT DEFAULT 'medium',
    trigger_source TEXT,
    entity_id INTEGER,
    line_order INTEGER DEFAULT 0,
    FOREIGN KEY(entity_id) REFERENCES entity(entity_id)
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
    stop_price REAL,
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
    phase TEXT NOT NULL DEFAULT 'close',
    cash REAL NOT NULL,
    holdings_value REAL NOT NULL,
    net_worth REAL NOT NULL,
    PRIMARY KEY (entity_id, date, phase),
    FOREIGN KEY (entity_id) REFERENCES entity(entity_id)
);

-- Dynamic knowledge node content (personalized via LLM)
CREATE TABLE IF NOT EXISTS dynamic_node_content (
    entity_id INTEGER NOT NULL,
    node_id TEXT NOT NULL,
    content TEXT NOT NULL,
    trigger_context TEXT,
    trigger_path TEXT,
    generated_at TEXT NOT NULL,
    PRIMARY KEY (entity_id, node_id),
    FOREIGN KEY (entity_id) REFERENCES entity(entity_id)
);

-- Player events (newspaper reads, NPC interactions — used as knowledge triggers)
CREATE TABLE IF NOT EXISTS player_events (
    event_id INTEGER PRIMARY KEY AUTOINCREMENT,
    entity_id INTEGER NOT NULL,
    event_type TEXT NOT NULL,
    event_date TEXT NOT NULL,
    metadata_json TEXT,
    created_at TEXT NOT NULL,
    UNIQUE(entity_id, event_type, event_date),
    FOREIGN KEY (entity_id) REFERENCES entity(entity_id)
);

-- NPC quest progress (typed NPC conversations that unlock knowledge nodes)
CREATE TABLE IF NOT EXISTS npc_quest_progress (
    entity_id INTEGER NOT NULL,
    npc_type TEXT NOT NULL,
    node_id TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'available',
    started_at TEXT,
    completed_at TEXT,
    PRIMARY KEY (entity_id, npc_type, node_id),
    FOREIGN KEY (entity_id) REFERENCES entity(entity_id)
);

-- Casey's daily closing comments (LLM-generated, cached per day)
CREATE TABLE IF NOT EXISTS casey_daily_comments (
    entity_id INTEGER NOT NULL,
    date TEXT NOT NULL,
    comment TEXT NOT NULL,
    generated_at TEXT NOT NULL,
    PRIMARY KEY (entity_id, date),
    FOREIGN KEY (entity_id) REFERENCES entity(entity_id)
);

-- Indexes for common query patterns
CREATE INDEX IF NOT EXISTS idx_ticker_prices_ticker_date ON ticker_prices(ticker_id, date);
CREATE INDEX IF NOT EXISTS idx_portfolio_entity_ticker ON portfolio(entity_id, ticker_id);
CREATE INDEX IF NOT EXISTS idx_trade_history_entity ON trade_history(entity_id, ticker_id);
CREATE INDEX IF NOT EXISTS idx_knowledge_progress_entity ON knowledge_node_progress(entity_id);
CREATE INDEX IF NOT EXISTS idx_pending_orders_entity ON pending_orders(entity_id);
CREATE INDEX IF NOT EXISTS idx_dynamic_dialogue_date_phase ON dynamic_npc_dialogue(date, phase);
CREATE INDEX IF NOT EXISTS idx_static_dialogue_npc_phase ON static_npc_dialogue(npc_type, phase, mood);
CREATE INDEX IF NOT EXISTS idx_player_events_entity_type ON player_events(entity_id, event_type);
CREATE INDEX IF NOT EXISTS idx_npc_quest_progress_entity ON npc_quest_progress(entity_id);
