package uz.pos.electro.scanner

import android.view.KeyEvent
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.launch
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Tashqi apparat skaneridan (Bluetooth yoki USB HID) keladigan
 * klaviatura signallarini ushlab qolib, to'liq shtrix-kodni hosil qiluvchi menejer.
 */
@Singleton
class HardwareScannerManager @Inject constructor() {

    private val _scanEvents = MutableSharedFlow<String>(extraBufferCapacity = 1)
    val scanEvents: SharedFlow<String> = _scanEvents.asSharedFlow()

    private val barcodeBuffer = StringBuilder()
    private var lastKeyTimestamp = 0L
    private val scope = CoroutineScope(Dispatchers.Default)

    // Hardware skanerlar harflarni juda tez (odatda < 50ms) yuboradi
    private val maxCharacterIntervalMs = 70L

    /**
     * MainActivity.dispatchKeyEvent orqali chaqiriladi.
     * @return agar hodisa apparat skaneriga tegishli bo'lsa true, aks holda false.
     */
    fun onKeyEvent(event: KeyEvent): Boolean {
        if (event.action != KeyEvent.ACTION_DOWN) {
            return false
        }

        val currentTime = System.currentTimeMillis()

        // Agar oraliq vaqt katta bo'lsa (qo'lda klaviaturadan kiritilayotgan bo'lsa), buferni tozalaymiz
        if (currentTime - lastKeyTimestamp > maxCharacterIntervalMs && barcodeBuffer.isNotEmpty()) {
            barcodeBuffer.clear()
        }
        lastKeyTimestamp = currentTime

        when (event.keyCode) {
            KeyEvent.KEYCODE_ENTER, KeyEvent.KEYCODE_NUMPAD_ENTER -> {
                if (barcodeBuffer.isNotEmpty()) {
                    val scannedBarcode = barcodeBuffer.toString().trim()
                    barcodeBuffer.clear()
                    if (scannedBarcode.isNotBlank()) {
                        scope.launch {
                            _scanEvents.emit(scannedBarcode)
                        }
                        return true
                    }
                }
            }
            else -> {
                val unicodeChar = event.unicodeChar
                if (unicodeChar != 0 && !Character.isISOControl(unicodeChar)) {
                    barcodeBuffer.append(unicodeChar.toChar())
                }
            }
        }

        return false
    }

    /**
     * Qo'lda yoki boshqa manbalardan (masalan, kamera skaneridan) kelgan kodni
     * global oqimga jo'natish uchun yordamchi funksiya.
     */
    fun emitScannedBarcode(barcode: String) {
        if (barcode.isNotBlank()) {
            scope.launch {
                _scanEvents.emit(barcode.trim())
            }
        }
    }
}
