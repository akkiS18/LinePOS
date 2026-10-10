package uz.pos.electro.data.debt

import android.content.Context
import androidx.sqlite.db.SupportSQLiteDatabase
import dagger.hilt.android.qualifiers.ApplicationContext
import uz.pos.electro.data.local.AppDatabase
import java.math.BigDecimal
import java.math.BigInteger
import java.math.RoundingMode
import java.time.LocalDate
import java.util.Collections
import java.util.UUID
import javax.inject.Inject
import javax.inject.Singleton

enum class DebtFilter {
    ALL,
    ACTIVE_DEBT,
    OVERDUE,
    SETTLED,
    CREDIT
}

data class DebtSummaryDto(
    val totalActiveDebtMinor: Long = 0L,
    val debtorsCount: Int = 0,
    val overdueDebtMinor: Long = 0L,
    val overdueDebtorsCount: Int = 0,
    val totalPaidMinor: Long = 0L,
    val totalCreditMinor: Long = 0L,
    val creditCustomersCount: Int = 0
) {
    val totalActiveDebtUz: Double get() = totalActiveDebtMinor / 100.0
    val overdueDebtUz: Double get() = overdueDebtMinor / 100.0
    val totalPaidUz: Double get() = totalPaidMinor / 100.0
    val totalCreditUz: Double get() = totalCreditMinor / 100.0
}

data class DebtCustomerItemDto(
    val guid: String,
    val name: String,
    val phone: String,
    val note: String,
    val archived: Boolean,
    val revision: Long,
    val createdAt: Long,
    val balanceMinor: Long,
    val originalDebtMinor: Long,
    val totalPaidMinor: Long,
    val hasOverdue: Boolean,
    val earliestDueDate: String?,
    val activeAccountsCount: Int
) {
    val customerGuid: String get() = guid
    val fullName: String get() = name
    val isArchived: Boolean get() = archived
    val isOverdue: Boolean get() = hasOverdue
    val balanceUz: Double get() = balanceMinor / 100.0
    val phoneDisplay: String get() = if (phone.isBlank()) "Telefon kiritilmagan" else phone
}

data class DebtAccountItemDto(
    val accountGuid: String,
    val saleGuid: String,
    val customerNameAtSale: String,
    val originalDebtMinor: Long,
    val balanceMinor: Long,
    val totalPaidMinor: Long,
    val dueDate: String?,
    val isOverdue: Boolean,
    val createdAt: Long,
    val receiptNumber: String
) {
    val originalDebtUz: Double get() = originalDebtMinor / 100.0
    val balanceUz: Double get() = balanceMinor / 100.0
    val totalPaidUz: Double get() = totalPaidMinor / 100.0
}

data class DebtEventItemDto(
    val eventGuid: String,
    val requestGuid: String,
    val kind: String,
    val occurredAt: Long,
    val cashMinor: Long,
    val cardMinor: Long,
    val lines: List<DebtEventLineDto>
) {
    val kindDisplay: String get() = when (kind) {
        "sale_open" -> "Nasiya savdo"
        "payment" -> "Qarz to'lovi"
        "return_offset" -> "Qaytarish chegirildi"
        "payment_reversal" -> "To'lov bekor qilindi"
        else -> kind
    }
    val totalPaymentUz: Double get() = (cashMinor + cardMinor) / 100.0
}

data class DebtEventLineDto(
    val accountGuid: String,
    val debtDeltaMinor: Long
) {
    val deltaUz: Double get() = debtDeltaMinor / 100.0
}

data class DebtCustomerDetailDto(
    val customer: DebtCustomerRecord,
    val balanceMinor: Long,
    val accounts: List<DebtAccountItemDto>,
    val events: List<DebtEventItemDto>
)

data class DebtPaymentAllocationPreview(
    val totalPaymentMinor: Long,
    val remainingBalanceMinor: Long,
    val lines: List<DebtAllocationLinePreview>
)

data class DebtAllocationLinePreview(
    val accountGuid: String,
    val receiptNumber: String,
    val previousBalanceMinor: Long,
    val paidMinor: Long,
    val newBalanceMinor: Long
)

data class DebtCartItemDto(
    val productGuid: String,
    val productName: String,
    val category: String,
    val unitDisplay: String,
    val warehouseGuid: String,
    val warehouseName: String,
    val quantity: Double,
    val priceAtSale: Double,
    val costPrice: Double,
    val costCurrency: String
)

@Singleton
class DebtService @Inject constructor(
    private val database: AppDatabase,
    @ApplicationContext private val context: Context
) {
    private val lock = Any()
    private var repository: DebtRepository? = null
    private var storeGuid: String = ""
    private var actorGuid: String = ""

    companion object {
        const val CANONICAL_DEFAULT_WAREHOUSE_GUID = "00000000-0000-0000-0000-000000000001"

        fun formatDecimal(value: BigDecimal): String {
            val stripped = value.stripTrailingZeros()
            val plain = stripped.toPlainString()
            return if (plain == "-0" || plain.isEmpty()) "0" else plain
        }

        private fun decimalUnits(s: String): BigInteger {
            require(s.matches(Regex("(?:0|[1-9][0-9]{0,11})(?:\\.[0-9]{0,7}[1-9])?")))
            val scale = BigInteger.TEN.pow(8)
            val p = s.split('.')
            return p[0].toBigInteger() * scale + if (p.size == 1) BigInteger.ZERO else p[1].padEnd(8, '0').toBigInteger()
        }

        private fun rounded(value: BigInteger, divisor: BigInteger): Long {
            val n = (value + divisor / BigInteger.valueOf(2)) / divisor
            require(n.signum() >= 0 && n <= BigInteger.valueOf(Long.MAX_VALUE))
            return n.toLong()
        }

        @JvmName("buildSaleSnapshotFromCart")
        fun buildSaleSnapshot(
            saleGuid: String,
            occurredAt: Long,
            cartItems: List<uz.pos.electro.data.model.CartItemModel>,
            cashMinor: Long,
            cardMinor: Long,
            usdRate: Double,
            cardTaxRate: Double
        ): DebtSaleSnapshot {
            val dtoList = cartItems.map { item ->
                DebtCartItemDto(
                    productGuid = item.product.guid,
                    productName = item.product.name,
                    category = item.product.category,
                    unitDisplay = item.product.unitType.name,
                    warehouseGuid = item.warehouseGuid ?: "",
                    warehouseName = item.warehouseName ?: "",
                    quantity = item.quantity,
                    priceAtSale = item.priceAtSale,
                    costPrice = item.product.costPrice,
                    costCurrency = item.product.costCurrency
                )
            }
            return buildSaleSnapshot(
                saleGuid = saleGuid,
                occurredAt = occurredAt,
                items = dtoList,
                cashMinor = cashMinor,
                cardMinor = cardMinor,
                usdRate = usdRate,
                cardTaxRate = cardTaxRate
            )
        }

        fun buildSaleSnapshot(
            saleGuid: String,
            occurredAt: Long,
            items: List<DebtCartItemDto>,
            cashMinor: Long,
            cardMinor: Long,
            usdRate: Double,
            cardTaxRate: Double
        ): DebtSaleSnapshot {
            require(items.isNotEmpty()) { "Savatchada tovar yo'q" }
            val scale = BigInteger.TEN.pow(8)
            val fxUnits = decimalUnits(formatDecimal(BigDecimal.valueOf(usdRate).setScale(8, RoundingMode.HALF_UP)))

            val saleItems = mutableListOf<DebtSaleItem>()
            var totalRevenueUnits = BigInteger.ZERO
            var totalCostUnits = BigInteger.ZERO

            for (item in items) {
                val itemGuid = UUID.randomUUID().toString()
                val prodGuid = item.productGuid
                val whGuid = if (isValidGuid(item.warehouseGuid)) item.warehouseGuid else CANONICAL_DEFAULT_WAREHOUSE_GUID
                val opGuid = UUID.randomUUID().toString()

                val qStr = formatDecimal(BigDecimal.valueOf(item.quantity).setScale(8, RoundingMode.HALF_UP))
                val pStr = formatDecimal(BigDecimal.valueOf(item.priceAtSale).setScale(8, RoundingMode.HALF_UP))
                val cStr = formatDecimal(BigDecimal.valueOf(item.costPrice).setScale(8, RoundingMode.HALF_UP))
                val deltaStr = "-$qStr"

                val qUnits = decimalUnits(qStr)
                val pUnits = decimalUnits(pStr)
                val cUnits = decimalUnits(cStr)

                totalRevenueUnits += qUnits * pUnits
                totalCostUnits += qUnits * cUnits * (if (item.costCurrency == "USD") fxUnits else scale)

                saleItems.add(
                    DebtSaleItem(
                        guid = itemGuid,
                        productGuid = prodGuid,
                        productName = item.productName,
                        category = item.category,
                        unit = item.unitDisplay,
                        warehouseGuid = whGuid,
                        warehouseName = item.warehouseName,
                        quantity = qStr,
                        price = pStr,
                        cost = cStr,
                        costCurrency = item.costCurrency,
                        stockOperationGuid = opGuid,
                        stockDelta = deltaStr
                    )
                )
            }

            val totalMinor = rounded(totalRevenueUnits * BigInteger.valueOf(100), scale * scale)
            val costMinor = rounded(totalCostUnits * BigInteger.valueOf(100), scale * scale * scale)

            val feeRateStr = formatDecimal(BigDecimal.valueOf(cardTaxRate).setScale(8, RoundingMode.HALF_UP))
            val feeRateUnits = decimalUnits(feeRateStr)
            val feeMinor = rounded(BigInteger.valueOf(cardMinor) * feeRateUnits, BigInteger.valueOf(100) * scale)

            return DebtSaleSnapshot(
                guid = saleGuid,
                occurredAt = occurredAt,
                totalMinor = totalMinor,
                costMinor = costMinor,
                cashMinor = cashMinor,
                cardMinor = cardMinor,
                feeMinor = feeMinor,
                feeRate = feeRateStr,
                usdRate = formatDecimal(BigDecimal.valueOf(usdRate).setScale(8, RoundingMode.HALF_UP)),
                paymentType = "DEBT",
                items = Collections.unmodifiableList(saleItems)
            )
        }

        fun isValidGuid(value: String): Boolean {
            return try {
                val g = UUID.fromString(value)
                g.toString().equals(value, ignoreCase = true) && g != UUID(0L, 0L)
            } catch (_: Throwable) {
                false
            }
        }
    }

    private fun ensureRepo(): DebtRepository = synchronized(lock) {
        val current = repository
        if (current != null) return current

        val db = database.openHelper.writableDatabase

        // Ensure canonical warehouse exists for legacy 'main-default-warehouse' fallback
        ensureCanonicalWarehouse(db)

        val existingStore = DebtRepository.scalar(db, "SELECT store_guid FROM debt_scope WHERE id=1") as? String
        val sGuid = if (!existingStore.isNullOrBlank()) {
            existingStore
        } else {
            val gen = UUID.randomUUID().toString()
            DebtRepository.exec(db, "INSERT INTO debt_scope(id, store_guid) VALUES(1, ?)", gen)
            gen
        }

        val existingActor = DebtRepository.scalar(db, "SELECT value FROM sync_meta WHERE key='mobile_actor_guid'") as? String
        val aGuid = if (!existingActor.isNullOrBlank()) {
            existingActor
        } else {
            val gen = UUID.randomUUID().toString()
            DebtRepository.exec(db, "INSERT INTO sync_meta(key, value) VALUES('mobile_actor_guid', ?)", gen)
            gen
        }

        storeGuid = sGuid
        actorGuid = aGuid

        val newRepo = DebtRepository(database, sGuid, aGuid) { true }
        repository = newRepo
        newRepo
    }

    private fun ensureCanonicalWarehouse(db: SupportSQLiteDatabase) {
        try {
            val hasCanonical = DebtRepository.scalar(db, "SELECT 1 FROM warehouses WHERE guid=?", CANONICAL_DEFAULT_WAREHOUSE_GUID) != null
            if (!hasCanonical) {
                val now = System.currentTimeMillis()
                DebtRepository.exec(db, "INSERT OR IGNORE INTO warehouses (guid, name, is_primary, is_deleted, updated_at) VALUES (?, 'Do''kondagi ombor', 0, 0, ?)", CANONICAL_DEFAULT_WAREHOUSE_GUID, now)
                DebtRepository.exec(db, "INSERT OR IGNORE INTO product_stocks (product_guid, warehouse_guid, quantity, updated_at) SELECT product_guid, ?, quantity, ? FROM product_stocks WHERE warehouse_guid='main-default-warehouse'", CANONICAL_DEFAULT_WAREHOUSE_GUID, now)
            }
        } catch (_: Throwable) { }
    }

    suspend fun getSummary(): DebtSummaryDto {
        ensureRepo()
        val db = database.openHelper.readableDatabase
        val today = LocalDate.now().toString()

        var totalActiveDebt = 0L
        var debtorsCount = 0
        var overdueDebt = 0L
        var overdueDebtorsCount = 0
        var totalPaid = 0L
        var totalCredit = 0L
        var creditCustomersCount = 0

        val custRows = DebtRepository.rows(
            db,
            "SELECT c.guid, c.archived, " +
                "(SELECT COALESCE(SUM(original_debt_minor), 0) FROM debt_accounts WHERE customer_guid = c.guid), " +
                "(SELECT COALESCE(SUM(-debt_delta_minor), 0) FROM debt_event_lines WHERE customer_guid = c.guid AND debt_delta_minor < 0) " +
                "FROM debt_customers c"
        )

        for (row in custRows) {
            val orig = (row[2] as Number).toLong()
            val paid = (row[3] as Number).toLong()
            val bal = orig - paid
            totalPaid += paid

            if (bal > 0) {
                totalActiveDebt += bal
                debtorsCount++
            } else if (bal < 0) {
                totalCredit += Math.abs(bal)
                creditCustomersCount++
            }

            val cGuid = row[0] as String
            val overdueRow = DebtRepository.rows(
                db,
                "SELECT a.guid, a.original_debt_minor, a.due_date, " +
                    "(SELECT COALESCE(SUM(-debt_delta_minor), 0) FROM debt_event_lines WHERE account_guid = a.guid AND debt_delta_minor < 0) " +
                    "FROM debt_accounts a " +
                    "WHERE a.customer_guid = ? AND a.due_date IS NOT NULL AND a.due_date < ?",
                cGuid, today
            )

            var custHasOverdue = false
            for (acc in overdueRow) {
                val accOrig = (acc[1] as Number).toLong()
                val accPaid = (acc[3] as Number).toLong()
                val accBal = accOrig - accPaid
                if (accBal > 0) {
                    overdueDebt += accBal
                    custHasOverdue = true
                }
            }
            if (custHasOverdue) overdueDebtorsCount++
        }

        return DebtSummaryDto(
            totalActiveDebtMinor = totalActiveDebt,
            debtorsCount = debtorsCount,
            overdueDebtMinor = overdueDebt,
            overdueDebtorsCount = overdueDebtorsCount,
            totalPaidMinor = totalPaid,
            totalCreditMinor = totalCredit,
            creditCustomersCount = creditCustomersCount
        )
    }

    suspend fun getCustomerList(search: String = "", filter: DebtFilter = DebtFilter.ALL): List<DebtCustomerItemDto> {
        ensureRepo()
        val db = database.openHelper.readableDatabase
        val today = LocalDate.now().toString()
        val list = mutableListOf<DebtCustomerItemDto>()

        val s = search.trim()
        val hasSearch = s.isNotEmpty()
        val searchPattern = "%$s%"

        val sql = if (hasSearch) {
            "SELECT guid, name, phone, note, archived, revision, created_at FROM debt_customers WHERE name LIKE ? OR phone LIKE ? ORDER BY name ASC"
        } else {
            "SELECT guid, name, phone, note, archived, revision, created_at FROM debt_customers ORDER BY name ASC"
        }

        val rows = if (hasSearch) DebtRepository.rows(db, sql, searchPattern, searchPattern) else DebtRepository.rows(db, sql)

        for (row in rows) {
            val guid = row[0] as String
            val name = row[1] as String
            val phone = (row[2] as? String) ?: ""
            val note = (row[3] as? String) ?: ""
            val archived = (row[4] as Long) == 1L
            val rev = (row[5] as Long)
            val createdAt = (row[6] as Long)

            val origDebt = ((DebtRepository.scalar(db, "SELECT COALESCE(SUM(original_debt_minor), 0) FROM debt_accounts WHERE customer_guid = ?", guid) as? Number)?.toLong()) ?: 0L
            val totalPaid = ((DebtRepository.scalar(db, "SELECT COALESCE(SUM(-debt_delta_minor), 0) FROM debt_event_lines WHERE customer_guid = ? AND debt_delta_minor < 0", guid) as? Number)?.toLong()) ?: 0L
            val balance = origDebt - totalPaid

            val overdueCount = ((DebtRepository.scalar(
                db,
                "SELECT COUNT(*) FROM debt_accounts a WHERE a.customer_guid = ? AND a.due_date IS NOT NULL AND a.due_date < ? AND " +
                    "((SELECT COALESCE(SUM(-debt_delta_minor), 0) FROM debt_event_lines WHERE account_guid = a.guid AND debt_delta_minor < 0) < a.original_debt_minor)",
                guid, today
            ) as? Number)?.toInt()) ?: 0

            val earliestDueDate = DebtRepository.scalar(
                db,
                "SELECT a.due_date FROM debt_accounts a WHERE a.customer_guid = ? AND a.due_date IS NOT NULL AND " +
                    "((SELECT COALESCE(SUM(-debt_delta_minor), 0) FROM debt_event_lines WHERE account_guid = a.guid AND debt_delta_minor < 0) < a.original_debt_minor) " +
                    "ORDER BY a.due_date ASC LIMIT 1",
                guid
            ) as? String

            val activeAccountsCount = ((DebtRepository.scalar(
                db,
                "SELECT COUNT(*) FROM debt_accounts a WHERE a.customer_guid = ? AND " +
                    "((SELECT COALESCE(SUM(-debt_delta_minor), 0) FROM debt_event_lines WHERE account_guid = a.guid AND debt_delta_minor < 0) < a.original_debt_minor)",
                guid
            ) as? Number)?.toInt()) ?: 0

            val item = DebtCustomerItemDto(
                guid = guid,
                name = name,
                phone = phone,
                note = note,
                archived = archived,
                revision = rev,
                createdAt = createdAt,
                balanceMinor = balance,
                originalDebtMinor = origDebt,
                totalPaidMinor = totalPaid,
                hasOverdue = overdueCount > 0,
                earliestDueDate = earliestDueDate,
                activeAccountsCount = activeAccountsCount
            )

            val match = when (filter) {
                DebtFilter.ALL -> true
                DebtFilter.ACTIVE_DEBT -> balance > 0
                DebtFilter.OVERDUE -> overdueCount > 0
                DebtFilter.SETTLED -> balance == 0L && origDebt > 0
                DebtFilter.CREDIT -> balance < 0
            }

            if (match) list.add(item)
        }

        return list
    }

    suspend fun getActiveCustomers(): List<DebtCustomerItemDto> {
        val all = getCustomerList("", DebtFilter.ALL)
        return all.filter { !it.archived }
    }

    suspend fun getCustomerDetails(customerGuid: String): DebtCustomerDetailDto? {
        val repo = ensureRepo()
        val customerRecord = try {
            repo.readCustomer(customerGuid)
        } catch (_: Throwable) {
            return null
        } ?: return null

        val db = database.openHelper.readableDatabase
        val today = LocalDate.now().toString()

        val accountsList = mutableListOf<DebtAccountItemDto>()
        val accRows = DebtRepository.rows(
            db,
            "SELECT a.guid, a.sale_guid, a.customer_name_at_sale, a.original_debt_minor, a.due_date, s.created_at, " +
                "(SELECT COALESCE(SUM(-debt_delta_minor), 0) FROM debt_event_lines WHERE account_guid = a.guid AND debt_delta_minor < 0) " +
                "FROM debt_accounts a " +
                "JOIN sales s ON s.guid = a.sale_guid " +
                "WHERE a.customer_guid = ? ORDER BY s.created_at DESC",
            customerGuid
        )

        for (row in accRows) {
            val aGuid = row[0] as String
            val saleGuid = row[1] as String
            val custNameAtSale = row[2] as String
            val orig = (row[3] as Number).toLong()
            val dueDate = row[4] as? String
            val createdAt = (row[5] as Number).toLong()
            val paid = (row[6] as Number).toLong()
            val bal = orig - paid
            val isOverdue = dueDate != null && dueDate < today && bal > 0

            accountsList.add(
                DebtAccountItemDto(
                    accountGuid = aGuid,
                    saleGuid = saleGuid,
                    customerNameAtSale = custNameAtSale,
                    originalDebtMinor = orig,
                    balanceMinor = bal,
                    totalPaidMinor = paid,
                    dueDate = dueDate,
                    isOverdue = isOverdue,
                    createdAt = createdAt,
                    receiptNumber = "LP-" + saleGuid.replace("-", "").uppercase().take(8)
                )
            )
        }

        val eventsList = mutableListOf<DebtEventItemDto>()
        val evRows = DebtRepository.rows(
            db,
            "SELECT guid, request_guid, kind, occurred_at, cash_minor, card_minor " +
                "FROM debt_events WHERE customer_guid = ? ORDER BY occurred_at DESC, device_sequence DESC",
            customerGuid
        )

        for (row in evRows) {
            val eGuid = row[0] as String
            val reqGuid = row[1] as String
            val kind = row[2] as String
            val occurredAt = (row[3] as Number).toLong()
            val cash = (row[4] as Number).toLong()
            val card = (row[5] as Number).toLong()

            val lineRows = DebtRepository.rows(
                db,
                "SELECT account_guid, debt_delta_minor FROM debt_event_lines WHERE event_guid = ? ORDER BY line_index ASC",
                eGuid
            )
            val lines = lineRows.map { l ->
                DebtEventLineDto(
                    accountGuid = l[0] as String,
                    debtDeltaMinor = (l[1] as Number).toLong()
                )
            }

            eventsList.add(
                DebtEventItemDto(
                    eventGuid = eGuid,
                    requestGuid = reqGuid,
                    kind = kind,
                    occurredAt = occurredAt,
                    cashMinor = cash,
                    cardMinor = card,
                    lines = lines
                )
            )
        }

        val totalOrig = accountsList.sumOf { it.originalDebtMinor }
        val totalPaid = accountsList.sumOf { it.totalPaidMinor }
        val balance = totalOrig - totalPaid

        return DebtCustomerDetailDto(
            customer = customerRecord,
            balanceMinor = balance,
            accounts = accountsList,
            events = eventsList
        )
    }

    suspend fun previewPayment(
        customerGuid: String,
        paymentMinor: Long,
        targetAccountGuid: String? = null
    ): DebtPaymentAllocationPreview {
        require(paymentMinor > 0) { "To'lov summasi musbat bo'lishi shart" }
        val details = getCustomerDetails(customerGuid) ?: throw IllegalArgumentException("Mijoz topilmadi")

        val totalOutstanding = details.accounts.sumOf { it.balanceMinor }
        if (paymentMinor > totalOutstanding) {
            throw IllegalArgumentException("To'lov summasi (${paymentMinor / 100.0} so'm) mavjud qarzdan (${totalOutstanding / 100.0} so'm) ko'p bo'lishi mumkin emas")
        }

        val candidateAccounts = if (!targetAccountGuid.isNullOrBlank()) {
            val target = details.accounts.find { it.accountGuid == targetAccountGuid }
                ?: throw IllegalArgumentException("Tanlangan nasiya hisobi topilmadi")
            if (paymentMinor > target.balanceMinor) {
                throw IllegalArgumentException("To'lov summasi tanlangan hisob qarzidan ko'p bo'lishi mumkin emas")
            }
            listOf(target)
        } else {
            details.accounts.filter { it.balanceMinor > 0 }.sortedBy { it.createdAt }
        }

        var remainingToAllocate = paymentMinor
        val previewLines = mutableListOf<DebtAllocationLinePreview>()

        for (acc in candidateAccounts) {
            if (remainingToAllocate <= 0) break
            val take = Math.min(acc.balanceMinor, remainingToAllocate)
            remainingToAllocate -= take

            previewLines.add(
                DebtAllocationLinePreview(
                    accountGuid = acc.accountGuid,
                    receiptNumber = acc.receiptNumber,
                    previousBalanceMinor = acc.balanceMinor,
                    paidMinor = take,
                    newBalanceMinor = acc.balanceMinor - take
                )
            )
        }

        val newTotalRemaining = totalOutstanding - paymentMinor
        return DebtPaymentAllocationPreview(
            totalPaymentMinor = paymentMinor,
            remainingBalanceMinor = newTotalRemaining,
            lines = previewLines
        )
    }

    suspend fun recordPayment(
        customerGuid: String,
        cashMinor: Long,
        cardMinor: Long,
        targetAccountGuid: String? = null,
        requestGuid: String? = null,
        occurredAt: Long? = null
    ): String {
        val repo = ensureRepo()
        val req = requestGuid ?: UUID.randomUUID().toString()
        var timestamp = occurredAt ?: System.currentTimeMillis()

        if (occurredAt == null && !requestGuid.isNullOrBlank()) {
            val existingAt = getDebtEventOccurredAt(req)
            if (existingAt != null) {
                timestamp = existingAt
            }
        }

        val cmd = DebtPaymentCommand(
            requestGuid = req,
            customerGuid = customerGuid,
            cashMinor = cashMinor,
            cardMinor = cardMinor,
            feeMinor = 0L,
            occurredAt = timestamp,
            targetAccountGuid = targetAccountGuid
        )

        return repo.takePayment(cmd)
    }

    suspend fun getDebtEventOccurredAt(requestGuid: String): Long? {
        if (requestGuid.isBlank()) return null
        val db = database.openHelper.readableDatabase
        val res = DebtRepository.scalar(db, "SELECT occurred_at FROM debt_events WHERE request_guid = ? LIMIT 1", requestGuid)
        return (res as? Number)?.toLong()
    }

    suspend fun openDebtSale(
        requestGuid: String,
        customerGuid: String,
        saleSnapshot: DebtSaleSnapshot,
        dueDate: String? = null,
        newCustomer: DebtCustomerDraft? = null,
        userId: Long = 1L
    ): String {
        val repo = ensureRepo()
        val cmd = DebtOpenSaleCommand(
            requestGuid = requestGuid,
            customerGuid = customerGuid,
            sale = saleSnapshot,
            dueDate = dueDate,
            newCustomer = newCustomer,
            userId = userId
        )
        return repo.openSale(cmd)
    }

    suspend fun createCustomer(name: String, phone: String, note: String): String {
        val repo = ensureRepo()
        val guid = UUID.randomUUID().toString()
        val draft = DebtCustomerDraft(
            guid = guid,
            name = name.trim(),
            phone = phone.trim(),
            note = note.trim(),
            createdAt = System.currentTimeMillis()
        )
        return repo.createCustomer(draft)
    }

    suspend fun updateCustomer(
        customerGuid: String,
        name: String,
        phone: String,
        note: String,
        revision: Long
    ): Boolean {
        val repo = ensureRepo()
        val update = DebtCustomerUpdate(
            customerGuid = customerGuid,
            name = name.trim(),
            phone = phone.trim(),
            note = note.trim(),
            revision = revision
        )
        return repo.updateCustomer(update)
    }

    suspend fun archiveCustomer(customerGuid: String, archive: Boolean): Boolean {
        val repo = ensureRepo()
        return repo.archiveCustomer(customerGuid, archive)
    }
}
