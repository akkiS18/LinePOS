using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Data;
using PosElectro.Desktop.Debt;

static class DebtSchemaTests
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Exec(SqliteConnection c, string sql) { using var cmd=c.CreateCommand(); cmd.CommandText=sql;cmd.ExecuteNonQuery(); }
    static long Scalar(SqliteConnection c, string sql) { using var cmd=c.CreateCommand();cmd.CommandText=sql;return Convert.ToInt64(cmd.ExecuteScalar()); }
    public static void Run()
    {
        var dir=Path.Combine(Path.GetTempPath(),"linepos-debt-schema-"+Guid.NewGuid());Directory.CreateDirectory(dir);
        var path=Path.Combine(dir,"database.db");
        try {
            _=new DatabaseContext(path);
            using(var c=new SqliteConnection("Data Source="+path)) {
                c.Open();DebtSchema.Install(c);DebtSchema.Install(c);
                Check(Scalar(c,"PRAGMA foreign_keys")==1,"Debt connection foreign keys disabled");
                Check(Scalar(c,"SELECT version FROM debt_schema")==1,"Missing schema marker");
                Check(Scalar(c,"SELECT COUNT(*) FROM debt_accounts")==0,"Migration invented accounts");
                Exec(c,"INSERT INTO sales(guid,total_amount,total_cost,payment_type,created_at) VALUES('legacy',123,70,3,1)");
                // Recreate the real pre-feature desktop shape without auxiliary debt tables.
                Exec(c,"PRAGMA foreign_keys=OFF");
                foreach(var table in DebtSchema.Tables.Reverse())Exec(c,"DROP TABLE "+table);
            }
            _=new DatabaseContext(path);
            var backups=Directory.GetFiles(Path.Combine(dir,"Backups"),"before-debt-*.db");
            Check(backups.Length==1,"Expected one pre-debt backup");
            using(var backup=new SqliteConnection("Data Source="+backups.Single())) {
                backup.Open();Check(!DebtSchema.IsInstalled(backup),"Backup was taken after migration");
                Check(Scalar(backup,"SELECT total_amount FROM sales WHERE guid='legacy'")==123,"Backup lost old receipt");
            }
            _=new DatabaseContext(path);
            Check(Directory.GetFiles(Path.Combine(dir,"Backups"),"before-debt-*.db").Length==1,"Reopen creates repeated backup");
            using(var c=new SqliteConnection("Data Source="+path)) {
                c.Open();Check(Scalar(c,"SELECT total_amount FROM sales WHERE guid='legacy'")==123,"Migration changed receipt");
                Check(Scalar(c,"SELECT COUNT(*) FROM debt_accounts")==0,"Legacy DEBT receipt became fabricated debt");
                Exec(c,"DROP TABLE debt_schema; CREATE TABLE debt_schema(id INTEGER PRIMARY KEY,version INTEGER); INSERT INTO debt_schema VALUES(1,99)");
                try { DebtSchema.Install(c); throw new Exception("Accepted future version"); } catch(InvalidOperationException) { }
                Check(Scalar(c,"SELECT version FROM debt_schema")==99,"Future version overwritten");
            }
            using(var c=new SqliteConnection("Data Source=:memory:")) {
                c.Open();Exec(c,"CREATE TABLE sales(guid TEXT PRIMARY KEY)");
                using var stream=typeof(DebtSchema).Assembly.GetManifestResourceStream("DebtSchema")!;
                using var reader=new StreamReader(stream);
                try { DebtSchema.Install(c,reader.ReadToEnd()+"\n-- statement\nINVALID SQL");throw new Exception("Expected rollback"); } catch(SqliteException) { }
                Check(Scalar(c,"SELECT COUNT(*) FROM sqlite_master WHERE name GLOB 'debt_*'")==0,"Failed installation left partial tables");
            }
            Console.WriteLine("PASS debt schema: fresh/reopen, legacy upgrade, recovery backup, future-version refusal, atomic DDL rollback");
        } finally { SqliteConnection.ClearAllPools();Directory.Delete(dir,true); }
    }
}
