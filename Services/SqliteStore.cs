using System.Runtime.InteropServices;
using System.Text;
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
 readonly IntPtr db;
 public SqliteStore(string file) { if(sqlite3_open_v2(file,out db,2|4|0x10000,IntPtr.Zero)!=0) throw new Exception("Не удалось открыть SQLite"); Exec("PRAGMA foreign_keys=ON"); Exec("CREATE TABLE IF NOT EXISTS app_state (id INTEGER PRIMARY KEY CHECK(id=1), json TEXT NOT NULL)"); }
 public string? Read(){ var st=Prepare("SELECT json FROM app_state WHERE id=1"); try { int rc=sqlite3_step(st); if(rc==100)return Marshal.PtrToStringUTF8(sqlite3_column_text(st,0)); if(rc==101)return null; throw Error(); } finally{sqlite3_finalize(st);} }
 public void Write(string json){var st=Prepare("INSERT INTO app_state(id,json) VALUES(1,?) ON CONFLICT(id) DO UPDATE SET json=excluded.json"); try{var bytes=Encoding.UTF8.GetBytes(json); if(sqlite3_bind_text(st,1,bytes,bytes.Length,new IntPtr(-1))!=0 || sqlite3_step(st)!=101) throw Error();}finally{sqlite3_finalize(st);} }
 IntPtr Prepare(string sql){if(sqlite3_prepare_v2(db,sql,-1,out var st,IntPtr.Zero)!=0)throw Error();return st;}
 void Exec(string sql){var st=Prepare(sql);try{if(sqlite3_step(st)!=101)throw Error();}finally{sqlite3_finalize(st);}}
 Exception Error()=>new InvalidOperationException(Marshal.PtrToStringUTF8(sqlite3_errmsg(db))??"SQLite error");
 public void Dispose()=>sqlite3_close(db);
}
