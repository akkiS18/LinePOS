package uz.pos.admin.voice

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.speech.RecognitionListener
import android.speech.RecognizerIntent
import android.speech.SpeechRecognizer
import android.speech.tts.TextToSpeech
import android.speech.tts.UtteranceProgressListener
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import uz.pos.admin.data.AdminRepository
import uz.pos.admin.ui.ArcReactorIcon
import uz.pos.admin.ui.theme.StarkBorderCyan
import uz.pos.admin.ui.theme.StarkReactorBlue
import uz.pos.admin.ui.theme.StarkTextWhite
import java.util.Locale

class SebVoiceActivity : ComponentActivity(), TextToSpeech.OnInitListener {

    private var speechRecognizer: SpeechRecognizer? = null
    private var textToSpeech: TextToSpeech? = null
    private val repository = AdminRepository()

    private var statusText by mutableStateOf("Seb is listening...")
    private var recognizedText by mutableStateOf("")
    private var isListening by mutableStateOf(false)
    private var isSessionActive = true

    private val permissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { granted ->
        if (granted) {
            startListening()
        } else {
            statusText = "Microphone permission denied"
            lifecycleScope.launch {
                delay(1500)
                finish()
            }
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // Lock Screen (qulflangan ekran) ustidan ochilish imkoniyati
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O_MR1) {
            setShowWhenLocked(true)
            setTurnScreenOn(true)
        }
        @Suppress("DEPRECATION")
        window.addFlags(
            WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED or
            WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON or
            WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON
        )

        textToSpeech = TextToSpeech(this, this)

        setContent {
            VoiceOverlayUI(
                status = statusText,
                recognized = recognizedText,
                listening = isListening,
                onDismiss = {
                    isSessionActive = false
                    finish()
                }
            )
        }

        checkAndStartListening()
    }

    private fun checkAndStartListening() {
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED) {
            startListening()
        } else {
            permissionLauncher.launch(Manifest.permission.RECORD_AUDIO)
        }
    }

    private fun startListening() {
        if (!isSessionActive || isFinishing) return

        runOnUiThread {
            try {
                speechRecognizer?.destroy()
                speechRecognizer = SpeechRecognizer.createSpeechRecognizer(this).apply {
                    setRecognitionListener(object : RecognitionListener {
                        override fun onReadyForSpeech(params: Bundle?) {
                            isListening = true
                            statusText = "Listening... (say 'close' to exit)"
                        }

                        override fun onBeginningOfSpeech() {
                            statusText = "Processing audio..."
                        }

                        override fun onRmsChanged(rmsdB: Float) {}
                        override fun onBufferReceived(buffer: ByteArray?) {}
                        override fun onEndOfSpeech() {
                            isListening = false
                            statusText = "Analyzing command..."
                        }

                        override fun onError(error: Int) {
                            isListening = false
                            // Agar sessiya hali ochiq bo'lsa, xatolikdan keyin yana tinglashga qaytadi (to'xtab qolmaydi)
                            if (isSessionActive && !isFinishing) {
                                lifecycleScope.launch {
                                    delay(800)
                                    if (isSessionActive) startListening()
                                }
                            }
                        }

                        override fun onResults(results: Bundle?) {
                            val matches = results?.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION)
                            val text = matches?.firstOrNull() ?: ""
                            recognizedText = text

                            lifecycleScope.launch {
                                val result = VoiceCommandProcessor.processCommand(text, repository)
                                statusText = result.reply

                                if (result.shouldClose) {
                                    isSessionActive = false
                                    speak(result.reply) {
                                        finish()
                                    }
                                } else {
                                    // Javobni ovoz chiqarib o'qiydi, tugashi bilan YANA tinglashni boshlaydi!
                                    speak(result.reply) {
                                        if (isSessionActive && !isFinishing) {
                                            startListening()
                                        }
                                    }
                                }
                            }
                        }

                        override fun onPartialResults(partialResults: Bundle?) {}
                        override fun onEvent(eventType: Int, params: Bundle?) {}
                    })
                }

                val intent = Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH).apply {
                    putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM)
                    putExtra(RecognizerIntent.EXTRA_LANGUAGE, "en-US")
                    putExtra(RecognizerIntent.EXTRA_LANGUAGE_PREFERENCE, "en-US")
                    putExtra("android.speech.extra.EXTRA_ADDITIONAL_LANGUAGES", arrayOf("uz-UZ", "en-US", "ru-RU"))
                }

                speechRecognizer?.startListening(intent)
            } catch (t: Throwable) {
                // Fail-safe
            }
        }
    }

    private fun speak(text: String, onDone: (() -> Unit)? = null) {
        val utteranceId = "SEB_TTS_${System.currentTimeMillis()}"

        textToSpeech?.setOnUtteranceProgressListener(object : UtteranceProgressListener() {
            override fun onStart(utteranceId: String?) {}
            override fun onDone(utteranceId: String?) {
                lifecycleScope.launch {
                    delay(300)
                    onDone?.invoke()
                }
            }
            @Deprecated("Deprecated in Java")
            override fun onError(utteranceId: String?) {
                onDone?.invoke()
            }
        })

        textToSpeech?.speak(text, TextToSpeech.QUEUE_FLUSH, null, utteranceId)
    }

    override fun onInit(status: Int) {
        if (status == TextToSpeech.SUCCESS) {
            // JARVIS kabi sof ingliz tilida ravon o'qishi uchun Locale.US
            textToSpeech?.language = Locale.US
            textToSpeech?.setPitch(0.95f) // Bir oz chuqurroq, texnik kiber tembr
            textToSpeech?.setSpeechRate(1.0f)
        }
    }

    override fun onDestroy() {
        isSessionActive = false
        super.onDestroy()
        speechRecognizer?.destroy()
        textToSpeech?.stop()
        textToSpeech?.shutdown()
    }
}

@Composable
fun VoiceOverlayUI(
    status: String,
    recognized: String,
    listening: Boolean,
    onDismiss: () -> Unit
) {
    val infiniteTransition = rememberInfiniteTransition(label = "pulse")
    val pulseScale by infiniteTransition.animateFloat(
        initialValue = 1f,
        targetValue = if (listening) 1.25f else 1.05f,
        animationSpec = infiniteRepeatable(
            animation = tween(800, easing = FastOutSlowInEasing),
            repeatMode = RepeatMode.Reverse
        ),
        label = "scale"
    )

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Color(0xD9040913))
            .clickable { onDismiss() },
        contentAlignment = Alignment.Center
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
            modifier = Modifier.padding(24.dp)
        ) {
            Box(
                modifier = Modifier
                    .size(100.dp)
                    .scale(pulseScale)
                    .background(Color(0x3338BDF8), CircleShape)
                    .border(2.dp, StarkBorderCyan, CircleShape),
                contentAlignment = Alignment.Center
            ) {
                ArcReactorIcon(
                    tint = StarkReactorBlue,
                    modifier = Modifier.size(54.dp)
                )
            }

            Spacer(modifier = Modifier.height(24.dp))

            Text(
                text = "SEB VOICE // STANDBY",
                color = StarkBorderCyan,
                fontSize = 12.sp,
                fontWeight = FontWeight.ExtraBold,
                fontFamily = FontFamily.Monospace,
                letterSpacing = 2.sp
            )

            Spacer(modifier = Modifier.height(10.dp))

            Text(
                text = status,
                color = StarkTextWhite,
                fontSize = 16.sp,
                fontWeight = FontWeight.Bold,
                fontFamily = FontFamily.SansSerif,
                textAlign = androidx.compose.ui.text.style.TextAlign.Center
            )

            if (recognized.isNotEmpty()) {
                Spacer(modifier = Modifier.height(10.dp))
                Text(
                    text = "\"$recognized\"",
                    color = Color(0xFF94A3B8),
                    fontSize = 14.sp,
                    fontFamily = FontFamily.SansSerif
                )
            }

            Spacer(modifier = Modifier.height(20.dp))

            Text(
                text = "Tap anywhere or say 'close' to exit",
                color = Color(0xFF64748B),
                fontSize = 11.sp,
                fontFamily = FontFamily.Monospace
            )
        }
    }
}
