package uz.pos.electro.data.local.converter

import androidx.room.TypeConverter
import uz.pos.electro.data.model.PaymentType
import uz.pos.electro.data.model.UnitType
import uz.pos.electro.data.model.UserRole

class Converters {

    @TypeConverter
    fun fromUserRole(role: UserRole): String = role.name

    @TypeConverter
    fun toUserRole(value: String): UserRole = runCatching {
        UserRole.valueOf(value)
    }.getOrDefault(UserRole.CASHIER)

    @TypeConverter
    fun fromUnitType(unitType: UnitType): String = unitType.name

    @TypeConverter
    fun toUnitType(value: String): UnitType = runCatching {
        UnitType.valueOf(value)
    }.getOrDefault(UnitType.DONA)

    @TypeConverter
    fun fromPaymentType(paymentType: PaymentType): String = paymentType.name

    @TypeConverter
    fun toPaymentType(value: String): PaymentType = runCatching {
        PaymentType.valueOf(value)
    }.getOrDefault(PaymentType.CASH)
}
