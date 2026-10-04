using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using PosElectro.Desktop.Models;

namespace PosElectro.Desktop.Data
{
    public class DatabaseContext
    {
        public static bool IsTestEnvironment { get; private set; }

        private readonly string _connectionString;
        public string DatabaseFilePath { get; }

        public DatabaseContext(string? dbPath = null)
        {
            var flagFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_env.flag");
            var envPath = Environment.GetEnvironmentVariable("LINEPOS_DB_PATH");
            var envTest = Environment.GetEnvironmentVariable("LINEPOS_TEST_ENV");

            if (!string.IsNullOrWhiteSpace(dbPath))
            {
                DatabaseFilePath = dbPath;
                IsTestEnvironment = true;
            }
            else if (!string.IsNullOrWhiteSpace(envPath))
            {
                DatabaseFilePath = envPath;
                IsTestEnvironment = true;
            }
            else if (envTest == "1" || File.Exists(flagFile))
            {
                var testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_data");
                Directory.CreateDirectory(testDir);
                DatabaseFilePath = Path.Combine(testDir, "pos_desktop_test.db");
                IsTestEnvironment = true;
            }
            else
            {
                var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PosElectro");
                Directory.CreateDirectory(appDataDir);
                DatabaseFilePath = Path.Combine(appDataDir, "pos_desktop.db");
                IsTestEnvironment = false;
            }

            var baseDir = Path.GetDirectoryName(DatabaseFilePath);
            if (!string.IsNullOrEmpty(baseDir)) Directory.CreateDirectory(baseDir);

            _connectionString = $"Data Source={DatabaseFilePath}";

            if (File.Exists(DatabaseFilePath) && new FileInfo(DatabaseFilePath).Length > 0)
            {
                using var source = new SqliteConnection(_connectionString); source.Open();
                using var check = source.CreateCommand();
                check.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name='sale_items' AND type='table'";
                if (Convert.ToInt32(check.ExecuteScalar()) > 0) {
                    check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('sale_items') WHERE name='guid'";
                    if (Convert.ToInt32(check.ExecuteScalar()) == 0) {
                        var backupDir = Path.Combine(Path.GetDirectoryName(DatabaseFilePath)!, "Backups"); Directory.CreateDirectory(backupDir);
                        using var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(backupDir, $"before-returns-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.db") }.ToString());
                        backup.Open(); source.BackupDatabase(backup);
                    }
                }
            }
            InitializeDatabase();
        }

        public void BackupDatabase(string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "VACUUM INTO $dest";
            cmd.Parameters.AddWithValue("$dest", destinationPath);
            cmd.ExecuteNonQuery();
        }

        public string? AutoBackup(string reason = "sync")
        {
            try
            {
                string backupDir;
                if (IsTestEnvironment)
                {
                    var baseDir = Path.GetDirectoryName(DatabaseFilePath) ?? AppDomain.CurrentDomain.BaseDirectory;
                    backupDir = Path.Combine(baseDir, "Backups");
                }
                else
                {
                    var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PosElectro");
                    backupDir = Path.Combine(appDataDir, "Backups");
                }
                Directory.CreateDirectory(backupDir);

                var fileName = $"LinePOS_AutoBackup_{DateTime.Now:yyyyMMdd_HHmmss}_{reason}.db";
                var destPath = Path.Combine(backupDir, fileName);
                BackupDatabase(destPath);

                // Oxirgi 15 ta avtomatik zaxirani saqlab, qolgan eskilarni tozalash (joyni tejash)
                var oldFiles = Directory.GetFiles(backupDir, "LinePOS_AutoBackup_*.db")
                                       .Select(f => new FileInfo(f))
                                       .OrderByDescending(f => f.CreationTime)
                                       .Skip(15);
                foreach (var oldFile in oldFiles)
                {
                    try { oldFile.Delete(); } catch { }
                }

                return destPath;
            }
            catch
            {
                return null;
            }
        }

        public event Action? ProductsChanged;
        public event Action? WarehousesChanged;
        public event Action<Sale>? LocalSaleCompleted;
        public event Action<Product>? LocalProductSaved;
        public event Action<Warehouse>? LocalWarehouseSaved;
        public event Action<StockTransferEvent>? LocalStockTransferred;
        public event Action<double>? CardTaxRateChanged;

        public void RaiseProductsChanged()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(() => ProductsChanged?.Invoke());
            }
            else
            {
                ProductsChanged?.Invoke();
            }
        }

        public void RaiseWarehousesChanged()
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(() => WarehousesChanged?.Invoke());
            }
            else
            {
                WarehousesChanged?.Invoke();
            }
        }

        public void RaiseLocalSaleCompleted(Sale sale)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => LocalSaleCompleted?.Invoke(sale));
            }
            else
            {
                LocalSaleCompleted?.Invoke(sale);
            }
        }

        public void RaiseLocalProductSaved(Product product)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => LocalProductSaved?.Invoke(product));
            }
            else
            {
                LocalProductSaved?.Invoke(product);
            }
        }

        public void RaiseLocalWarehouseSaved(Warehouse warehouse)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => LocalWarehouseSaved?.Invoke(warehouse));
            }
            else
            {
                LocalWarehouseSaved?.Invoke(warehouse);
            }
        }

        public void RaiseLocalStockTransferred(StockTransferEvent evt)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => LocalStockTransferred?.Invoke(evt));
            }
            else
            {
                LocalStockTransferred?.Invoke(evt);
            }
        }

        public void RaiseCardTaxRateChanged(double rate)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => CardTaxRateChanged?.Invoke(rate));
            }
            else
            {
                CardTaxRateChanged?.Invoke(rate);
            }
        }

        private SqliteConnection CreateConnection()
        {
            var conn = new SqliteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        private void InitializeDatabase()
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;

                CREATE TABLE IF NOT EXISTS products (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    guid TEXT NOT NULL UNIQUE,
                    barcode TEXT UNIQUE,
                    name TEXT NOT NULL,
                    category TEXT NOT NULL DEFAULT 'Barchasi',
                    cost_price REAL NOT NULL,
                    cost_currency TEXT NOT NULL DEFAULT 'UZS',
                    selling_price REAL NOT NULL,
                    selling_price_2 REAL,
                    stock_quantity REAL NOT NULL,
                    unit_type INTEGER NOT NULL,
                    min_stock_alert REAL NOT NULL DEFAULT 3.0,
                    is_deleted INTEGER NOT NULL DEFAULT 0,
                    note TEXT DEFAULT '',
                    updated_at INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS sales (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    guid TEXT NOT NULL UNIQUE,
                    total_amount REAL NOT NULL,
                    total_cost REAL NOT NULL,
                    payment_type INTEGER NOT NULL,
                    cash_amount REAL NOT NULL DEFAULT 0,
                    card_amount REAL NOT NULL DEFAULT 0,
                    tax_amount REAL NOT NULL DEFAULT 0,
                    tax_rate REAL NOT NULL DEFAULT 0,
                    created_at INTEGER NOT NULL,
                    user_id INTEGER NOT NULL DEFAULT 1,
                    is_synced INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS sale_items (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    sale_id INTEGER NOT NULL,
                    sale_guid TEXT NOT NULL,
                    product_id INTEGER NOT NULL,
                    product_guid TEXT NOT NULL,
                    product_name TEXT NOT NULL,
                    quantity REAL NOT NULL,
                    price_at_sale REAL NOT NULL,
                    cost_at_sale REAL NOT NULL,
                    cost_currency TEXT NOT NULL DEFAULT 'UZS',
                    FOREIGN KEY (sale_id) REFERENCES sales(id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS refunds (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    sale_id INTEGER NOT NULL,
                    product_id INTEGER NOT NULL,
                    quantity REAL NOT NULL,
                    refund_amount REAL NOT NULL,
                    refund_date INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS app_settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_products_barcode ON products(barcode);
                CREATE INDEX IF NOT EXISTS idx_products_guid ON products(guid);
                CREATE INDEX IF NOT EXISTS idx_sales_created ON sales(created_at);
                CREATE INDEX IF NOT EXISTS idx_sales_guid ON sales(guid);
                CREATE INDEX IF NOT EXISTS idx_refunds_sale ON refunds(sale_id);
            ";
            cmd.ExecuteNonQuery();

            // Mavjud bazalar uchun 'note' ustunini tekshirib qo'shish
            try
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE products ADD COLUMN note TEXT DEFAULT '';";
                alterCmd.ExecuteNonQuery();
            }
            catch
            {
                // Ustun mavjud bo'lsa xatolik bo'lmaydi
            }

            // Mavjud bazalar uchun 'selling_price_2' ustunini tekshirib qo'shish
            try
            {
                using var alterCmd2 = conn.CreateCommand();
                alterCmd2.CommandText = "ALTER TABLE products ADD COLUMN selling_price_2 REAL NULL;";
                alterCmd2.ExecuteNonQuery();
            }
            catch
            {
                // Ustun mavjud bo'lsa xatolik bo'lmaydi
            }

            // Mavjud bazalar uchun sales jadvaliga cash_amount, card_amount, tax_amount, tax_rate qo'shish
            string[] salesCols = {
                "ALTER TABLE sales ADD COLUMN cash_amount REAL NOT NULL DEFAULT 0;",
                "ALTER TABLE sales ADD COLUMN card_amount REAL NOT NULL DEFAULT 0;",
                "ALTER TABLE sales ADD COLUMN tax_amount REAL NOT NULL DEFAULT 0;",
                "ALTER TABLE sales ADD COLUMN tax_rate REAL NOT NULL DEFAULT 0;"
            };
            foreach (var colSql in salesCols)
            {
                try
                {
                    using var alterSalesCmd = conn.CreateCommand();
                    alterSalesCmd.CommandText = colSql;
                    alterSalesCmd.ExecuteNonQuery();
                }
                catch { }
            }

            foreach (var (table, column, definition) in new[] {
                ("sales", "usd_rate", "REAL NOT NULL DEFAULT 0"),
                ("sale_items", "guid", "TEXT NOT NULL DEFAULT ''"),
                ("sale_items", "category_at_sale", "TEXT NOT NULL DEFAULT ''"),
                ("sale_items", "unit_at_sale", "TEXT NOT NULL DEFAULT ''") })
            {
                using var check = conn.CreateCommand();
                check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name='{column}'";
                if (Convert.ToInt32(check.ExecuteScalar()) == 0) {
                    using var alter = conn.CreateCommand();
                    alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
                    alter.ExecuteNonQuery();
                }
            }
            using (var lineIds = conn.CreateCommand())
            {
                lineIds.CommandText = @"UPDATE sale_items SET guid=(SELECT guid FROM sales WHERE id=sale_items.sale_id)||':'||(SELECT COUNT(*) FROM sale_items previous WHERE previous.sale_id=sale_items.sale_id AND previous.id<=sale_items.id) WHERE guid='';";
                lineIds.ExecuteNonQuery();
            }
            // Omborlar va Ombor qoldiqlari jadvallari
            using (var whCmd = conn.CreateCommand())
            {
                whCmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS warehouses (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        guid TEXT NOT NULL UNIQUE,
                        name TEXT NOT NULL,
                        is_primary INTEGER NOT NULL DEFAULT 0,
                        is_deleted INTEGER NOT NULL DEFAULT 0,
                        updated_at INTEGER NOT NULL
                    );

                    CREATE TABLE IF NOT EXISTS product_stocks (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        product_guid TEXT NOT NULL,
                        warehouse_guid TEXT NOT NULL,
                        quantity REAL NOT NULL DEFAULT 0,
                        updated_at INTEGER NOT NULL,
                        UNIQUE(product_guid, warehouse_guid)
                    );

                    CREATE INDEX IF NOT EXISTS idx_warehouses_guid ON warehouses(guid);
                    CREATE INDEX IF NOT EXISTS idx_pstocks_prod ON product_stocks(product_guid);
                    CREATE INDEX IF NOT EXISTS idx_pstocks_wh ON product_stocks(warehouse_guid);
                ";
                whCmd.ExecuteNonQuery();
            }

            // sale_items jadvaliga warehouse_guid va warehouse_name qo'shish
            string[] saleItemCols = {
                "ALTER TABLE sale_items ADD COLUMN warehouse_guid TEXT DEFAULT '';",
                "ALTER TABLE sale_items ADD COLUMN warehouse_name TEXT DEFAULT '';"
            };
            foreach (var colSql in saleItemCols)
            {
                try
                {
                    using var alterItemCmd = conn.CreateCommand();
                    alterItemCmd.CommandText = colSql;
                    alterItemCmd.ExecuteNonQuery();
                }
                catch { }
            }

            // Standart asosiy ombor mavjudligini tekshirish va kiritish
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using (var checkWhCmd = conn.CreateCommand())
            {
                checkWhCmd.CommandText = "SELECT COUNT(*) FROM warehouses WHERE is_deleted = 0";
                var count = Convert.ToInt32(checkWhCmd.ExecuteScalar());
                if (count == 0)
                {
                    using var seedWhCmd = conn.CreateCommand();
                    seedWhCmd.CommandText = @"
                        INSERT INTO warehouses (guid, name, is_primary, is_deleted, updated_at)
                        VALUES ('main-default-warehouse', 'Do''kondagi ombor', 1, 0, @now);

                        INSERT OR IGNORE INTO product_stocks (product_guid, warehouse_guid, quantity, updated_at)
                        SELECT guid, 'main-default-warehouse', stock_quantity, @now
                        FROM products WHERE is_deleted = 0 AND guid IS NOT NULL AND guid != '';
                    ";
                    seedWhCmd.Parameters.AddWithValue("@now", nowMs);
                    seedWhCmd.ExecuteNonQuery();
                }
            }

            // Invalid / null ombor qoldiqlarini tozalash
            try
            {
                using var cleanWhCmd = conn.CreateCommand();
                cleanWhCmd.CommandText = "DELETE FROM product_stocks WHERE warehouse_guid IS NULL OR warehouse_guid = 'null' OR warehouse_guid = '';";
                cleanWhCmd.ExecuteNonQuery();
            }
            catch { }
        }

        // --- APP SETTINGS OPERATIONS ---

        public double GetCardTaxRate()
        {
            var val = GetSetting("card_tax_rate", "1.8");
            return double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rate) ? rate : 1.8;
        }

        public void SetCardTaxRate(double rate)
        {
            SetSetting("card_tax_rate", rate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            RaiseCardTaxRateChanged(rate);
        }

        public string GetSetting(string key, string defaultValue = "")
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM app_settings WHERE key = @key";
            cmd.Parameters.AddWithValue("@key", key);
            var result = cmd.ExecuteScalar();
            return result != null ? result.ToString()! : defaultValue;
        }

        public void SetSetting(string key, string value)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO app_settings (key, value) VALUES (@key, @value)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            ";
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@value", value);
            cmd.ExecuteNonQuery();
        }

        // --- WAREHOUSE OPERATIONS ---

        public List<Warehouse> GetWarehouses(bool includeDeleted = false)
        {
            var list = new List<Warehouse>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = includeDeleted
                ? "SELECT * FROM warehouses ORDER BY is_primary DESC, name ASC"
                : "SELECT * FROM warehouses WHERE is_deleted = 0 ORDER BY is_primary DESC, name ASC";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Warehouse
                {
                    Id = reader.GetInt64(reader.GetOrdinal("id")),
                    Guid = reader.GetString(reader.GetOrdinal("guid")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    IsPrimary = reader.GetInt32(reader.GetOrdinal("is_primary")) == 1,
                    IsDeleted = reader.GetInt32(reader.GetOrdinal("is_deleted")) == 1,
                    UpdatedAt = reader.GetInt64(reader.GetOrdinal("updated_at"))
                });
            }

            foreach (var wh in list)
            {
                using var statsCmd = conn.CreateCommand();
                statsCmd.CommandText = @"
                    SELECT 
                        COUNT(ps.product_guid) AS prod_count,
                        COALESCE(SUM(ps.quantity), 0) AS total_qty,
                        COALESCE(SUM(ps.quantity * p.selling_price), 0) AS total_val
                    FROM product_stocks ps
                    INNER JOIN products p ON p.guid = ps.product_guid
                    WHERE ps.warehouse_guid = @wh_guid AND p.is_deleted = 0 AND ps.quantity > 0;
                ";
                statsCmd.Parameters.AddWithValue("@wh_guid", wh.Guid);
                using var statReader = statsCmd.ExecuteReader();
                if (statReader.Read())
                {
                    wh.TotalProductCount = Convert.ToInt32(statReader["prod_count"]);
                    wh.TotalStockQuantity = Convert.ToDouble(statReader["total_qty"]);
                    wh.TotalStockValue = Convert.ToDouble(statReader["total_val"]);
                }
            }

            return list;
        }

        public Warehouse? GetPrimaryWarehouse()
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM warehouses WHERE is_primary = 1 AND is_deleted = 0 LIMIT 1";
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new Warehouse
                {
                    Id = reader.GetInt64(reader.GetOrdinal("id")),
                    Guid = reader.GetString(reader.GetOrdinal("guid")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    IsPrimary = true,
                    IsDeleted = false,
                    UpdatedAt = reader.GetInt64(reader.GetOrdinal("updated_at"))
                };
            }
            return null;
        }

        public Warehouse? GetWarehouseByGuid(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid)) return null;
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM warehouses WHERE guid = @guid LIMIT 1";
            cmd.Parameters.AddWithValue("@guid", guid);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new Warehouse
                {
                    Id = reader.GetInt64(reader.GetOrdinal("id")),
                    Guid = reader.GetString(reader.GetOrdinal("guid")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    IsPrimary = reader.GetInt32(reader.GetOrdinal("is_primary")) == 1,
                    IsDeleted = reader.GetInt32(reader.GetOrdinal("is_deleted")) == 1,
                    UpdatedAt = reader.GetInt64(reader.GetOrdinal("updated_at"))
                };
            }
            return null;
        }

        public (string Guid, string Name) GetWarehouseForProduct(string productGuid, string? preferredWarehouseGuid = null)
        {
            var defWh = GetPrimaryWarehouse();
            var defGuid = defWh?.Guid ?? "main-default-warehouse";
            var defName = defWh?.Name ?? "Do'kondagi ombor";

            if (string.IsNullOrWhiteSpace(productGuid))
            {
                return (defGuid, defName);
            }

            using var conn = CreateConnection();

            // 1. Agar afzal ko'rilgan ombor ko'rsatilgan bo'lsa
            if (!string.IsNullOrWhiteSpace(preferredWarehouseGuid))
            {
                using var cmdPref = conn.CreateCommand();
                cmdPref.CommandText = "SELECT guid, name FROM warehouses WHERE guid = @wg AND is_deleted = 0 LIMIT 1";
                cmdPref.Parameters.AddWithValue("@wg", preferredWarehouseGuid);
                using var rPref = cmdPref.ExecuteReader();
                if (rPref.Read())
                {
                    return (rPref.GetString(0), rPref.GetString(1));
                }
            }

            // 2. Ushbu tovar qoldig'i 0 dan katta bo'lgan omborni topish (eng ko'p qoldiq ustuvor)
            using var cmdStock = conn.CreateCommand();
            cmdStock.CommandText = @"
                SELECT w.guid, w.name, ps.quantity 
                FROM product_stocks ps
                JOIN warehouses w ON w.guid = ps.warehouse_guid AND w.is_deleted = 0
                WHERE ps.product_guid = @pg AND ps.quantity > 0
                ORDER BY ps.quantity DESC, w.is_primary DESC
                LIMIT 1;
            ";
            cmdStock.Parameters.AddWithValue("@pg", productGuid);
            using var rStock = cmdStock.ExecuteReader();
            if (rStock.Read())
            {
                return (rStock.GetString(0), rStock.GetString(1));
            }

            // 3. Qoldiq bo'lmasa, bog'langan har qanday ombor
            using var cmdAny = conn.CreateCommand();
            cmdAny.CommandText = @"
                SELECT w.guid, w.name 
                FROM product_stocks ps
                JOIN warehouses w ON w.guid = ps.warehouse_guid AND w.is_deleted = 0
                WHERE ps.product_guid = @pg
                ORDER BY ps.quantity DESC, w.is_primary DESC
                LIMIT 1;
            ";
            cmdAny.Parameters.AddWithValue("@pg", productGuid);
            using var rAny = cmdAny.ExecuteReader();
            if (rAny.Read())
            {
                return (rAny.GetString(0), rAny.GetString(1));
            }

            // 4. Standart asosiy ombor
            return (defGuid, defName);
        }

        public Dictionary<string, string> GetProductWarehouseNamesMap()
        {
            var map = new Dictionary<string, string>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT ps.product_guid, w.name
                FROM product_stocks ps
                JOIN warehouses w ON w.guid = ps.warehouse_guid AND w.is_deleted = 0
                WHERE ps.warehouse_guid IS NOT NULL AND ps.warehouse_guid != 'null' AND ps.warehouse_guid != ''
                ORDER BY ps.quantity DESC, ps.updated_at DESC
            ";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var pg = r.GetString(0);
                if (!map.ContainsKey(pg))
                {
                    map[pg] = r.GetString(1);
                }
            }
            return map;
        }

        public void SaveWarehouse(Warehouse w, bool isFromSync = false)
        {
            using var conn = CreateConnection();
            using var syncTransaction = conn.BeginTransaction();
            if (!isFromSync || w.UpdatedAt <= 0)
            {
                w.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            if (string.IsNullOrWhiteSpace(w.Guid)) w.Guid = Guid.NewGuid().ToString();

            using var cmd = conn.CreateCommand();
            cmd.Transaction = syncTransaction;
            if (w.Id == 0)
            {
                if (w.IsPrimary)
                {
                    using var resetCmd = conn.CreateCommand();
            resetCmd.Transaction = syncTransaction;
                    resetCmd.CommandText = "UPDATE warehouses SET is_primary = 0";
                    resetCmd.ExecuteNonQuery();
                }

                cmd.CommandText = @"
                    INSERT INTO warehouses (guid, name, is_primary, is_deleted, updated_at)
                    VALUES (@guid, @name, @is_primary, @is_deleted, @updated_at);
                    SELECT last_insert_rowid();
                ";
                cmd.Parameters.AddWithValue("@guid", w.Guid);
                cmd.Parameters.AddWithValue("@name", w.Name);
                cmd.Parameters.AddWithValue("@is_primary", w.IsPrimary ? 1 : 0);
                cmd.Parameters.AddWithValue("@is_deleted", w.IsDeleted ? 1 : 0);
                cmd.Parameters.AddWithValue("@updated_at", w.UpdatedAt);
                w.Id = Convert.ToInt64(cmd.ExecuteScalar());
            }
            else
            {
                if (w.IsPrimary)
                {
                    using var resetCmd = conn.CreateCommand();
            resetCmd.Transaction = syncTransaction;
                    resetCmd.CommandText = "UPDATE warehouses SET is_primary = 0 WHERE id != @id";
                    resetCmd.Parameters.AddWithValue("@id", w.Id);
                    resetCmd.ExecuteNonQuery();
                }

                cmd.CommandText = @"
                    UPDATE warehouses SET
                        name = @name,
                        is_primary = @is_primary,
                        is_deleted = @is_deleted,
                        updated_at = @updated_at
                    WHERE id = @id;
                ";
                cmd.Parameters.AddWithValue("@id", w.Id);
                cmd.Parameters.AddWithValue("@name", w.Name);
                cmd.Parameters.AddWithValue("@is_primary", w.IsPrimary ? 1 : 0);
                cmd.Parameters.AddWithValue("@is_deleted", w.IsDeleted ? 1 : 0);
                cmd.Parameters.AddWithValue("@updated_at", w.UpdatedAt);
                cmd.ExecuteNonQuery();
            }

            syncTransaction.Commit();
            RaiseWarehousesChanged();
            if (!isFromSync)
            {
                RaiseLocalWarehouseSaved(w);
            }
        }

        public void SetPrimaryWarehouse(string guid, bool isFromSync = false)
        {
            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();
            using var resetCmd = conn.CreateCommand();
            resetCmd.Transaction = tx;
            resetCmd.CommandText = "UPDATE warehouses SET is_primary = 0";
            resetCmd.ExecuteNonQuery();

            using var setCmd = conn.CreateCommand();
            setCmd.Transaction = tx;
            setCmd.CommandText = "UPDATE warehouses SET is_primary = 1, updated_at = @now WHERE guid = @guid";
            setCmd.Parameters.AddWithValue("@guid", guid);
            setCmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            setCmd.ExecuteNonQuery();

            tx.Commit();
            RaiseWarehousesChanged();
            if (!isFromSync)
            {
                var wh = GetWarehouseByGuid(guid);
                if (wh != null) RaiseLocalWarehouseSaved(wh);
            }
        }

        public void DeleteWarehouse(string guid, bool isFromSync = false)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE warehouses SET is_deleted = 1, updated_at = @now WHERE guid = @guid AND is_primary = 0";
            cmd.Parameters.AddWithValue("@guid", guid);
            cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            cmd.ExecuteNonQuery();
            RaiseWarehousesChanged();
            if (!isFromSync)
            {
                var wh = GetWarehouseByGuid(guid);
                if (wh != null) RaiseLocalWarehouseSaved(wh);
            }
        }

        public void UpsertSyncWarehouse(Warehouse w)
        {
            Warehouse? existing = null;
            if (!string.IsNullOrWhiteSpace(w.Guid))
            {
                existing = GetWarehouseByGuid(w.Guid);
            }

            if (existing == null)
            {
                w.Id = 0;
                if (string.IsNullOrWhiteSpace(w.Guid)) w.Guid = Guid.NewGuid().ToString();
                SaveWarehouse(w, isFromSync: true);
            }
            else if (w.UpdatedAt >= existing.UpdatedAt || w.Name != existing.Name || w.IsDeleted != existing.IsDeleted || w.IsPrimary != existing.IsPrimary)
            {
                w.Id = existing.Id;
                if (string.IsNullOrWhiteSpace(w.Guid)) w.Guid = existing.Guid;
                SaveWarehouse(w, isFromSync: true);
            }
        }

        public List<Warehouse> GetWarehousesChangedSince(long since)
        {
            var list = new List<Warehouse>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, guid, name, is_primary, is_deleted, updated_at FROM warehouses WHERE updated_at > @since ORDER BY updated_at ASC";
            cmd.Parameters.AddWithValue("@since", since);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new Warehouse
                {
                    Id = reader.GetInt64(reader.GetOrdinal("id")),
                    Guid = reader.GetString(reader.GetOrdinal("guid")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    IsPrimary = reader.GetInt32(reader.GetOrdinal("is_primary")) == 1,
                    IsDeleted = reader.GetInt32(reader.GetOrdinal("is_deleted")) == 1,
                    UpdatedAt = reader.GetInt64(reader.GetOrdinal("updated_at"))
                });
            }
            return list;
        }

        public double GetProductStockInWarehouse(string productGuid, string warehouseGuid)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT quantity FROM product_stocks WHERE product_guid = @pg AND warehouse_guid = @wg LIMIT 1";
            cmd.Parameters.AddWithValue("@pg", productGuid);
            cmd.Parameters.AddWithValue("@wg", warehouseGuid);
            var res = cmd.ExecuteScalar();
            return res != null && res != DBNull.Value ? Convert.ToDouble(res) : 0.0;
        }

        public Dictionary<string, double> GetAllProductStocksForWarehouse(string warehouseGuid)
        {
            var dict = new Dictionary<string, double>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT product_guid, quantity FROM product_stocks WHERE warehouse_guid = @wg";
            cmd.Parameters.AddWithValue("@wg", warehouseGuid);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                dict[reader.GetString(0)] = reader.GetDouble(1);
            }
            return dict;
        }

        public List<ProductStock> GetProductStocksChangedSince(long since)
        {
            var list = new List<ProductStock>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, product_guid, warehouse_guid, quantity, updated_at FROM product_stocks WHERE updated_at > @since";
            cmd.Parameters.AddWithValue("@since", since);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ProductStock
                {
                    Id = reader.GetInt64(0),
                    ProductGuid = reader.GetString(1),
                    WarehouseGuid = reader.GetString(2),
                    Quantity = reader.GetDouble(3),
                    UpdatedAt = reader.GetInt64(4)
                });
            }
            return list;
        }

        public List<ProductStock> GetAllProductStocks()
        {
            var list = new List<ProductStock>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, product_guid, warehouse_guid, quantity, updated_at FROM product_stocks";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ProductStock
                {
                    Id = reader.GetInt64(0),
                    ProductGuid = reader.GetString(1),
                    WarehouseGuid = reader.GetString(2),
                    Quantity = reader.GetDouble(3),
                    UpdatedAt = reader.GetInt64(4)
                });
            }
            return list;
        }

        public void SetProductStockInWarehouse(string productGuid, string warehouseGuid, double quantity)
        {
            using var conn = CreateConnection();
            using var syncTransaction = conn.BeginTransaction();
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = syncTransaction;
            cmd.CommandText = @"
                INSERT INTO product_stocks (product_guid, warehouse_guid, quantity, updated_at)
                VALUES (@pg, @wg, @qty, @now)
                ON CONFLICT(product_guid, warehouse_guid) DO UPDATE SET
                    quantity = excluded.quantity,
                    updated_at = excluded.updated_at;

                UPDATE products 
                SET stock_quantity = (SELECT COALESCE(SUM(quantity), 0) FROM product_stocks WHERE product_guid = @pg),
                    updated_at = @now
                WHERE guid = @pg;
            ";
            cmd.Parameters.AddWithValue("@pg", productGuid);
            cmd.Parameters.AddWithValue("@wg", warehouseGuid);
            cmd.Parameters.AddWithValue("@qty", quantity);
            cmd.Parameters.AddWithValue("@now", now);
            cmd.ExecuteNonQuery();
            syncTransaction.Commit();
            RaiseProductsChanged();
        }

        public void TransferStock(string productGuid, string fromWarehouseGuid, string toWarehouseGuid, double quantity, bool isFromSync = false)
        {
            if (quantity <= 0 || fromWarehouseGuid == toWarehouseGuid) return;
            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            using var fromCmd = conn.CreateCommand();
            fromCmd.Transaction = tx;
            fromCmd.CommandText = @"
                INSERT INTO product_stocks (product_guid, warehouse_guid, quantity, updated_at)
                VALUES (@pg, @from_wh, 0, @now)
                ON CONFLICT(product_guid, warehouse_guid) DO NOTHING;

                UPDATE product_stocks
                SET quantity = quantity - @qty, updated_at = @now
                WHERE product_guid = @pg AND warehouse_guid = @from_wh;
            ";
            fromCmd.Parameters.AddWithValue("@pg", productGuid);
            fromCmd.Parameters.AddWithValue("@from_wh", fromWarehouseGuid);
            fromCmd.Parameters.AddWithValue("@qty", quantity);
            fromCmd.Parameters.AddWithValue("@now", now);
            fromCmd.ExecuteNonQuery();

            using var toCmd = conn.CreateCommand();
            toCmd.Transaction = tx;
            toCmd.CommandText = @"
                INSERT INTO product_stocks (product_guid, warehouse_guid, quantity, updated_at)
                VALUES (@pg, @to_wh, @qty, @now)
                ON CONFLICT(product_guid, warehouse_guid) DO UPDATE SET
                    quantity = quantity + excluded.quantity,
                    updated_at = excluded.updated_at;
            ";
            toCmd.Parameters.AddWithValue("@pg", productGuid);
            toCmd.Parameters.AddWithValue("@to_wh", toWarehouseGuid);
            toCmd.Parameters.AddWithValue("@qty", quantity);
            toCmd.Parameters.AddWithValue("@now", now);
            toCmd.ExecuteNonQuery();

            using var updProdCmd = conn.CreateCommand();
            updProdCmd.Transaction = tx;
            updProdCmd.CommandText = @"
                UPDATE products 
                SET stock_quantity = (SELECT COALESCE(SUM(quantity), 0) FROM product_stocks WHERE product_guid = @pg),
                    updated_at = @now
                WHERE guid = @pg;
            ";
            updProdCmd.Parameters.AddWithValue("@pg", productGuid);
            updProdCmd.Parameters.AddWithValue("@now", now);
            updProdCmd.ExecuteNonQuery();

            tx.Commit();
            RaiseProductsChanged();
            RaiseWarehousesChanged();
            if (!isFromSync)
            {
                RaiseLocalStockTransferred(new StockTransferEvent
                {
                    ProductGuid = productGuid,
                    FromWarehouseGuid = fromWarehouseGuid,
                    ToWarehouseGuid = toWarehouseGuid,
                    Quantity = quantity,
                    Timestamp = now
                });
            }
        }

        // --- PRODUCTS OPERATIONS ---

        public List<Product> GetAllProducts(bool includeDeleted = false)
        {
            var list = new List<Product>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = includeDeleted
                ? "SELECT * FROM products ORDER BY name ASC"
                : "SELECT * FROM products WHERE is_deleted = 0 ORDER BY name ASC";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadProduct(reader));
            }
            return list;
        }

        public Product? GetProductByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;
            var clean = barcode.Trim().Replace("\r", "").Replace("\n", "");
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM products WHERE TRIM(barcode) = @barcode COLLATE NOCASE AND is_deleted = 0 LIMIT 1";
            cmd.Parameters.AddWithValue("@barcode", clean);

            using var reader = cmd.ExecuteReader();
            if (reader.Read()) return ReadProduct(reader);

            // Fallback: Agar shtrix-kod ichida bo'lsa yoki qisman mos kelsa
            using var cmdFallback = conn.CreateCommand();
            cmdFallback.CommandText = "SELECT * FROM products WHERE is_deleted = 0 AND TRIM(barcode) LIKE @pattern COLLATE NOCASE LIMIT 1";
            cmdFallback.Parameters.AddWithValue("@pattern", "%" + clean + "%");
            using var reader2 = cmdFallback.ExecuteReader();
            if (reader2.Read()) return ReadProduct(reader2);

            // Fallback 2: Agar skaner kodi tovar shtrix-kodini o'z ichiga olsa (masalan, prefiks yoki nazorat raqami bilan)
            using var cmdFallback2 = conn.CreateCommand();
            cmdFallback2.CommandText = "SELECT * FROM products WHERE is_deleted = 0 AND @clean LIKE '%' || TRIM(barcode) || '%' AND LENGTH(TRIM(barcode)) >= 4 LIMIT 1";
            cmdFallback2.Parameters.AddWithValue("@clean", clean);
            using var reader3 = cmdFallback2.ExecuteReader();
            return reader3.Read() ? ReadProduct(reader3) : null;
        }

        public Product? GetProductByGuid(string guid)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM products WHERE guid = @guid LIMIT 1";
            cmd.Parameters.AddWithValue("@guid", guid);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadProduct(reader) : null;
        }

        /// <summary>
        /// Eng ko'p sotilgan tovarlarni qaytaradi (sale_items jadvaliga asosan).
        /// Agar savdolar yo'q bo'lsa, barcha tovarlarni qaytaradi.
        /// </summary>
        public List<Product> GetTopSellingProducts(int limit = 30)
        {
            var list = new List<Product>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            // sale_items jadvalidan product_id bo'yicha umumiy sotilgan miqdorni hisoblash
            cmd.CommandText = @"
                SELECT p.*, COALESCE(SUM(si.quantity), 0) AS total_sold
                FROM products p
                LEFT JOIN sale_items si ON si.product_id = p.id
                WHERE p.is_deleted = 0 AND p.stock_quantity > 0
                GROUP BY p.id
                ORDER BY total_sold DESC, p.name ASC
                LIMIT @limit
            ";
            cmd.Parameters.AddWithValue("@limit", limit);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadProduct(reader));
            }

            // Agar hech qanday savdo yo'q bo'lsa yoki natija kam bo'lsa, barcha tovarlarni qo'shing
            if (list.Count < 5)
            {
                var all = GetAllProducts(includeDeleted: false);
                foreach (var p in all)
                {
                    if (!list.Any(x => x.Id == p.Id))
                    {
                        list.Add(p);
                        if (list.Count >= limit) break;
                    }
                }
            }
            return list;
        }

        public Product? GetProductByBarcodeAny(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;
            var clean = barcode.Trim().Replace("\r", "").Replace("\n", "");
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM products WHERE TRIM(barcode) = @barcode COLLATE NOCASE LIMIT 1";
            cmd.Parameters.AddWithValue("@barcode", clean);

            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadProduct(reader) : null;
        }

        public void SaveProduct(Product p, bool isFromSync = false)
        {
            using var conn = CreateConnection();
            using var syncTransaction = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = syncTransaction;
            if (!isFromSync || p.UpdatedAt <= 0)
            {
                p.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            // Agar yangi mahsulot qo'shilayotgan bo'lsa (p.Id == 0)
            if (p.Id == 0)
            {
                // 1. Shtrix-kod bo'yicha tekshirish (o'chirilganlar ichidan ham)
                if (!string.IsNullOrWhiteSpace(p.Barcode))
                {
                    var cleanBc = p.Barcode.Trim();
                    using var checkCmd = conn.CreateCommand();
            checkCmd.Transaction = syncTransaction;
                    checkCmd.CommandText = "SELECT id, guid, is_deleted FROM products WHERE TRIM(barcode) = @bc COLLATE NOCASE LIMIT 1";
                    checkCmd.Parameters.AddWithValue("@bc", cleanBc);
                    using var r = checkCmd.ExecuteReader();
                    if (r.Read())
                    {
                        var foundId = r.GetInt64(0);
                        var foundGuid = r.GetString(1);
                        var isDel = r.GetInt32(2) == 1;
                        r.Close();

                        if (isDel)
                        {
                            // O'chirilgan tovar topildi: arxivdan qayta tiklaymiz (is_deleted = false)
                            p.Id = foundId;
                            p.Guid = foundGuid;
                            p.IsDeleted = false;
                        }
                        else
                        {
                            throw new InvalidOperationException($"'{p.Barcode}' shtrix-kodli tovar omborda allaqachon mavjud!");
                        }
                    }
                }
                // 2. Agar shtrix-kodsiz bo'lsa, o'chirilgan tovarlar ichida xuddi shu nomdagi tovar bormi
                else if (!string.IsNullOrWhiteSpace(p.Name))
                {
                    using var checkNameCmd = conn.CreateCommand();
            checkNameCmd.Transaction = syncTransaction;
                    checkNameCmd.CommandText = "SELECT id, guid FROM products WHERE TRIM(name) = @nm COLLATE NOCASE AND is_deleted = 1 LIMIT 1";
                    checkNameCmd.Parameters.AddWithValue("@nm", p.Name.Trim());
                    using var rName = checkNameCmd.ExecuteReader();
                    if (rName.Read())
                    {
                        p.Id = rName.GetInt64(0);
                        p.Guid = rName.GetString(1);
                        p.IsDeleted = false;
                    }
                }
            }

            if (p.Id == 0)
            {
                cmd.CommandText = @"
                    INSERT INTO products (guid, barcode, name, category, cost_price, cost_currency, selling_price, selling_price_2, stock_quantity, unit_type, min_stock_alert, is_deleted, note, updated_at)
                    VALUES (@guid, @barcode, @name, @category, @cost_price, @cost_currency, @selling_price, @selling_price_2, @stock_quantity, @unit_type, @min_stock_alert, @is_deleted, @note, @updated_at);
                    SELECT last_insert_rowid();
                ";
            }
            else
            {
                cmd.CommandText = @"
                    UPDATE products SET
                        barcode = @barcode,
                        name = @name,
                        category = @category,
                        cost_price = @cost_price,
                        cost_currency = @cost_currency,
                        selling_price = @selling_price,
                        selling_price_2 = @selling_price_2,
                        stock_quantity = @stock_quantity,
                        unit_type = @unit_type,
                        min_stock_alert = @min_stock_alert,
                        is_deleted = @is_deleted,
                        note = @note,
                        updated_at = @updated_at
                    WHERE id = @id;
                ";
                cmd.Parameters.AddWithValue("@id", p.Id);
            }

            cmd.Parameters.AddWithValue("@guid", p.Guid);
            cmd.Parameters.AddWithValue("@barcode", (object?)p.Barcode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@name", p.Name);
            cmd.Parameters.AddWithValue("@category", p.Category);
            cmd.Parameters.AddWithValue("@cost_price", p.CostPrice);
            cmd.Parameters.AddWithValue("@cost_currency", p.CostCurrency);
            cmd.Parameters.AddWithValue("@selling_price", p.SellingPrice);
            cmd.Parameters.AddWithValue("@selling_price_2", (object?)p.SellingPrice2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@stock_quantity", p.StockQuantity);
            cmd.Parameters.AddWithValue("@unit_type", (int)p.UnitType);
            cmd.Parameters.AddWithValue("@min_stock_alert", p.MinStockAlert);
            cmd.Parameters.AddWithValue("@is_deleted", p.IsDeleted ? 1 : 0);
            cmd.Parameters.AddWithValue("@note", p.Note ?? string.Empty);
            cmd.Parameters.AddWithValue("@updated_at", p.UpdatedAt);

            if (p.Id == 0)
            {
                p.Id = (long)cmd.ExecuteScalar()!;
            }
            else
            {
                cmd.ExecuteNonQuery();
            }

            // Yangi tovar yoki tahrirlashda tegishli ombor qoldig'ini yangilash
            // Sinxronizatsiyadan kelganda tovar saqlash product_stocks ga tegmaydi (push_stocks va delta o'zi taqsimlaydi)
            if (!isFromSync)
            {
                var targetWhGuid = (!string.IsNullOrWhiteSpace(p.WarehouseGuid) && p.WarehouseGuid != "null")
                    ? p.WarehouseGuid
                    : (GetPrimaryWarehouse()?.Guid ?? "main-default-warehouse");

                using (var stockCmd = conn.CreateCommand())
                {
                    stockCmd.Transaction = syncTransaction;
                    stockCmd.CommandText = @"
                        INSERT INTO product_stocks (product_guid, warehouse_guid, quantity, updated_at)
                        VALUES (@pg, @wg, @qty, @now)
                        ON CONFLICT(product_guid, warehouse_guid) DO UPDATE SET
                            quantity = excluded.quantity,
                            updated_at = excluded.updated_at;
                    ";
                    stockCmd.Parameters.AddWithValue("@pg", p.Guid);
                    stockCmd.Parameters.AddWithValue("@wg", targetWhGuid);
                    stockCmd.Parameters.AddWithValue("@qty", p.StockQuantity);
                    stockCmd.Parameters.AddWithValue("@now", p.UpdatedAt);
                    stockCmd.ExecuteNonQuery();
                }

                // Umumiy tovar qoldig'ini omborlar yig'indisi bo'yicha hisoblash
                using (var sumCmd = conn.CreateCommand())
                {
                    sumCmd.Transaction = syncTransaction;
                    sumCmd.CommandText = @"
                        UPDATE products
                        SET stock_quantity = (SELECT COALESCE(SUM(quantity), 0) FROM product_stocks WHERE product_guid = @pg)
                        WHERE guid = @pg;
                    ";
                    sumCmd.Parameters.AddWithValue("@pg", p.Guid);
                    sumCmd.ExecuteNonQuery();
                }
            }

            syncTransaction.Commit();
            RaiseProductsChanged();
            if (!isFromSync)
            {
                RaiseLocalProductSaved(p);
            }
        }

        public void UpsertSyncProduct(Product p)
        {
            Product? existing = null;
            if (!string.IsNullOrWhiteSpace(p.Guid))
            {
                existing = GetProductByGuid(p.Guid);
            }
            if (existing == null && !string.IsNullOrWhiteSpace(p.Barcode))
            {
                existing = GetProductByBarcodeAny(p.Barcode);
            }

            if (existing == null)
            {
                p.Id = 0;
                if (string.IsNullOrWhiteSpace(p.Guid)) p.Guid = Guid.NewGuid().ToString();
                SaveProduct(p, isFromSync: true);
            }
            else if (p.UpdatedAt >= existing.UpdatedAt)
            {
                p.Id = existing.Id;
                if (string.IsNullOrWhiteSpace(p.Guid)) p.Guid = existing.Guid;
                SaveProduct(p, isFromSync: true);
            }
        }

        public int GetActiveProductsCount()
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM products WHERE is_deleted = 0";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public int GetModifiedProductsCount(long since)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM products WHERE updated_at > @since";
            cmd.Parameters.AddWithValue("@since", since);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public List<Product> GetProductsChangedSince(long since)
        {
            var list = new List<Product>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM products WHERE updated_at > @since ORDER BY updated_at ASC";
            cmd.Parameters.AddWithValue("@since", since);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadProduct(reader));
            }
            return list;
        }

        // --- SALES OPERATIONS ---

        public Sale? GetSaleByGuid(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid)) return null;
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM sales WHERE guid = @guid LIMIT 1";
            cmd.Parameters.AddWithValue("@guid", guid);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadSale(reader) : null;
        }

        public int GetTotalSalesCount()
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sales";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public int GetUnsyncedSalesCount()
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sales WHERE is_synced = 0";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public List<Sale> GetSalesSince(long since)
        {
            var list = new List<Sale>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM sales WHERE created_at > @since OR is_synced = 0 ORDER BY created_at ASC";
            cmd.Parameters.AddWithValue("@since", since);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadSale(reader));
            }
            foreach (var s in list)
            {
                s.Items = GetSaleItems(s.Id, conn);
            }
            return list;
        }

        public void MarkSalesSynced(IEnumerable<string> guids)
        {
            var list = guids.Where(g => !string.IsNullOrWhiteSpace(g)).ToList();
            if (list.Count == 0) return;

            using var conn = CreateConnection();
            using var tx = conn.BeginTransaction();
            foreach (var guid in list)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE sales SET is_synced = 1 WHERE guid = @guid";
                cmd.Parameters.AddWithValue("@guid", guid);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }

        public long InsertSale(Sale sale, bool isFromSync = false, bool deductStock = true)
        {
            if (string.IsNullOrWhiteSpace(sale.Guid))
            {
                sale.Guid = Guid.NewGuid().ToString();
            }

            var existing = GetSaleByGuid(sale.Guid);
            if (existing != null)
            {
                if (!existing.IsSynced && sale.IsSynced)
                {
                    MarkSalesSynced(new[] { sale.Guid });
                }
                return existing.Id;
            }

            using var conn = CreateConnection();
            using var transaction = conn.BeginTransaction();

            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                INSERT INTO sales (usd_rate, guid, total_amount, total_cost, payment_type, cash_amount, card_amount, tax_amount, tax_rate, created_at, user_id, is_synced)
                VALUES (@usd_rate, @guid, @total_amount, @total_cost, @payment_type, @cash_amount, @card_amount, @tax_amount, @tax_rate, @created_at, @user_id, @is_synced);
                SELECT last_insert_rowid();
            ";
            cmd.Parameters.AddWithValue("@guid", sale.Guid);
            cmd.Parameters.AddWithValue("@total_amount", sale.TotalAmount);
            cmd.Parameters.AddWithValue("@total_cost", sale.TotalCost);
            cmd.Parameters.AddWithValue("@usd_rate", sale.UsdRate);
            cmd.Parameters.AddWithValue("@payment_type", (int)sale.PaymentType);
            cmd.Parameters.AddWithValue("@cash_amount", sale.CashAmount);
            cmd.Parameters.AddWithValue("@card_amount", sale.CardAmount);
            cmd.Parameters.AddWithValue("@tax_amount", sale.TaxAmount);
            cmd.Parameters.AddWithValue("@tax_rate", sale.TaxRate);
            cmd.Parameters.AddWithValue("@created_at", sale.CreatedAt);
            cmd.Parameters.AddWithValue("@user_id", sale.UserId);
            cmd.Parameters.AddWithValue("@is_synced", sale.IsSynced ? 1 : 0);

            sale.Id = Convert.ToInt64(cmd.ExecuteScalar());

            // Faol omborlar ro'yxatini olish (Asosiy ombor birinchi o'rinda)
            var activeWarehouses = new List<(string Guid, string Name, bool IsPrimary)>();
            using (var whListCmd = conn.CreateCommand())
            {
                whListCmd.Transaction = transaction;
                whListCmd.CommandText = "SELECT guid, name, is_primary FROM warehouses WHERE is_deleted = 0 ORDER BY is_primary DESC, id ASC";
                using var whReader = whListCmd.ExecuteReader();
                while (whReader.Read())
                {
                    activeWarehouses.Add((whReader.GetString(0), whReader.GetString(1), whReader.GetInt32(2) == 1));
                }
            }
            if (activeWarehouses.Count == 0)
            {
                activeWarehouses.Add(("main-default-warehouse", "Do'kondagi ombor", true));
            }

            foreach (var item in sale.Items)
            {
                long targetProductId = item.ProductId;
                string prodGuid = item.ProductGuid ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(prodGuid))
                {
                    var p = GetProductByGuid(prodGuid);
                    if (p != null)
                    {
                        targetProductId = p.Id;
                        prodGuid = p.Guid;
                    }
                }

                string usedWhGuid = item.WarehouseGuid;
                string usedWhName = item.WarehouseName;

                if (deductStock && !string.IsNullOrWhiteSpace(prodGuid))
                {
                    if (!string.IsNullOrWhiteSpace(usedWhGuid))
                    {
                        // Aniq ko'rsatilgan ombordan yechish
                        using var deductWhCmd = conn.CreateCommand();
                        deductWhCmd.Transaction = transaction;
                        deductWhCmd.CommandText = @"
                            INSERT INTO product_stocks (product_guid, warehouse_guid, quantity, updated_at)
                            VALUES (@pg, @wg, 0, @now)
                            ON CONFLICT(product_guid, warehouse_guid) DO NOTHING;

                            UPDATE product_stocks 
                            SET quantity = quantity - @qty, updated_at = @now
                            WHERE product_guid = @pg AND warehouse_guid = @wg;
                        ";
                        deductWhCmd.Parameters.AddWithValue("@pg", prodGuid);
                        deductWhCmd.Parameters.AddWithValue("@wg", usedWhGuid);
                        deductWhCmd.Parameters.AddWithValue("@qty", item.Quantity);
                        deductWhCmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                        deductWhCmd.ExecuteNonQuery();

                        if (string.IsNullOrWhiteSpace(usedWhName))
                        {
                            var whObj = activeWarehouses.FirstOrDefault(w => w.Guid == usedWhGuid);
                            usedWhName = whObj.Name ?? "";
                        }
                    }
                    else
                    {
                        // Avtomatik Split logikasi (Asosiy ombor birinchi, yetmagani keyingi ombordan)
                        var primaryWh = activeWarehouses.FirstOrDefault(w => w.IsPrimary);
                        if (string.IsNullOrEmpty(primaryWh.Guid)) primaryWh = activeWarehouses[0];

                        double primaryQty = 0;
                        using (var chkCmd = conn.CreateCommand())
                        {
                            chkCmd.Transaction = transaction;
                            chkCmd.CommandText = "SELECT quantity FROM product_stocks WHERE product_guid = @pg AND warehouse_guid = @wg LIMIT 1";
                            chkCmd.Parameters.AddWithValue("@pg", prodGuid);
                            chkCmd.Parameters.AddWithValue("@wg", primaryWh.Guid);
                            var qRes = chkCmd.ExecuteScalar();
                            if (qRes != null && qRes != DBNull.Value) primaryQty = Convert.ToDouble(qRes);
                        }

                        if (primaryQty >= item.Quantity || activeWarehouses.Count <= 1)
                        {
                            // Asosiy omborda yetarli yoki bitta ombor bor
                            usedWhGuid = primaryWh.Guid;
                            usedWhName = primaryWh.Name;

                            using var deductWhCmd = conn.CreateCommand();
                            deductWhCmd.Transaction = transaction;
                            deductWhCmd.CommandText = @"
                                INSERT INTO product_stocks (product_guid, warehouse_guid, quantity, updated_at)
                                VALUES (@pg, @wg, 0, @now)
                                ON CONFLICT(product_guid, warehouse_guid) DO NOTHING;

                                UPDATE product_stocks 
                                SET quantity = quantity - @qty, updated_at = @now
                                WHERE product_guid = @pg AND warehouse_guid = @wg;
                            ";
                            deductWhCmd.Parameters.AddWithValue("@pg", prodGuid);
                            deductWhCmd.Parameters.AddWithValue("@wg", primaryWh.Guid);
                            deductWhCmd.Parameters.AddWithValue("@qty", item.Quantity);
                            deductWhCmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                            deductWhCmd.ExecuteNonQuery();
                        }
                        else
                        {
                            // Variant 1: Avtomatik Split (Asosiy ombordan boricha, qolgani keyingi ombordan)
                            double fromPrimary = Math.Max(0, primaryQty);
                            double fromSecondary = item.Quantity - fromPrimary;
                            var secWh = activeWarehouses.FirstOrDefault(w => !w.IsPrimary);
                            if (string.IsNullOrEmpty(secWh.Guid)) secWh = primaryWh;

                            if (fromPrimary > 0)
                            {
                                using var d1Cmd = conn.CreateCommand();
                                d1Cmd.Transaction = transaction;
                                d1Cmd.CommandText = @"
                                    UPDATE product_stocks 
                                    SET quantity = quantity - @qty, updated_at = @now
                                    WHERE product_guid = @pg AND warehouse_guid = @wg;
                                ";
                                d1Cmd.Parameters.AddWithValue("@pg", prodGuid);
                                d1Cmd.Parameters.AddWithValue("@wg", primaryWh.Guid);
                                d1Cmd.Parameters.AddWithValue("@qty", fromPrimary);
                                d1Cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                                d1Cmd.ExecuteNonQuery();
                            }

                            using var d2Cmd = conn.CreateCommand();
                            d2Cmd.Transaction = transaction;
                            d2Cmd.CommandText = @"
                                INSERT INTO product_stocks (product_guid, warehouse_guid, quantity, updated_at)
                                VALUES (@pg, @wg, 0, @now)
                                ON CONFLICT(product_guid, warehouse_guid) DO NOTHING;

                                UPDATE product_stocks 
                                SET quantity = quantity - @qty, updated_at = @now
                                WHERE product_guid = @pg AND warehouse_guid = @wg;
                            ";
                            d2Cmd.Parameters.AddWithValue("@pg", prodGuid);
                            d2Cmd.Parameters.AddWithValue("@wg", secWh.Guid);
                            d2Cmd.Parameters.AddWithValue("@qty", fromSecondary);
                            d2Cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                            d2Cmd.ExecuteNonQuery();

                            usedWhGuid = primaryWh.Guid;
                            usedWhName = fromPrimary > 0
                                ? $"{primaryWh.Name} ({fromPrimary:0.##}) + {secWh.Name} ({fromSecondary:0.##})"
                                : secWh.Name;
                        }
                    }

                    // Umumiy tovar qoldig'ini yangilash
                    using var updProdStockCmd = conn.CreateCommand();
                    updProdStockCmd.Transaction = transaction;
                    updProdStockCmd.CommandText = @"
                        UPDATE products 
                        SET stock_quantity = (SELECT COALESCE(SUM(quantity), 0) FROM product_stocks WHERE product_guid = @pg),
                            updated_at = @now
                        WHERE guid = @pg;
                    ";
                    updProdStockCmd.Parameters.AddWithValue("@pg", prodGuid);
                    updProdStockCmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    updProdStockCmd.ExecuteNonQuery();
                }

                item.WarehouseGuid = usedWhGuid ?? "";
                item.WarehouseName = usedWhName ?? "";

                using var itemCmd = conn.CreateCommand();
                itemCmd.Transaction = transaction;
                itemCmd.CommandText = @"
                    INSERT INTO sale_items (guid, category_at_sale, unit_at_sale, sale_id, sale_guid, product_id, product_guid, product_name, quantity, price_at_sale, cost_at_sale, cost_currency, warehouse_guid, warehouse_name)
                    VALUES (@item_guid, @category_at_sale, @unit_at_sale, @sale_id, @sale_guid, @product_id, @product_guid, @product_name, @quantity, @price_at_sale, @cost_at_sale, @cost_currency, @wh_guid, @wh_name);
                ";
                if (string.IsNullOrEmpty(item.Guid)) item.Guid = sale.Guid + ":" + (sale.Items.IndexOf(item) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                itemCmd.Parameters.AddWithValue("@item_guid", item.Guid);
                itemCmd.Parameters.AddWithValue("@category_at_sale", item.CategoryAtSale);
                itemCmd.Parameters.AddWithValue("@unit_at_sale", item.UnitAtSale);
                itemCmd.Parameters.AddWithValue("@sale_id", sale.Id);
                itemCmd.Parameters.AddWithValue("@sale_guid", sale.Guid ?? "");
                itemCmd.Parameters.AddWithValue("@product_id", targetProductId);
                itemCmd.Parameters.AddWithValue("@product_guid", item.ProductGuid ?? "");
                itemCmd.Parameters.AddWithValue("@product_name", item.ProductName ?? "");
                itemCmd.Parameters.AddWithValue("@quantity", item.Quantity);
                itemCmd.Parameters.AddWithValue("@price_at_sale", item.PriceAtSale);
                itemCmd.Parameters.AddWithValue("@cost_at_sale", item.CostAtSale);
                itemCmd.Parameters.AddWithValue("@cost_currency", item.CostCurrency ?? "UZS");
                itemCmd.Parameters.AddWithValue("@wh_guid", item.WarehouseGuid);
                itemCmd.Parameters.AddWithValue("@wh_name", item.WarehouseName);
                itemCmd.ExecuteNonQuery();
            }

            transaction.Commit();
            RaiseProductsChanged();
            if (!isFromSync)
            {
                RaiseLocalSaleCompleted(sale);
            }
            return sale.Id;
        }

        public List<string> GetHistoricalCategories()
        {
            using var conn = CreateConnection(); using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT CASE WHEN category_at_sale='' THEN 'Tarixiy kategoriya noma’lum' ELSE category_at_sale END FROM sale_items";
            using var reader = cmd.ExecuteReader(); var result = new List<string>();
            while (reader.Read()) result.Add(reader.GetString(0));
            return result;
        }

        public List<Sale> GetSales(DateTime from, DateTime to)
        {
            var list = new List<Sale>();
            var fromMs = new DateTimeOffset(from).ToUnixTimeMilliseconds();
            var toMs = new DateTimeOffset(to).ToUnixTimeMilliseconds();

            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT * FROM sales 
                WHERE created_at >= @fromMs AND created_at <= @toMs 
                ORDER BY created_at DESC
            ";
            cmd.Parameters.AddWithValue("@fromMs", fromMs);
            cmd.Parameters.AddWithValue("@toMs", toMs);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadSale(reader));
            }

            foreach (var s in list)
            {
                s.Items = GetSaleItems(s.Id, conn);
            }
            return list;
        }

        public List<Sale> SearchSalesByReceiptNumber(string query, int limit = 1000000)
        {
            var list = new List<Sale>();
            if (string.IsNullOrWhiteSpace(query)) return list;
            var clean = query.Trim().TrimStart('#').ToUpperInvariant();
            if (clean.StartsWith("LP-") || clean.StartsWith("RT-")) clean = clean.Substring(3);
            clean = clean.Replace("-", "");

            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT * FROM sales 
                WHERE instr(upper(replace(guid, '-', '')), @pattern) > 0 OR instr(CAST(id AS TEXT), @pattern) > 0
                ORDER BY id DESC 
                LIMIT @limit;
            ";
            cmd.Parameters.AddWithValue("@pattern", clean.ToUpperInvariant());
            cmd.Parameters.AddWithValue("@limit", limit);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadSale(reader));
            }
            reader.Close();
            foreach (var s in list)
            {
                s.Items = GetSaleItems(s.Id, conn);
            }
            return list;
        }

        private List<SaleItem> GetSaleItems(long saleId, SqliteConnection conn)
        {
            var items = new List<SaleItem>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM sale_items WHERE sale_id = @saleId ORDER BY id";
            cmd.Parameters.AddWithValue("@saleId", saleId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                items.Add(new SaleItem
                {
                    Guid = reader.GetString(reader.GetOrdinal("guid")),
                    Id = reader.GetInt64(reader.GetOrdinal("id")),
                    SaleId = reader.GetInt64(reader.GetOrdinal("sale_id")),
                    SaleGuid = reader.GetString(reader.GetOrdinal("sale_guid")),
                    ProductId = reader.GetInt64(reader.GetOrdinal("product_id")),
                    ProductGuid = reader.GetString(reader.GetOrdinal("product_guid")),
                    ProductName = reader.GetString(reader.GetOrdinal("product_name")),
                    CategoryAtSale = reader.GetString(reader.GetOrdinal("category_at_sale")),
                    UnitAtSale = reader.GetString(reader.GetOrdinal("unit_at_sale")),
                    Quantity = reader.GetDouble(reader.GetOrdinal("quantity")),
                    PriceAtSale = reader.GetDouble(reader.GetOrdinal("price_at_sale")),
                    CostAtSale = reader.GetDouble(reader.GetOrdinal("cost_at_sale")),
                    CostCurrency = reader.GetString(reader.GetOrdinal("cost_currency")),
                    WarehouseGuid = reader.IsDBNull(reader.GetOrdinal("warehouse_guid")) ? string.Empty : reader.GetString(reader.GetOrdinal("warehouse_guid")),
                    WarehouseName = reader.IsDBNull(reader.GetOrdinal("warehouse_name")) ? string.Empty : reader.GetString(reader.GetOrdinal("warehouse_name"))
                });
            }
            return items;
        }

        public List<SaleReportItem> GetDetailedReportItems(DateTime from, DateTime to, double usdRate, string? categoryFilter = null, string? warehouseGuidFilter = null)
        {
            return GetSales(from, to).SelectMany(SaleAccounting.Lines).Where(item =>
                (string.IsNullOrWhiteSpace(categoryFilter) || categoryFilter == "Barchasi" || string.Equals(item.Category, categoryFilter, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(warehouseGuidFilter) || warehouseGuidFilter == "all" || item.WarehouseGuid == warehouseGuidFilter)).ToList();
        }

        private static Product ReadProduct(SqliteDataReader r) => new()
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            Guid = r.GetString(r.GetOrdinal("guid")),
            Barcode = r.IsDBNull(r.GetOrdinal("barcode")) ? null : r.GetString(r.GetOrdinal("barcode")),
            Name = r.GetString(r.GetOrdinal("name")),
            Category = r.GetString(r.GetOrdinal("category")),
            CostPrice = r.GetDouble(r.GetOrdinal("cost_price")),
            CostCurrency = r.GetString(r.GetOrdinal("cost_currency")),
            SellingPrice = r.GetDouble(r.GetOrdinal("selling_price")),
            SellingPrice2 = r.IsDBNull(r.GetOrdinal("selling_price_2")) ? null : r.GetDouble(r.GetOrdinal("selling_price_2")),
            StockQuantity = r.GetDouble(r.GetOrdinal("stock_quantity")),
            UnitType = (UnitType)r.GetInt32(r.GetOrdinal("unit_type")),
            MinStockAlert = r.GetDouble(r.GetOrdinal("min_stock_alert")),
            IsDeleted = r.GetInt32(r.GetOrdinal("is_deleted")) == 1,
            Note = r.IsDBNull(r.GetOrdinal("note")) ? string.Empty : r.GetString(r.GetOrdinal("note")),
            UpdatedAt = r.GetInt64(r.GetOrdinal("updated_at"))
        };

        private static Sale ReadSale(SqliteDataReader r)
        {
            var sale = new Sale
            {
                Id = r.GetInt64(r.GetOrdinal("id")),
                Guid = r.GetString(r.GetOrdinal("guid")),
                TotalAmount = r.GetDouble(r.GetOrdinal("total_amount")),
                TotalCost = r.GetDouble(r.GetOrdinal("total_cost")),
                UsdRate = r.GetDouble(r.GetOrdinal("usd_rate")),
                PaymentType = (PaymentType)r.GetInt32(r.GetOrdinal("payment_type")),
                CreatedAt = r.GetInt64(r.GetOrdinal("created_at")),
                UserId = r.GetInt64(r.GetOrdinal("user_id")),
                IsSynced = r.GetInt32(r.GetOrdinal("is_synced")) == 1
            };

            try
            {
                int cIdx = r.GetOrdinal("cash_amount");
                if (cIdx >= 0 && !r.IsDBNull(cIdx)) sale.CashAmount = r.GetDouble(cIdx);
            }
            catch { }

            try
            {
                int kIdx = r.GetOrdinal("card_amount");
                if (kIdx >= 0 && !r.IsDBNull(kIdx)) sale.CardAmount = r.GetDouble(kIdx);
            }
            catch { }

            try
            {
                int tIdx = r.GetOrdinal("tax_amount");
                if (tIdx >= 0 && !r.IsDBNull(tIdx)) sale.TaxAmount = r.GetDouble(tIdx);
            }
            catch { }

            try
            {
                int trIdx = r.GetOrdinal("tax_rate");
                if (trIdx >= 0 && !r.IsDBNull(trIdx)) sale.TaxRate = r.GetDouble(trIdx);
            }
            catch { }

            // Agar eski yozuv bo'lsa va cash/card 0 bo'lsa
            if (sale.CashAmount == 0 && sale.CardAmount == 0 && sale.TotalAmount > 0)
            {
                if (sale.PaymentType == PaymentType.CARD)
                {
                    sale.CardAmount = sale.TotalAmount;
                    if (sale.TaxAmount == 0) sale.TaxAmount = sale.TotalAmount * (sale.TaxRate > 0 ? sale.TaxRate : 1.8) / 100.0;
                }
                else
                {
                    sale.CashAmount = sale.TotalAmount;
                }
            }

            return sale;
        }

        public void RenameCategory(string oldCategory, string newCategory)
        {
            if (string.IsNullOrWhiteSpace(oldCategory) || string.IsNullOrWhiteSpace(newCategory)) return;
            var trimmedOld = oldCategory.Trim();
            var trimmedNew = newCategory.Trim();
            if (string.Equals(trimmedOld, trimmedNew, StringComparison.OrdinalIgnoreCase)) return;

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE products 
                SET category = @newCategory, updated_at = @now 
                WHERE category = @oldCategory;
            ";
            cmd.Parameters.AddWithValue("@newCategory", trimmedNew);
            cmd.Parameters.AddWithValue("@oldCategory", trimmedOld);
            cmd.Parameters.AddWithValue("@now", now);
            cmd.ExecuteNonQuery();

            RaiseProductsChanged();
        }
    }
}
