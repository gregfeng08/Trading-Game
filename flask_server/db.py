import sqlite3
import os

#Get current connection
def get_db_connection():
    conn = sqlite3.connect("database.db")
    conn.row_factory = sqlite3.Row
    return conn

#Get current path
def get_db_path():
     return os.path.join(os.path.dirname(__file__), "database.db")