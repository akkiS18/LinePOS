package uz.pos.electro.data.licensing

import android.annotation.SuppressLint
import android.content.Context
import android.content.SharedPreferences
import android.os.Build
import android.provider.Settings
import com.google.firebase.database.DataSnapshot
import com.google.firebase.database.DatabaseError
import com.google.firebase.database.FirebaseDatabase
import com.google.firebase.database.ValueEventListener
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.coroutines.launch
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class DeviceLicensingManager @Inject constructor(
    @ApplicationContext private val context: Context
) {
    private val prefs: SharedPreferences = context.getSharedPreferences("pos_license_prefs", Context.MODE_PRIVATE)

    companion object {
        val ADMIN_MASTER_PIN = uz.pos.electro.BuildConfig.ADMIN_PIN
        private const val KEY_IS_ACTIVATED = "is_device_activated"
        private const val KEY_ACTIVATION_DATE = "activation_date"
        private const val KEY_CUSTOM_DEVICE_NAME = "custom_device_name"
    }

    private val _isActivated = MutableStateFlow(prefs.getBoolean(KEY_IS_ACTIVATED, false))
    val isActivated: StateFlow<Boolean> = _isActivated.asStateFlow()

    private val dbInstance: FirebaseDatabase? by lazy {
        try {
            FirebaseDatabase.getInstance().apply {
                setPersistenceEnabled(true)
            }
        } catch (e: Exception) {
            null
        }
    }

    init {
        startSyncAndLicenseListener()
    }

    @SuppressLint("HardwareIds")
    fun getDeviceId(): String {
        return try {
            Settings.Secure.getString(context.contentResolver, Settings.Secure.ANDROID_ID) ?: "UNKNOWN_DEVICE_ID"
        } catch (e: Exception) {
            "UNKNOWN_DEVICE_ID"
        }
    }

    fun getDeviceModel(): String {
        return "${Build.MANUFACTURER.replaceFirstChar { it.uppercase() }} ${Build.MODEL}"
    }

    fun getActivationDate(): Long {
        return prefs.getLong(KEY_ACTIVATION_DATE, 0L)
    }

    fun getCustomDeviceName(): String {
        return prefs.getString(KEY_CUSTOM_DEVICE_NAME, "") ?: ""
    }

    /**
     * Start Realtime sync:
     * 1. Pings device presence & info to Firebase (/devices/{deviceId})
     * 2. Listens for remote Lock / Unlock commands from Admin
     */
    private fun startSyncAndLicenseListener() {
        val deviceId = getDeviceId()
        if (deviceId == "UNKNOWN_DEVICE_ID") return

        try {
            val database = dbInstance ?: return
            val deviceRef = database.getReference("devices").child(deviceId)

            // 1. Report info & heartbeat
            val localIsActivated = prefs.getBoolean(KEY_IS_ACTIVATED, false)
            val infoMap = mutableMapOf<String, Any>(
                "deviceId" to deviceId,
                "deviceModel" to getDeviceModel(),
                "lastActive" to System.currentTimeMillis(),
                "appVersion" to "1.0.0"
            )
            if (!prefs.contains(KEY_IS_ACTIVATED)) {
                infoMap["isActivated"] = false
            }

            deviceRef.updateChildren(infoMap)

            // 2. Real-time License State Listener (Remote Kill Switch)
            deviceRef.child("isActivated").addValueEventListener(object : ValueEventListener {
                override fun onDataChange(snapshot: DataSnapshot) {
                    val remoteActive = snapshot.getValue(Boolean::class.java)
                    if (remoteActive != null) {
                        prefs.edit().putBoolean(KEY_IS_ACTIVATED, remoteActive).apply()
                        _isActivated.value = remoteActive
                    }
                }

                override fun onCancelled(error: DatabaseError) {
                    // Silently fallback to local state if offline
                }
            })
        } catch (e: Exception) {
            // Firebase not initialized or no network; graceful offline fallback
        }
    }

    /**
     * Admin PIN orqali qurilmani faollashtirish (Firebase'dan tekshiriladi)
     */
    fun activateWithAdminPin(pin: String, onResult: (Boolean) -> Unit) {
        val database = dbInstance
        if (database == null) {
            // Agar umuman internet yoki Firebase ishlamasa, default oflayn zaxira kod 1984
            if (pin.trim() == "1984") {
                activateLocal()
                onResult(true)
            } else {
                onResult(false)
            }
            return
        }

        database.getReference("settings").child("globalCode").get().addOnCompleteListener { task ->
            if (task.isSuccessful) {
                val remotePin = task.result?.getValue(String::class.java) ?: "1984"
                if (pin.trim() == remotePin) {
                    activateLocal()
                    // Sync to Firebase
                    val deviceId = getDeviceId()
                    database.getReference("devices").child(deviceId).updateChildren(
                        mapOf(
                            "isActivated" to true,
                            "lastActive" to System.currentTimeMillis()
                        )
                    )
                    onResult(true)
                } else {
                    onResult(false)
                }
            } else {
                onResult(false)
            }
        }
    }

    private fun activateLocal() {
        val now = System.currentTimeMillis()
        prefs.edit()
            .putBoolean(KEY_IS_ACTIVATED, true)
            .putLong(KEY_ACTIVATION_DATE, now)
            .apply()
        _isActivated.value = true
    }

    /**
     * Qurilmani lokal bloklash
     */
    fun blockDevice() {
        prefs.edit().putBoolean(KEY_IS_ACTIVATED, false).apply()
        _isActivated.value = false

        try {
            val deviceId = getDeviceId()
            dbInstance?.getReference("devices")?.child(deviceId)?.child("isActivated")?.setValue(false)
        } catch (e: Exception) {
            // Offline fallback
        }
    }

    /**
     * Admin: Fetch all registered devices in real time from Firebase
     */
    fun getAllDevicesFlow(): Flow<List<RemoteDevice>> = callbackFlow {
        val database = dbInstance
        if (database == null) {
            // If Firebase not configured, return current local device
            trySend(
                listOf(
                    RemoteDevice(
                        deviceId = getDeviceId(),
                        deviceModel = getDeviceModel(),
                        isActivated = _isActivated.value,
                        lastActive = System.currentTimeMillis(),
                        customName = getCustomDeviceName()
                    )
                )
            )
            close()
            return@callbackFlow
        }

        val devicesRef = database.getReference("devices")
        val listener = object : ValueEventListener {
            override fun onDataChange(snapshot: DataSnapshot) {
                val list = mutableListOf<RemoteDevice>()
                for (child in snapshot.children) {
                    val deviceId = child.child("deviceId").getValue(String::class.java) ?: child.key ?: ""
                    val deviceModel = child.child("deviceModel").getValue(String::class.java) ?: "Noma'lum"
                    val isActivated = child.child("isActivated").getValue(Boolean::class.java) ?: false
                    val lastActive = child.child("lastActive").getValue(Long::class.java) ?: 0L
                    val customName = child.child("customName").getValue(String::class.java) ?: ""
                    val appVersion = child.child("appVersion").getValue(String::class.java) ?: "1.0.0"

                    list.add(
                        RemoteDevice(
                            deviceId = deviceId,
                            deviceModel = deviceModel,
                            isActivated = isActivated,
                            lastActive = lastActive,
                            customName = customName,
                            appVersion = appVersion
                        )
                    )
                }
                // Sort by last active desc
                list.sortByDescending { it.lastActive }
                trySend(list)
            }

            override fun onCancelled(error: DatabaseError) {
                close(error.toException())
            }
        }

        devicesRef.addValueEventListener(listener)
        awaitClose { devicesRef.removeEventListener(listener) }
    }

    /**
     * Admin: Remotely toggle activation for ANY device
     */
    fun setRemoteDeviceActivation(targetDeviceId: String, activated: Boolean) {
        try {
            dbInstance?.getReference("devices")?.child(targetDeviceId)?.child("isActivated")?.setValue(activated)
            if (targetDeviceId == getDeviceId()) {
                prefs.edit().putBoolean(KEY_IS_ACTIVATED, activated).apply()
                _isActivated.value = activated
            }
        } catch (e: Exception) {
            // Ignore
        }
    }

    /**
     * Admin: Set custom label for a device (e.g. "Kassa 1", "Kassa Filial")
     */
    fun setRemoteDeviceCustomName(targetDeviceId: String, name: String) {
        try {
            dbInstance?.getReference("devices")?.child(targetDeviceId)?.child("customName")?.setValue(name)
            if (targetDeviceId == getDeviceId()) {
                prefs.edit().putString(KEY_CUSTOM_DEVICE_NAME, name).apply()
            }
        } catch (e: Exception) {
            // Ignore
        }
    }
}
