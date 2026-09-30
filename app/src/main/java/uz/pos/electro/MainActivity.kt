package uz.pos.electro

import android.os.Bundle
import android.view.KeyEvent
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.Surface
import androidx.compose.ui.Modifier
import dagger.hilt.android.AndroidEntryPoint
import uz.pos.electro.data.licensing.DeviceLicensingManager
import uz.pos.electro.scanner.HardwareScannerManager
import uz.pos.electro.ui.theme.LinePOSTheme
import javax.inject.Inject

@AndroidEntryPoint
class MainActivity : ComponentActivity() {

    @Inject
    lateinit var hardwareScannerManager: HardwareScannerManager

    @Inject
    lateinit var licensingManager: DeviceLicensingManager

    @Inject
    lateinit var localSyncManager: uz.pos.electro.data.sync.LocalSyncManager

    @Inject
    lateinit var taxSettingsRepository: uz.pos.electro.data.repository.TaxSettingsRepository

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            LinePOSTheme {
                Surface(modifier = Modifier.fillMaxSize()) {
                    MainScreen(
                        scannerManager = hardwareScannerManager,
                        licensingManager = licensingManager,
                        syncManager = localSyncManager,
                        taxSettingsRepository = taxSettingsRepository
                    )
                }
            }
        }
    }

    override fun onResume() {
        super.onResume()
        localSyncManager.startLiveSyncEngine()
    }

    /**
     * Tashqi apparat skaneri (USB yoki Bluetooth) orqali kiritilgan
     * signallarni global darajada tutib olish.
     */
    override fun dispatchKeyEvent(event: KeyEvent): Boolean {
        if (hardwareScannerManager.onKeyEvent(event)) {
            return true
        }
        return super.dispatchKeyEvent(event)
    }
}
