-- SQLite: фактическая схема приложения
PRAGMA foreign_keys=ON;
CREATE TABLE IF NOT EXISTS app_state (id INTEGER PRIMARY KEY CHECK(id=1), json TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS users(id INTEGER PRIMARY KEY,name TEXT NOT NULL,email TEXT NOT NULL UNIQUE,password TEXT NOT NULL,role INTEGER NOT NULL CHECK(role BETWEEN 0 AND 2),created_at TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS categories(id INTEGER PRIMARY KEY,name TEXT NOT NULL,icon TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS equipment(id INTEGER PRIMARY KEY,name TEXT NOT NULL,description TEXT NOT NULL,category_id INTEGER NOT NULL REFERENCES categories(id),price_per_day TEXT NOT NULL,total_units INTEGER NOT NULL CHECK(total_units>=0),available_units INTEGER NOT NULL,repair_units INTEGER NOT NULL CHECK(repair_units>=0),written_off_units INTEGER NOT NULL CHECK(written_off_units>=0),state INTEGER NOT NULL,image_url TEXT NOT NULL,tags TEXT NOT NULL,CHECK(repair_units+written_off_units<=total_units));
CREATE TABLE IF NOT EXISTS bookings(id INTEGER PRIMARY KEY,equipment_id INTEGER NOT NULL REFERENCES equipment(id),user_id INTEGER NOT NULL REFERENCES users(id),start_date TEXT NOT NULL,end_date TEXT NOT NULL,total_price TEXT NOT NULL,daily_rate TEXT NOT NULL,status INTEGER NOT NULL,return_condition TEXT,fine TEXT NOT NULL,created_at TEXT NOT NULL,CHECK(end_date>=start_date));
CREATE TABLE IF NOT EXISTS inventory_units(id INTEGER PRIMARY KEY,equipment_id INTEGER NOT NULL REFERENCES equipment(id),inventory_number TEXT NOT NULL UNIQUE,state TEXT NOT NULL CHECK(state IN ('available','repair','written_off')));
CREATE TABLE IF NOT EXISTS inventory_history(id INTEGER PRIMARY KEY,unit_id INTEGER NOT NULL REFERENCES inventory_units(id),old_state TEXT,new_state TEXT NOT NULL,changed_at TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_bookings_period ON bookings(equipment_id,start_date,end_date);
CREATE TABLE IF NOT EXISTS schema_version(id INTEGER PRIMARY KEY CHECK(id=1),version INTEGER NOT NULL);


CREATE TABLE IF NOT EXISTS booking_units(booking_id INTEGER PRIMARY KEY REFERENCES bookings(id),unit_id INTEGER NOT NULL REFERENCES inventory_units(id),assigned_at TEXT NOT NULL);

