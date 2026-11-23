# ER-диаграмма базы данных

Выполнили: Фурсов Артем и Сподриков Матвей.

```mermaid
erDiagram
 USERS ||--o{ BOOKINGS : places
 CATEGORIES ||--o{ EQUIPMENT : groups
 EQUIPMENT ||--o{ BOOKINGS : rented
 EQUIPMENT ||--o{ INVENTORY_UNITS : contains
 INVENTORY_UNITS ||--o{ INVENTORY_HISTORY : changes
 BOOKINGS ||--o| BOOKING_UNITS : assignment
 INVENTORY_UNITS ||--o{ BOOKING_UNITS : assigned
 USERS { int id PK
 string email UK
 string name
 string password
 int role }
 CATEGORIES { int id PK
 string name }
 EQUIPMENT { int id PK
 int category_id FK
 string name
 decimal price_per_day
 int total_units
 int repair_units
 int written_off_units }
 BOOKINGS { int id PK
 int equipment_id FK
 int user_id FK
 date start_date
 date end_date
 decimal total_price
 int status
 decimal fine }
 INVENTORY_UNITS { int id PK
 int equipment_id FK
 string inventory_number UK
 string state }
 BOOKING_UNITS { int booking_id PK,FK
 int unit_id FK
 string assigned_at }
 INVENTORY_HISTORY { int id PK
 int unit_id FK
 string old_state
 string new_state
 string changed_at }
```

Связи и поля соответствуют основной схеме SQLite; полный набор полей и ограничений приведён в `database.sql`. Старый снимок `app_state` сохраняется при миграции.

