package uz.pos.electro.data.local

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase
import androidx.room.TypeConverters
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase
import kotlinx.coroutines.CoroutineScope
import uz.pos.electro.data.local.converter.Converters
import uz.pos.electro.data.local.dao.ProductDao
import uz.pos.electro.data.local.dao.ProductStockDao
import uz.pos.electro.data.local.dao.RefundDao
import uz.pos.electro.data.local.dao.SaleDao
import uz.pos.electro.data.local.dao.UserDao
import uz.pos.electro.data.local.dao.WarehouseDao
import uz.pos.electro.data.local.entity.ProductEntity
import uz.pos.electro.data.local.entity.ProductStockEntity
import uz.pos.electro.data.local.entity.RefundEntity
import uz.pos.electro.data.local.entity.SaleEntity
import uz.pos.electro.data.local.entity.SaleItemEntity
import uz.pos.electro.data.local.entity.UserEntity
import uz.pos.electro.data.local.entity.WarehouseEntity

@Database(
    entities = [
        UserEntity::class,
        ProductEntity::class,
        SaleEntity::class,
        SaleItemEntity::class,
        RefundEntity::class,
        WarehouseEntity::class,
        ProductStockEntity::class
    ],
    version = 13,
    exportSchema = false
)
@TypeConverters(Converters::class)
abstract class AppDatabase : RoomDatabase() {

    abstract fun userDao(): UserDao
    abstract fun productDao(): ProductDao
    abstract fun saleDao(): SaleDao
    abstract fun refundDao(): RefundDao
    abstract fun warehouseDao(): WarehouseDao
    abstract fun productStockDao(): ProductStockDao

    companion object {
        const val DATABASE_NAME = "electro_pos.db"

        val MIGRATION_3_4 = object : Migration(3, 4) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE products ADD COLUMN cost_currency TEXT NOT NULL DEFAULT 'UZS'")
                db.execSQL("ALTER TABLE sale_items ADD COLUMN cost_currency TEXT NOT NULL DEFAULT 'UZS'")
            }
        }

        val MIGRATION_4_5 = object : Migration(4, 5) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE products ADD COLUMN category TEXT NOT NULL DEFAULT 'Barchasi'")
            }
        }

        val MIGRATION_5_6 = object : Migration(5, 6) {
            override fun migrate(db: SupportSQLiteDatabase) {
                // 1. Products jadvali yangilanishi
                db.execSQL("ALTER TABLE products ADD COLUMN guid TEXT NOT NULL DEFAULT ''")
                db.execSQL("ALTER TABLE products ADD COLUMN note TEXT NOT NULL DEFAULT ''")
                db.execSQL("ALTER TABLE products ADD COLUMN updated_at INTEGER NOT NULL DEFAULT 0")
                db.execSQL("UPDATE products SET guid = 'mob_prod_' || id || '_' || lower(hex(randomblob(4))) WHERE guid = ''")
                db.execSQL("UPDATE products SET updated_at = (strftime('%s', 'now') * 1000) WHERE updated_at = 0")
                db.execSQL("CREATE UNIQUE INDEX IF NOT EXISTS index_products_guid ON products(guid)")

                // 2. Sales jadvali yangilanishi
                db.execSQL("ALTER TABLE sales ADD COLUMN guid TEXT NOT NULL DEFAULT ''")
                db.execSQL("ALTER TABLE sales ADD COLUMN is_synced INTEGER NOT NULL DEFAULT 0")
                db.execSQL("UPDATE sales SET guid = 'mob_sale_' || id || '_' || lower(hex(randomblob(4))) WHERE guid = ''")
                db.execSQL("CREATE UNIQUE INDEX IF NOT EXISTS index_sales_guid ON sales(guid)")

                // 3. Sale Items jadvali yangilanishi
                db.execSQL("ALTER TABLE sale_items ADD COLUMN sale_guid TEXT NOT NULL DEFAULT ''")
                db.execSQL("ALTER TABLE sale_items ADD COLUMN product_guid TEXT NOT NULL DEFAULT ''")
                db.execSQL("ALTER TABLE sale_items ADD COLUMN product_name TEXT NOT NULL DEFAULT ''")
                db.execSQL("UPDATE sale_items SET sale_guid = COALESCE((SELECT guid FROM sales WHERE sales.id = sale_items.sale_id), '')")
                db.execSQL("UPDATE sale_items SET product_guid = COALESCE((SELECT guid FROM products WHERE products.id = sale_items.product_id), '')")
                db.execSQL("UPDATE sale_items SET product_name = COALESCE((SELECT name FROM products WHERE products.id = sale_items.product_id), '')")
                db.execSQL("CREATE INDEX IF NOT EXISTS index_sale_items_sale_guid ON sale_items(sale_guid)")
                db.execSQL("CREATE INDEX IF NOT EXISTS index_sale_items_product_guid ON sale_items(product_guid)")
            }
        }

        val MIGRATION_6_7 = object : Migration(6, 7) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE products ADD COLUMN selling_price_2 REAL DEFAULT NULL")
            }
        }

        val MIGRATION_7_8 = object : Migration(7, 8) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE sales ADD COLUMN cash_amount REAL NOT NULL DEFAULT 0.0")
                db.execSQL("ALTER TABLE sales ADD COLUMN card_amount REAL NOT NULL DEFAULT 0.0")
                db.execSQL("ALTER TABLE sales ADD COLUMN tax_amount REAL NOT NULL DEFAULT 0.0")
                db.execSQL("ALTER TABLE sales ADD COLUMN tax_rate REAL NOT NULL DEFAULT 0.0")
            }
        }

        private fun ensureMultiWarehouseTables(db: SupportSQLiteDatabase) {
            try {
                // 1. Warehouses jadvali
                db.execSQL("""
                    CREATE TABLE IF NOT EXISTS warehouses (
                        id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                        guid TEXT NOT NULL,
                        name TEXT NOT NULL,
                        is_primary INTEGER NOT NULL DEFAULT 0,
                        is_deleted INTEGER NOT NULL DEFAULT 0,
                        updated_at INTEGER NOT NULL DEFAULT 0
                    )
                """.trimIndent())
                db.execSQL("CREATE UNIQUE INDEX IF NOT EXISTS index_warehouses_guid ON warehouses(guid)")

                // 2. Product Stocks jadvali
                db.execSQL("""
                    CREATE TABLE IF NOT EXISTS product_stocks (
                        id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                        product_guid TEXT NOT NULL,
                        warehouse_guid TEXT NOT NULL,
                        quantity REAL NOT NULL DEFAULT 0.0,
                        updated_at INTEGER NOT NULL DEFAULT 0
                    )
                """.trimIndent())
                db.execSQL("CREATE UNIQUE INDEX IF NOT EXISTS index_product_stocks_product_guid_warehouse_guid ON product_stocks(product_guid, warehouse_guid)")
                db.execSQL("CREATE INDEX IF NOT EXISTS index_product_stocks_product_guid ON product_stocks(product_guid)")
                db.execSQL("CREATE INDEX IF NOT EXISTS index_product_stocks_warehouse_guid ON product_stocks(warehouse_guid)")

                // 3. Sale Items jadvaliga ombor maydonlari
                try {
                    db.execSQL("ALTER TABLE sale_items ADD COLUMN warehouse_guid TEXT NOT NULL DEFAULT ''")
                } catch (_: Throwable) {}
                try {
                    db.execSQL("ALTER TABLE sale_items ADD COLUMN warehouse_name TEXT NOT NULL DEFAULT ''")
                } catch (_: Throwable) {}

                // 4. Standart asosiy ombor va dastlabki qoldiqlar
                val now = System.currentTimeMillis()
                try {
                    db.execSQL("INSERT OR IGNORE INTO warehouses (guid, name, is_primary, is_deleted, updated_at) VALUES ('main-default-warehouse', 'Do''kondagi ombor', 1, 0, $now)")
                } catch (_: Throwable) {}
                try {
                    db.execSQL("INSERT OR IGNORE INTO product_stocks (product_guid, warehouse_guid, quantity, updated_at) SELECT guid, 'main-default-warehouse', COALESCE(stock_quantity, 0.0), $now FROM products WHERE is_deleted = 0 AND guid IS NOT NULL AND guid != ''")
                } catch (_: Throwable) {}
            } catch (e: Throwable) {
                android.util.Log.e("AppDatabase", "ensureMultiWarehouseTables error: ${e.message}")
            }
        }

        val MIGRATION_8_9 = object : Migration(8, 9) {
            override fun migrate(db: SupportSQLiteDatabase) {
                ensureMultiWarehouseTables(db)
            }
        }

        val MIGRATION_9_10 = object : Migration(9, 10) {
            override fun migrate(db: SupportSQLiteDatabase) {
                ensureMultiWarehouseTables(db)
            }
        }

        val MIGRATION_8_10 = object : Migration(8, 10) {
            override fun migrate(db: SupportSQLiteDatabase) {
                ensureMultiWarehouseTables(db)
            }
        }

        fun databaseName(context: Context): String =
            if (context.packageName.endsWith(".test")) "electro_pos_test.db" else DATABASE_NAME

        fun buildDatabase(context: Context, scope: CoroutineScope, nameOverride: String? = null): AppDatabase {
            val dbName = nameOverride ?: databaseName(context)
            return Room.databaseBuilder(
                context.applicationContext,
                AppDatabase::class.java,
                dbName
            )
            .addMigrations(MIGRATION_3_4, MIGRATION_4_5, MIGRATION_5_6, MIGRATION_6_7, MIGRATION_7_8, MIGRATION_8_9, MIGRATION_9_10, MIGRATION_8_10)
            .addMigrations(object : Migration(10, 11) {
                override fun migrate(db: SupportSQLiteDatabase) {
                    uz.pos.electro.data.sync.WifiSyncSchema.install(db, context.assets.open("wifi-sync-schema.sql").bufferedReader().use { it.readText() })
                }
            })
            .addMigrations(object : Migration(11, 12) {
                override fun migrate(db: SupportSQLiteDatabase) {
                    db.execSQL("ALTER TABLE sales ADD COLUMN usd_rate REAL NOT NULL DEFAULT 0.0")
                    db.execSQL("ALTER TABLE sale_items ADD COLUMN category_at_sale TEXT NOT NULL DEFAULT ''")
                    db.execSQL("ALTER TABLE sale_items ADD COLUMN unit_at_sale TEXT NOT NULL DEFAULT ''")
                }
            })
            .addMigrations(object : Migration(12, 13) {
                override fun migrate(db: SupportSQLiteDatabase) {
                    val backupDir = java.io.File(context.filesDir, "migration-backups").apply { mkdirs() }
                    uz.pos.electro.util.DatabaseBackupExporter.copySnapshot(db,
                        java.io.File(backupDir, "before-returns-${System.currentTimeMillis()}-${java.util.UUID.randomUUID()}.db"))
                    db.execSQL("ALTER TABLE sale_items ADD COLUMN guid TEXT NOT NULL DEFAULT ''")
                    db.execSQL("UPDATE sale_items SET guid=(SELECT guid FROM sales WHERE id=sale_items.sale_id)||':'||(SELECT COUNT(*) FROM sale_items previous WHERE previous.sale_id=sale_items.sale_id AND previous.id<=sale_items.id) WHERE guid=''")
                }
            })
            .addCallback(object : Callback() {
                override fun onOpen(db: SupportSQLiteDatabase) {
                    super.onOpen(db)
                    uz.pos.electro.data.sync.WifiSyncSchema.install(db, context.assets.open("wifi-sync-schema.sql").bufferedReader().use { it.readText() })
                }
                private fun seedDefaults(db: SupportSQLiteDatabase) {
                    try {
                        val now = System.currentTimeMillis()
                        // Standart Admin foydalanuvchisini bazaga yozish (Foreign key uchun)
                        db.execSQL("INSERT OR IGNORE INTO users (id, name, pin_code, role) VALUES (1, 'Admin', '0000', 'ADMIN')")
                        // Standart asosiy ombor
                        db.execSQL("INSERT OR IGNORE INTO warehouses (guid, name, is_primary, is_deleted, updated_at) VALUES ('main-default-warehouse', 'Do''kondagi ombor', 1, 0, $now)")
                        // Yangi bazada mavjud tovarlar bo'lsa qoldiq bog'lash
                        db.execSQL("INSERT OR IGNORE INTO product_stocks (product_guid, warehouse_guid, quantity, updated_at) SELECT guid, 'main-default-warehouse', COALESCE(stock_quantity, 0.0), $now FROM products WHERE is_deleted = 0 AND guid IS NOT NULL AND guid != ''")
                    } catch (e: Throwable) {
                        android.util.Log.e("AppDatabase", "seedDefaults error in onCreate: ${e.message}")
                    }
                }

                override fun onCreate(db: SupportSQLiteDatabase) {
                    super.onCreate(db)
                    seedDefaults(db)
                }
            })
            .build()
        }
    }
}
