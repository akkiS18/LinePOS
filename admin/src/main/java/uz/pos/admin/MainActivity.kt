package uz.pos.admin

import android.app.KeyguardManager
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.Bundle
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.animation.Crossfade
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.Surface
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import uz.pos.admin.data.AdminRepository
import uz.pos.admin.ui.DashboardScreen
import uz.pos.admin.ui.LoginScreen
import uz.pos.admin.voice.SebVoiceActivity

class MainActivity : ComponentActivity() {

    private val repository = AdminRepository()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // Lock Screen (qulflangan ekran)dan ochilish imkoniyati
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O_MR1) {
            setShowWhenLocked(true)
            setTurnScreenOn(true)
        } else {
            @Suppress("DEPRECATION")
            window.addFlags(
                WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED or
                WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON
            )
        }

        // AGAR TELEFON QULFLANGAN HOLATDA (Lock Screen shortcut orqali) OCHILSA:
        // Kod so'rab o'tirmasdan, darhol Seb Voice (ovozli yordamchi)ga yo'naltiramiz!
        val keyguardManager = getSystemService(Context.KEYGUARD_SERVICE) as? KeyguardManager
        if (keyguardManager?.isKeyguardLocked == true) {
            val voiceIntent = Intent(this, SebVoiceActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
            }
            startActivity(voiceIntent)
            finish()
            return
        }
        setContent {
            var isLoggedIn by remember { mutableStateOf(false) }

            Surface(
                modifier = Modifier.fillMaxSize(),
                color = Color(0xFF0F172A)
            ) {
                Crossfade(targetState = isLoggedIn, label = "AdminAuthCrossfade") { loggedIn ->
                    if (loggedIn) {
                        DashboardScreen(
                            repository = repository,
                            onLogout = { isLoggedIn = false }
                        )
                    } else {
                        LoginScreen(
                            onLoginSuccess = { isLoggedIn = true }
                        )
                    }
                }
            }
        }
    }
}
