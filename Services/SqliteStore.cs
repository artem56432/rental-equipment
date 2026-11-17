using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using EquipmentRental.Models;
namespace EquipmentRental.Services;
// Windows includes SQLite as winsqlite3.dll; all values are bound parameters.
public sealed class SqliteStore : IDisposable
{
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string name, out IntPtr database, int flags, IntPtr vfs);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_close(IntPtr database);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_prepare_v2(IntPtr database, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, int length, out IntPtr stmt, IntPtr tail);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_bind_text(IntPtr stmt, int index, byte[] value, int length, IntPtr destructor);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_step(IntPtr stmt);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_finalize(IntPtr stmt);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr sqlite3_column_text(IntPtr stmt, int index);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr sqlite3_errmsg(IntPtr database);
    [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)]
    static extern int sqlite3_column_count(IntPtr stmt);
    readonly IntPtr database;
    public SqliteStore(string file)
    {
        if (sqlite3_open_v2(file, out database, 2 | 4 | 0x10000, IntPtr.Zero) != 0)
        {
            throw new Exception("Не удалось открыть SQLite");
        }
        Execute("PRAGMA foreign_keys=ON");
        Execute("CREATE TABLE IF NOT EXISTS app_state (id INTEGER PRIMARY KEY CHECK(id=1), json TEXT NOT NULL)");
    }
    public string? Read()
    {
        var statement = PrepareStatement("SELECT json FROM app_state WHERE id=1");
        try
        {
            int result = sqlite3_step(statement);
            if (result == 100)
            {
                return Marshal.PtrToStringUTF8(sqlite3_column_text(statement, 0));
            }
            if (result == 101)
            {
                return null;
            }
            throw DatabaseError();
        }
        finally
        {
            sqlite3_finalize(statement);
        }
    }
    public void Write(string json)
    {
        var statement = PrepareStatement("INSERT INTO app_state(id,json) VALUES(1,?) ON CONFLICT(id) DO UPDATE SET json=excluded.json");
        try
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            if (sqlite3_bind_text(statement, 1, bytes, bytes.Length, new IntPtr(-1)) != 0 || sqlite3_step(statement) != 101)
            {
                throw DatabaseError();
            }
        }
        finally
        {
            sqlite3_finalize(statement);
        }
    }
    static string SqlText(string? value) => value == null ? "NULL" : "'" + value.Replace("'", "''") + "'";
    static string SqlValue(object? value) => value == null ? "NULL" : SqlText(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
    public void InitializeSchema()
    {
        foreach (var sql in new[]
        {
            """
CREATE TABLE IF NOT EXISTS users(id INTEGER PRIMARY KEY,name TEXT NOT NULL,email TEXT NOT NULL UNIQUE,password TEXT NOT NULL,role INTEGER NOT NULL CHECK(role BETWEEN 0 AND 2),created_at TEXT NOT NULL)
""",
            "CREATE TABLE IF NOT EXISTS categories(id INTEGER PRIMARY KEY,name TEXT NOT NULL,icon TEXT NOT NULL)",
            """
CREATE TABLE IF NOT EXISTS equipment(id INTEGER PRIMARY KEY,name TEXT NOT NULL,description TEXT NOT NULL,category_id INTEGER NOT NULL REFERENCES categories(id),price_per_day TEXT NOT NULL,total_units INTEGER NOT NULL CHECK(total_units>=0),available_units INTEGER NOT NULL,repair_units INTEGER NOT NULL CHECK(repair_units>=0),written_off_units INTEGER NOT NULL CHECK(written_off_units>=0),state INTEGER NOT NULL,image_url TEXT NOT NULL,tags TEXT NOT NULL,CHECK(repair_units+written_off_units<=total_units))
""",
            """
CREATE TABLE IF NOT EXISTS bookings(id INTEGER PRIMARY KEY,equipment_id INTEGER NOT NULL REFERENCES equipment(id),user_id INTEGER NOT NULL REFERENCES users(id),start_date TEXT NOT NULL,end_date TEXT NOT NULL,total_price TEXT NOT NULL,daily_rate TEXT NOT NULL,status INTEGER NOT NULL,return_condition TEXT,fine TEXT NOT NULL,created_at TEXT NOT NULL,CHECK(end_date>=start_date))
""",
            """
CREATE TABLE IF NOT EXISTS inventory_units(id INTEGER PRIMARY KEY,equipment_id INTEGER NOT NULL REFERENCES equipment(id),inventory_number TEXT NOT NULL UNIQUE,state TEXT NOT NULL CHECK(state IN ('available','repair','written_off')))
""",
            """
CREATE TABLE IF NOT EXISTS booking_units(booking_id INTEGER PRIMARY KEY REFERENCES bookings(id),unit_id INTEGER NOT NULL REFERENCES inventory_units(id),assigned_at TEXT NOT NULL)
""",
            """
CREATE TABLE IF NOT EXISTS inventory_history(id INTEGER PRIMARY KEY,unit_id INTEGER NOT NULL REFERENCES inventory_units(id),old_state TEXT,new_state TEXT NOT NULL,changed_at TEXT NOT NULL)
""",
            "CREATE INDEX IF NOT EXISTS idx_bookings_period ON bookings(equipment_id,start_date,end_date)",
            "CREATE TABLE IF NOT EXISTS schema_version(id INTEGER PRIMARY KEY CHECK(id=1),version INTEGER NOT NULL)"
        }
        )
        {
            Execute(sql);
        }
    }
    string? ReadScalar(string sql)
    {
        var statement = PrepareStatement(sql);
        try
        {
            var result = sqlite3_step(statement);
            if (result == 100)
            {
                return Marshal.PtrToStringUTF8(sqlite3_column_text(statement, 0));
            }
            if (result == 101)
            {
                return null;
            }
            throw DatabaseError();
        }
        finally
        {
            sqlite3_finalize(statement);
        }
    }
    List<string?[]> QueryRows(string sql)
    {
        var list = new List<string?[]>();
        var statement = PrepareStatement(sql);
        try
        {
            int result;
            while ((result = sqlite3_step(statement)) == 100)
            {
                var row = new string?[sqlite3_column_count(statement)];
                for (int index = 0; index < row.Length; index++)
                {
                    row[index] = Marshal.PtrToStringUTF8(sqlite3_column_text(statement, index));
                }
                list.Add(row);
            }
            if (result != 101)
            {
                throw DatabaseError();
            }
            return list;
        }
        finally
        {
            sqlite3_finalize(statement);
        }
    }
    static int ReadInt(string? value) => int.Parse(value!, System.Globalization.CultureInfo.InvariantCulture);
    static decimal ReadDecimal(string? value) => decimal.Parse(value!, System.Globalization.CultureInfo.InvariantCulture);
    static DateTime ReadDate(string? value) => DateTime.Parse(value!, null, System.Globalization.DateTimeStyles.RoundtripKind);
    public AppData? ReadNormalized()
    {
        if (ReadScalar("SELECT version FROM schema_version WHERE id=1") == null)
        {
            return null;
        }
        var data = new AppData();
        foreach (var row in QueryRows("SELECT id,name,email,password,role,created_at FROM users ORDER BY id"))
        {
            data.Users.Add(new User { Id = ReadInt(row[0]), Name = row[1]!, Email = row[2]!, Password = row[3]!, Role = (UserRole)ReadInt(row[4]), CreatedAt = ReadDate(row[5]) });
        }
        foreach (var row in QueryRows("SELECT id,name,icon FROM categories ORDER BY id"))
        {
            data.Categories.Add(new Category { Id = ReadInt(row[0]), Name = row[1]!, Icon = row[2]! });
        }
        foreach (var row in QueryRows("SELECT id,name,description,category_id,price_per_day,total_units,available_units,repair_units,written_off_units,state,image_url,tags FROM equipment ORDER BY id"))
        {
            data.Equipment.Add(new Equipment { Id = ReadInt(row[0]), Name = row[1]!, Description = row[2]!, CategoryId = ReadInt(row[3]), PricePerDay = ReadDecimal(row[4]), TotalUnits = ReadInt(row[5]), AvailableUnits = ReadInt(row[6]), RepairUnits = ReadInt(row[7]), WrittenOffUnits = ReadInt(row[8]), State = (EquipmentState)ReadInt(row[9]), ImageUrl = row[10]!, Tags = row[11]! });
        }
        foreach (var row in QueryRows("SELECT id,equipment_id,user_id,start_date,end_date,total_price,daily_rate,status,return_condition,fine,created_at FROM bookings ORDER BY id"))
        {
            data.Bookings.Add(new Booking { Id = ReadInt(row[0]), EquipmentId = ReadInt(row[1]), UserId = ReadInt(row[2]), StartDate = ReadDate(row[3]), EndDate = ReadDate(row[4]), TotalPrice = ReadDecimal(row[5]), DailyRate = ReadDecimal(row[6]), Status = (BookingStatus)ReadInt(row[7]), ReturnCondition = row[8], Fine = ReadDecimal(row[9]), CreatedAt = ReadDate(row[10]) });
        }
        return data;
    }
    public List<string?[]> InventoryRows() => QueryRows("SELECT u.inventory_number,e.name,u.state FROM inventory_units u JOIN equipment e ON e.id=u.equipment_id ORDER BY e.id,u.id");
    public List<string?[]> InventoryHistory() => QueryRows("SELECT u.inventory_number,h.old_state,h.new_state,h.changed_at FROM inventory_history h JOIN inventory_units u ON u.id=h.unit_id ORDER BY h.id DESC LIMIT 100");
    public List<string?[]> AvailableUnits(int equipmentId) => QueryRows($"""
SELECT id,inventory_number 
FROM inventory_units 
WHERE equipment_id={equipmentId} AND state='available' 
AND id NOT IN (SELECT bu.unit_id 
FROM booking_units bu 
JOIN bookings b ON b.id=bu.booking_id 
WHERE b.status IN (1,3)) 
ORDER BY inventory_number
""");
    public string? UnitNumber(int bookingId) => ReadScalar($"SELECT u.inventory_number FROM booking_units bu JOIN inventory_units u ON u.id=bu.unit_id WHERE bu.booking_id={bookingId}");
    public bool AssignUnit(int bookingId, int equipmentId, int unitId)
    {
        if (ReadScalar($"""
SELECT id 
FROM inventory_units 
WHERE id={unitId} AND equipment_id={equipmentId} AND state='available' 
AND id NOT IN (SELECT bu.unit_id 
FROM booking_units bu 
JOIN bookings b ON b.id=bu.booking_id 
WHERE b.status IN (1,3))
""") == null)
        {
            return false;
        }
        Execute($"""
INSERT INTO booking_units(booking_id,unit_id,assigned_at) 
VALUES({bookingId},{unitId},{SqlText(DateTime.UtcNow.ToString("O"))}) 
ON CONFLICT(booking_id) 
DO UPDATE SET unit_id=excluded.unit_id,assigned_at=excluded.assigned_at
""");
        return true;
    }
    public bool HasIssuedUnits(int equipmentId) => ReadScalar($"SELECT 1 FROM booking_units bu JOIN bookings b ON b.id=bu.booking_id WHERE b.equipment_id={equipmentId} AND b.status IN (1,3) LIMIT 1") != null;
    public void WriteNormalized(AppData data)
    {
        Execute("BEGIN IMMEDIATE");
        try
        {
            foreach (var user in data.Users)
            {
                Execute($"""
INSERT INTO users 
VALUES({user.Id},{SqlText(user.Name)},{SqlText(user.Email)},{SqlText(user.Password)},{(int)user.Role},{SqlText(user.CreatedAt.ToString("O"))}) 
ON CONFLICT(id) 
DO UPDATE SET name=excluded.name,email=excluded.email,password=excluded.password,role=excluded.role,created_at=excluded.created_at
""");
            }
            foreach (var category in data.Categories)
            {
                Execute($"INSERT INTO categories VALUES({category.Id},{SqlText(category.Name)},{SqlText(category.Icon)}) ON CONFLICT(id) DO UPDATE SET name=excluded.name,icon=excluded.icon");
            }
            foreach (var equipment in data.Equipment)
            {
                Execute($"""
INSERT INTO equipment 
VALUES({equipment.Id},{SqlText(equipment.Name)},{SqlText(equipment.Description)},{equipment.CategoryId},{SqlValue(equipment.PricePerDay)},{equipment.TotalUnits},{equipment.AvailableUnits},{equipment.RepairUnits},{equipment.WrittenOffUnits},{(int)equipment.State},{SqlText(equipment.ImageUrl)},{SqlText(equipment.Tags)}) 
ON CONFLICT(id) 
DO UPDATE SET name=excluded.name,description=excluded.description,category_id=excluded.category_id,price_per_day=excluded.price_per_day,total_units=excluded.total_units,available_units=excluded.available_units,repair_units=excluded.repair_units,written_off_units=excluded.written_off_units,state=excluded.state,image_url=excluded.image_url,tags=excluded.tags
""");
            }
            foreach (var equipment in data.Equipment)
            {
                var units = QueryRows($"SELECT id,state FROM inventory_units WHERE equipment_id={equipment.Id} ORDER BY id");
                if (units.Count > equipment.TotalUnits)
                {
                    throw new InvalidOperationException("Нельзя уменьшить количество экземпляров: используйте списание.");
                }
                for (int index = units.Count; index < equipment.TotalUnits; index++)
                {
                    Execute($"INSERT INTO inventory_units(equipment_id,inventory_number,state) VALUES({equipment.Id},'EQ-{equipment.Id:D4}-'||(SELECT COALESCE(MAX(id),0)+1 FROM inventory_units),'available')");
                    var unitId = ReadScalar("SELECT last_insert_rowid()");
                    Execute($"INSERT INTO inventory_history(unit_id,old_state,new_state,changed_at) VALUES({unitId},NULL,'available',{SqlText(DateTime.UtcNow.ToString("O"))})");
                }
                units = QueryRows($"SELECT id,state FROM inventory_units WHERE equipment_id={equipment.Id} ORDER BY id");
                var targets = Enumerable.Repeat("written_off", equipment.WrittenOffUnits).Concat(Enumerable.Repeat("repair", equipment.RepairUnits)).Concat(Enumerable.Repeat("available", equipment.TotalUnits - equipment.RepairUnits - equipment.WrittenOffUnits)).ToArray();
                for (int index = 0; index < units.Count; index++)
                {
                    if (units[index][1] != targets[index])
                    {
                        Execute($"UPDATE inventory_units SET state={SqlText(targets[index])} WHERE id={ReadInt(units[index][0])}");
                        Execute($"""
INSERT INTO inventory_history(unit_id,old_state,new_state,changed_at) 
VALUES({ReadInt(units[index][0])},{SqlText(units[index][1])},{SqlText(targets[index])},{SqlText(DateTime.UtcNow.ToString("O"))})
""");
                    }
                }
            }
            foreach (var booking in data.Bookings)
            {
                Execute($"""
INSERT INTO bookings 
VALUES({booking.Id},{booking.EquipmentId},{booking.UserId},{SqlText(booking.StartDate.ToString("O"))},{SqlText(booking.EndDate.ToString("O"))},{SqlValue(booking.TotalPrice)},{SqlValue(booking.DailyRate)},{(int)booking.Status},{SqlText(booking.ReturnCondition)},{SqlValue(booking.Fine)},{SqlText(booking.CreatedAt.ToString("O"))}) 
ON CONFLICT(id) 
DO UPDATE SET equipment_id=excluded.equipment_id,user_id=excluded.user_id,start_date=excluded.start_date,end_date=excluded.end_date,total_price=excluded.total_price,daily_rate=excluded.daily_rate,status=excluded.status,return_condition=excluded.return_condition,fine=excluded.fine,created_at=excluded.created_at
""");
            }
            Execute("INSERT INTO schema_version VALUES(1,1) ON CONFLICT(id) DO UPDATE SET version=excluded.version");
            Execute("COMMIT");
        }
        catch
        {
            Execute("ROLLBACK");
            throw;
        }
    }
    IntPtr PrepareStatement(string sql)
    {
        if (sqlite3_prepare_v2(database, sql, -1, out var statement, IntPtr.Zero) != 0)
        {
            throw DatabaseError();
        }
        return statement;
    }
    void Execute(string sql)
    {
        var statement = PrepareStatement(sql);
        try
        {
            if (sqlite3_step(statement) != 101)
            {
                throw DatabaseError();
            }
        }
        finally
        {
            sqlite3_finalize(statement);
        }
    }
    Exception DatabaseError() => new InvalidOperationException(Marshal.PtrToStringUTF8(sqlite3_errmsg(database)) ?? "SQLite error");
    public void Dispose() => sqlite3_close(database);
}

