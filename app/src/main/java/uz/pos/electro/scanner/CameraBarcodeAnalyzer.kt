package uz.pos.electro.scanner

import androidx.annotation.OptIn
import androidx.camera.core.ExperimentalGetImage
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.ImageProxy
import com.google.mlkit.vision.barcode.BarcodeScannerOptions
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.common.InputImage

class CameraBarcodeAnalyzer(
    private val onBarcodeDetected: (String) -> Unit
) : ImageAnalysis.Analyzer {

    private val options = BarcodeScannerOptions.Builder()
        .setBarcodeFormats(
            Barcode.FORMAT_EAN_13,
            Barcode.FORMAT_EAN_8,
            Barcode.FORMAT_UPC_A,
            Barcode.FORMAT_UPC_E,
            Barcode.FORMAT_CODE_128,
            Barcode.FORMAT_CODE_39,
            Barcode.FORMAT_CODE_93,
            Barcode.FORMAT_ITF,
            Barcode.FORMAT_QR_CODE
        )
        .build()

    private val scanner = BarcodeScanning.getClient(options)
    private var isScanningEnabled = true
    private var lastScannedCode: String? = null
    private var lastScannedTimestamp = 0L
    private val debounceTimeMs = 1500L

    @OptIn(ExperimentalGetImage::class)
    override fun analyze(imageProxy: ImageProxy) {
        val mediaImage = imageProxy.image
        if (mediaImage != null && isScanningEnabled) {
            val image = InputImage.fromMediaImage(
                mediaImage,
                imageProxy.imageInfo.rotationDegrees
            )

            scanner.process(image)
                .addOnSuccessListener { barcodes ->
                    val currentTime = System.currentTimeMillis()
                    for (barcode in barcodes) {
                        barcode.rawValue?.let { rawValue ->
                            if (rawValue.isNotBlank() && (rawValue != lastScannedCode || currentTime - lastScannedTimestamp > debounceTimeMs)) {
                                lastScannedCode = rawValue
                                lastScannedTimestamp = currentTime
                                onBarcodeDetected(rawValue)
                            }
                        }
                    }
                }
                .addOnFailureListener {
                    // Xatolik bo'lsa ham imageProxy yopilishi shart
                }
                .addOnCompleteListener {
                    imageProxy.close()
                }
        } else {
            imageProxy.close()
        }
    }

    fun pauseScanning() {
        isScanningEnabled = false
    }

    fun resumeScanning() {
        isScanningEnabled = true
    }
}
