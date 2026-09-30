package uz.pos.admin

import android.os.Bundle
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

class MainActivity : ComponentActivity() {

    private val repository = AdminRepository()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
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
