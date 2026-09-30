package uz.pos.electro.data.repository

import android.content.Context
import android.content.SharedPreferences
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class TaxSettingsRepository @Inject constructor(
    @ApplicationContext private val context: Context
) {
    private val prefs: SharedPreferences = context.getSharedPreferences("pos_tax_prefs", Context.MODE_PRIVATE)

    companion object {
        private const val KEY_CARD_TAX_RATE = "card_tax_rate"
        const val DEFAULT_CARD_TAX_RATE = 1.8
    }

    private val _cardTaxRate = MutableStateFlow(getCardTaxRate())
    val cardTaxRate: StateFlow<Double> = _cardTaxRate.asStateFlow()

    fun getCardTaxRate(): Double {
        return try {
            val str = prefs.getString(KEY_CARD_TAX_RATE, null)
            if (str != null) {
                str.toDoubleOrNull() ?: DEFAULT_CARD_TAX_RATE
            } else {
                val fl = prefs.getFloat(KEY_CARD_TAX_RATE, DEFAULT_CARD_TAX_RATE.toFloat())
                Math.round(fl * 100.0) / 100.0
            }
        } catch (e: Exception) {
            try {
                val fl = prefs.getFloat(KEY_CARD_TAX_RATE, DEFAULT_CARD_TAX_RATE.toFloat())
                Math.round(fl * 100.0) / 100.0
            } catch (_: Exception) {
                DEFAULT_CARD_TAX_RATE
            }
        }
    }

    fun setCardTaxRate(rate: Double) {
        val rounded = Math.round(rate * 100.0) / 100.0
        val formatted = if (rounded % 1.0 == 0.0) {
            rounded.toInt().toString()
        } else {
            String.format(java.util.Locale.US, "%.2f", rounded).trimEnd('0').trimEnd('.')
        }
        prefs.edit()
            .remove(KEY_CARD_TAX_RATE)
            .putString(KEY_CARD_TAX_RATE, formatted)
            .apply()
        _cardTaxRate.value = rounded
    }
}
