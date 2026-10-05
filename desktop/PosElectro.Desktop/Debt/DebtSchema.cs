using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace PosElectro.Desktop.Debt;

// Auxiliary SQLite tables, shared byte-for-byte with Android. No customer/debt is
// invented during migration; store binding and business writes belong to repository.
public static class DebtSchema
{
    public static readonly string[] Tables = { "debt_schema", "debt_scope", "debt_customers", "debt_events", "debt_accounts", "debt_event_lines", "debt_command_receipts", "debt_sync_inbox" };

    public static bool IsInstalled(SqliteConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='debt_schema'";
        return Convert.ToInt32(cmd.ExecuteScalar()) != 0;
    }

    public static void Install(SqliteConnection db)
    {
        using var stream = typeof(DebtSchema).Assembly.GetManifestResourceStream("DebtSchema")
            ?? throw new InvalidOperationException("Debt schema resource missing");
        using var reader = new StreamReader(stream);
        Install(db, reader.ReadToEnd());
    }

    public static void Install(SqliteConnection db, string schema)
    {
        using var pragma = db.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON"; pragma.ExecuteNonQuery();
        pragma.CommandText = "PRAGMA foreign_keys";
        if (Convert.ToInt32(pragma.ExecuteScalar()) != 1) throw new InvalidOperationException("Debt ledger requires foreign keys");
        using var tx = db.BeginTransaction();
        using var cmd = db.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name GLOB 'debt_*'";
        var existing = Convert.ToInt32(cmd.ExecuteScalar());
        if (existing != 0)
        {
            if (existing != Tables.Length) throw new InvalidOperationException("Incomplete or unsupported debt schema");
            cmd.CommandText = "SELECT version FROM debt_schema WHERE id=1";
            if (Convert.ToInt32(cmd.ExecuteScalar()) != 1) throw new InvalidOperationException("Unsupported debt schema version");
        }
        foreach (var statement in schema.Split("-- statement", StringSplitOptions.RemoveEmptyEntries))
        {
            cmd.CommandText = statement; cmd.ExecuteNonQuery();
            var definition = Regex.Match(statement, "CREATE (?:UNIQUE )?(?:TABLE|INDEX|TRIGGER) IF NOT EXISTS ([a-z_]+)");
            if (definition.Success)
            {
                cmd.CommandText = "SELECT sql FROM sqlite_master WHERE name=@name";
                cmd.Parameters.AddWithValue("@name", definition.Groups[1].Value);
                var actual = Convert.ToString(cmd.ExecuteScalar()) ?? "";
                cmd.Parameters.Clear();
                static string Normalize(string sql) => Regex.Replace(sql.Replace("IF NOT EXISTS ", ""), @"\s+", " ").Trim().TrimEnd(';');
                if (Normalize(actual) != Normalize(statement.Substring(definition.Index)))
                    throw new InvalidOperationException("Debt schema definition mismatch: " + definition.Groups[1].Value);
            }
        }
        foreach (var table in Tables)
        {
            cmd.CommandText = $"PRAGMA foreign_key_check('{table}')";
            using var violations = cmd.ExecuteReader();
            if (violations.Read()) throw new InvalidOperationException("Debt foreign key integrity failed: " + table);
        }
        cmd.CommandText = "SELECT version FROM debt_schema WHERE id=1";
        if (Convert.ToInt32(cmd.ExecuteScalar()) != 1) throw new InvalidOperationException("Debt schema installation failed");
        tx.Commit();
    }
}
