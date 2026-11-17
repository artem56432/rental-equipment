using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using EquipmentRental.Models;
namespace EquipmentRental.Services;
// Windows includes SQLite as winsqlite3.dll; all values are bound parameters.
public sealed class SqliteStore : IDisposable {
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)] string name,out IntPtr db,int flags,IntPtr vfs);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_close(IntPtr db);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_prepare_v2(IntPtr db,[MarshalAs(UnmanagedType.LPUTF8Str)] string sql,int n,out IntPtr stmt,IntPtr tail);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_bind_text(IntPtr stmt,int index,byte[] value,int n,IntPtr destructor);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_step(IntPtr stmt);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_finalize(IntPtr stmt);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_text(IntPtr stmt,int index);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_errmsg(IntPtr db);
 [DllImport("winsqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_count(IntPtr stmt);
 readonly IntPtr db;
 public SqliteStore(string file) { if(sqlite3_open_v2(file,out db,2|4|0x10000,IntPtr.Zero)!=0) throw new Exception("Не удалось открыть SQLite"); Exec("PRAGMA foreign_keys=ON"); Exec("CREATE TABLE IF NOT EXISTS app_state (id INTEGER PRIMARY KEY CHECK(id=1), json TEXT NOT NULL)"); }
 public string? Read(){ var st=Prepare("SELECT json FROM app_state WHERE id=1"); try { int rc=sqlite3_step(st); if(rc==100)return Marshal.PtrToStringUTF8(sqlite3_column_text(st,0)); if(rc==101)return null; throw Error(); } finally{sqlite3_finalize(st);} }
 public void Write(string json){var st=Prepare("INSERT INTO app_state(id,json) VALUES(1,?) ON CONFLICT(id) DO UPDATE SET json=excluded.json"); try{var bytes=Encoding.UTF8.GetBytes(json); if(sqlite3_bind_text(st,1,bytes,bytes.Length,new IntPtr(-1))!=0 || sqlite3_step(st)!=101) throw Error();}finally{sqlite3_finalize(st);} }
 static string Q(string? s) => s==null ? "NULL" : "'"+s.Replace("'","''")+"'";
 static string V(object? value) => value==null ? "NULL" : Q(Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture));
 public void InitializeSchema() {
  foreach(var sql in new[]{
   "CREATE TABLE IF NOT EXISTS users(id INTEGER PRIMARY KEY,name TEXT NOT NULL,email TEXT NOT NULL UNIQUE,password TEXT NOT NULL,role INTEGER NOT NULL CHECK(role BETWEEN 0 AND 2),created_at TEXT NOT NULL)",
   "CREATE TABLE IF NOT EXISTS categories(id INTEGER PRIMARY KEY,name TEXT NOT NULL,icon TEXT NOT NULL)",
   "CREATE TABLE IF NOT EXISTS equipment(id INTEGER PRIMARY KEY,name TEXT NOT NULL,description TEXT NOT NULL,category_id INTEGER NOT NULL REFERENCES categories(id),price_per_day TEXT NOT NULL,total_units INTEGER NOT NULL CHECK(total_units>=0),available_units INTEGER NOT NULL,repair_units INTEGER NOT NULL CHECK(repair_units>=0),written_off_units INTEGER NOT NULL CHECK(written_off_units>=0),state INTEGER NOT NULL,image_url TEXT NOT NULL,tags TEXT NOT NULL,CHECK(repair_units+written_off_units<=total_units))",
   "CREATE TABLE IF NOT EXISTS bookings(id INTEGER PRIMARY KEY,equipment_id INTEGER NOT NULL REFERENCES equipment(id),user_id INTEGER NOT NULL REFERENCES users(id),start_date TEXT NOT NULL,end_date TEXT NOT NULL,total_price TEXT NOT NULL,daily_rate TEXT NOT NULL,status INTEGER NOT NULL,return_condition TEXT,fine TEXT NOT NULL,created_at TEXT NOT NULL,CHECK(end_date>=start_date))",
   "CREATE TABLE IF NOT EXISTS inventory_units(id INTEGER PRIMARY KEY,equipment_id INTEGER NOT NULL REFERENCES equipment(id),inventory_number TEXT NOT NULL UNIQUE,state TEXT NOT NULL CHECK(state IN ('available','repair','written_off')))",
   "CREATE TABLE IF NOT EXISTS inventory_history(id INTEGER PRIMARY KEY,unit_id INTEGER NOT NULL REFERENCES inventory_units(id),old_state TEXT,new_state TEXT NOT NULL,changed_at TEXT NOT NULL)",
   "CREATE INDEX IF NOT EXISTS idx_bookings_period ON bookings(equipment_id,start_date,end_date)",
   "CREATE TABLE IF NOT EXISTS schema_version(id INTEGER PRIMARY KEY CHECK(id=1),version INTEGER NOT NULL)"}) Exec(sql);
 }
 string? Scalar(string sql){var st=Prepare(sql);try{var rc=sqlite3_step(st);if(rc==100)return Marshal.PtrToStringUTF8(sqlite3_column_text(st,0));if(rc==101)return null;throw Error();}finally{sqlite3_finalize(st);}}
 List<string?[]> Rows(string sql){var list=new List<string?[]>();var st=Prepare(sql);try{int rc;while((rc=sqlite3_step(st))==100){var row=new string?[sqlite3_column_count(st)];for(int i=0;i<row.Length;i++)row[i]=Marshal.PtrToStringUTF8(sqlite3_column_text(st,i));list.Add(row);}if(rc!=101)throw Error();return list;}finally{sqlite3_finalize(st);}}
 static int I(string? s)=>int.Parse(s!,System.Globalization.CultureInfo.InvariantCulture); static decimal D(string? s)=>decimal.Parse(s!,System.Globalization.CultureInfo.InvariantCulture); static DateTime T(string? s)=>DateTime.Parse(s!,null,System.Globalization.DateTimeStyles.RoundtripKind);
 public AppData? ReadNormalized(){ if(Scalar("SELECT version FROM schema_version WHERE id=1") == null) return null; var d=new AppData();
  foreach(var r in Rows("SELECT id,name,email,password,role,created_at FROM users ORDER BY id")) d.Users.Add(new User{Id=I(r[0]),Name=r[1]!,Email=r[2]!,Password=r[3]!,Role=(UserRole)I(r[4]),CreatedAt=T(r[5])});
  foreach(var r in Rows("SELECT id,name,icon FROM categories ORDER BY id")) d.Categories.Add(new Category{Id=I(r[0]),Name=r[1]!,Icon=r[2]!});
  foreach(var r in Rows("SELECT id,name,description,category_id,price_per_day,total_units,available_units,repair_units,written_off_units,state,image_url,tags FROM equipment ORDER BY id")) d.Equipment.Add(new Equipment{Id=I(r[0]),Name=r[1]!,Description=r[2]!,CategoryId=I(r[3]),PricePerDay=D(r[4]),TotalUnits=I(r[5]),AvailableUnits=I(r[6]),RepairUnits=I(r[7]),WrittenOffUnits=I(r[8]),State=(EquipmentState)I(r[9]),ImageUrl=r[10]!,Tags=r[11]!});
  foreach(var r in Rows("SELECT id,equipment_id,user_id,start_date,end_date,total_price,daily_rate,status,return_condition,fine,created_at FROM bookings ORDER BY id")) d.Bookings.Add(new Booking{Id=I(r[0]),EquipmentId=I(r[1]),UserId=I(r[2]),StartDate=T(r[3]),EndDate=T(r[4]),TotalPrice=D(r[5]),DailyRate=D(r[6]),Status=(BookingStatus)I(r[7]),ReturnCondition=r[8],Fine=D(r[9]),CreatedAt=T(r[10])}); return d;
 }
 public List<string?[]> InventoryRows() => Rows("SELECT u.inventory_number,e.name,u.state FROM inventory_units u JOIN equipment e ON e.id=u.equipment_id ORDER BY e.id,u.id");
 public List<string?[]> InventoryHistory() => Rows("SELECT u.inventory_number,h.old_state,h.new_state,h.changed_at FROM inventory_history h JOIN inventory_units u ON u.id=h.unit_id ORDER BY h.id DESC LIMIT 100");
 public void WriteNormalized(AppData d){Exec("BEGIN IMMEDIATE");try{
  foreach(var u in d.Users) Exec($"INSERT INTO users VALUES({u.Id},{Q(u.Name)},{Q(u.Email)},{Q(u.Password)},{(int)u.Role},{Q(u.CreatedAt.ToString("O"))}) ON CONFLICT(id) DO UPDATE SET name=excluded.name,email=excluded.email,password=excluded.password,role=excluded.role,created_at=excluded.created_at");
  foreach(var c in d.Categories) Exec($"INSERT INTO categories VALUES({c.Id},{Q(c.Name)},{Q(c.Icon)}) ON CONFLICT(id) DO UPDATE SET name=excluded.name,icon=excluded.icon");
  foreach(var e in d.Equipment) Exec($"INSERT INTO equipment VALUES({e.Id},{Q(e.Name)},{Q(e.Description)},{e.CategoryId},{V(e.PricePerDay)},{e.TotalUnits},{e.AvailableUnits},{e.RepairUnits},{e.WrittenOffUnits},{(int)e.State},{Q(e.ImageUrl)},{Q(e.Tags)}) ON CONFLICT(id) DO UPDATE SET name=excluded.name,description=excluded.description,category_id=excluded.category_id,price_per_day=excluded.price_per_day,total_units=excluded.total_units,available_units=excluded.available_units,repair_units=excluded.repair_units,written_off_units=excluded.written_off_units,state=excluded.state,image_url=excluded.image_url,tags=excluded.tags");
  foreach(var e in d.Equipment) {
   var units=Rows($"SELECT id,state FROM inventory_units WHERE equipment_id={e.Id} ORDER BY id");
   if(units.Count>e.TotalUnits) throw new InvalidOperationException("Нельзя уменьшить количество экземпляров: используйте списание.");
   for(int i=units.Count;i<e.TotalUnits;i++){Exec($"INSERT INTO inventory_units(equipment_id,inventory_number,state) VALUES({e.Id},'EQ-{e.Id:D4}-'||(SELECT COALESCE(MAX(id),0)+1 FROM inventory_units),'available')"); var uid=Scalar("SELECT last_insert_rowid()"); Exec($"INSERT INTO inventory_history(unit_id,old_state,new_state,changed_at) VALUES({uid},NULL,'available',{Q(DateTime.UtcNow.ToString("O"))})");}
   units=Rows($"SELECT id,state FROM inventory_units WHERE equipment_id={e.Id} ORDER BY id");
   var targets=Enumerable.Repeat("written_off",e.WrittenOffUnits).Concat(Enumerable.Repeat("repair",e.RepairUnits)).Concat(Enumerable.Repeat("available",e.TotalUnits-e.RepairUnits-e.WrittenOffUnits)).ToArray();
   for(int i=0;i<units.Count;i++){if(units[i][1]!=targets[i]){Exec($"UPDATE inventory_units SET state={Q(targets[i])} WHERE id={I(units[i][0])}");Exec($"INSERT INTO inventory_history(unit_id,old_state,new_state,changed_at) VALUES({I(units[i][0])},{Q(units[i][1])},{Q(targets[i])},{Q(DateTime.UtcNow.ToString("O"))})");}}
  }
  foreach(var b in d.Bookings) Exec($"INSERT INTO bookings VALUES({b.Id},{b.EquipmentId},{b.UserId},{Q(b.StartDate.ToString("O"))},{Q(b.EndDate.ToString("O"))},{V(b.TotalPrice)},{V(b.DailyRate)},{(int)b.Status},{Q(b.ReturnCondition)},{V(b.Fine)},{Q(b.CreatedAt.ToString("O"))}) ON CONFLICT(id) DO UPDATE SET equipment_id=excluded.equipment_id,user_id=excluded.user_id,start_date=excluded.start_date,end_date=excluded.end_date,total_price=excluded.total_price,daily_rate=excluded.daily_rate,status=excluded.status,return_condition=excluded.return_condition,fine=excluded.fine,created_at=excluded.created_at");
  Exec("INSERT INTO schema_version VALUES(1,1) ON CONFLICT(id) DO UPDATE SET version=excluded.version");Exec("COMMIT");
 }catch{Exec("ROLLBACK");throw;}}
 IntPtr Prepare(string sql){if(sqlite3_prepare_v2(db,sql,-1,out var st,IntPtr.Zero)!=0)throw Error();return st;}
 void Exec(string sql){var st=Prepare(sql);try{if(sqlite3_step(st)!=101)throw Error();}finally{sqlite3_finalize(st);}}
 Exception Error()=>new InvalidOperationException(Marshal.PtrToStringUTF8(sqlite3_errmsg(db))??"SQLite error");
 public void Dispose()=>sqlite3_close(db);
}


