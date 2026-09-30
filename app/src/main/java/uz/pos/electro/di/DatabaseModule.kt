package uz.pos.electro.di

import android.content.Context
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import uz.pos.electro.data.local.AppDatabase
import uz.pos.electro.data.local.dao.ProductDao
import uz.pos.electro.data.local.dao.RefundDao
import uz.pos.electro.data.local.dao.SaleDao
import uz.pos.electro.data.local.dao.UserDao
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object DatabaseModule {

    @Provides
    @Singleton
    fun provideCoroutineScope(): CoroutineScope {
        return CoroutineScope(SupervisorJob() + Dispatchers.IO)
    }

    @Provides
    @Singleton
    fun provideAppDatabase(
        @ApplicationContext context: Context,
        scope: CoroutineScope
    ): AppDatabase {
        return AppDatabase.buildDatabase(context, scope)
    }

    @Provides
    @Singleton
    fun provideUserDao(database: AppDatabase): UserDao {
        return database.userDao()
    }

    @Provides
    @Singleton
    fun provideProductDao(database: AppDatabase): ProductDao {
        return database.productDao()
    }

    @Provides
    @Singleton
    fun provideSaleDao(database: AppDatabase): SaleDao {
        return database.saleDao()
    }

    @Provides
    @Singleton
    fun provideRefundDao(database: AppDatabase): RefundDao {
        return database.refundDao()
    }

    @Provides
    @Singleton
    fun provideWarehouseDao(database: AppDatabase): uz.pos.electro.data.local.dao.WarehouseDao {
        return database.warehouseDao()
    }

    @Provides
    @Singleton
    fun provideProductStockDao(database: AppDatabase): uz.pos.electro.data.local.dao.ProductStockDao {
        return database.productStockDao()
    }
}
