package uz.pos.admin.data

import com.google.firebase.database.DataSnapshot
import com.google.firebase.database.DatabaseError
import com.google.firebase.database.FirebaseDatabase
import com.google.firebase.database.ValueEventListener
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.callbackFlow
import uz.pos.admin.model.DeviceItem
import uz.pos.admin.util.AdminLogger

class AdminRepository {

    private val dbInstance: FirebaseDatabase? by lazy {
        try {
            val db = FirebaseDatabase.getInstance("https://line-pos-56296-default-rtdb.firebaseio.com")
            AdminLogger.s("Firebase", "Firebase RTDB ulandi: line-pos-56296-default-rtdb.firebaseio.com")
            db
        } catch (t: Throwable) {
            AdminLogger.w("Firebase", "Maxsus URL bilan ulanib bo'lmadi (${t.message}), default instance ishlatilmoqda...")
            try {
                val db = FirebaseDatabase.getInstance()
                AdminLogger.s("Firebase", "Standart Firebase instance ulandi")
                db
            } catch (e: Throwable) {
                AdminLogger.e("Firebase", "Firebase instansiyasini yuklab bo'lmadi", e)
                null
            }
        }
    }

    private val _errorState = MutableStateFlow<String?>(null)
    val errorState: StateFlow<String?> = _errorState

    /**
     * Barcha ulangan qurilmalarni real vaqt rejimida (realtime) kuzatish
     * Flow faqat bir marta yaratiladi va barcha chaqiriqlar uchun keshlanadi
     */
    private val _devicesFlow: Flow<List<DeviceItem>> by lazy {
        callbackFlow {
            AdminLogger.i("Qurilmalar", "Qurilmalar ro'yxati tinglanmoqda...")
            val database = dbInstance
            if (database == null) {
                val err = "Firebase ma'lumotlar bazasi instansiyasi topilmadi"
                _errorState.value = err
                AdminLogger.e("Qurilmalar", err)
                trySend(emptyList())
                close()
                return@callbackFlow
            }

            val devicesRef = database.getReference("devices")
            val listener = object : ValueEventListener {
                override fun onDataChange(snapshot: DataSnapshot) {
                    _errorState.value = null
                    val list = mutableListOf<DeviceItem>()
                    for (child in snapshot.children) {
                        val id = child.key ?: continue
                        val model = child.child("deviceModel").getValue(String::class.java) ?: "Noma'lum"
                        val customName = child.child("customName").getValue(String::class.java) ?: ""
                        val isActivated = child.child("isActivated").getValue(Boolean::class.java) ?: false
                        val lastActive = child.child("lastActive").getValue(Long::class.java) ?: 0L
                        val appVersion = child.child("appVersion").getValue(String::class.java) ?: "1.0.0"

                        list.add(
                            DeviceItem(
                                id = id,
                                model = model,
                                customName = customName,
                                isActivated = isActivated,
                                lastActive = lastActive,
                                appVersion = appVersion
                            )
                        )
                    }
                    list.sortByDescending { it.lastActive }
                    trySend(list)
                }

                override fun onCancelled(error: DatabaseError) {
                    val msg = "Firebase ruxsat xatosi: ${error.message} (Code: ${error.code}). Firebase Console -> Realtime Database -> Rules bo'limida .read: true qiling!"
                    android.util.Log.e("AdminRepository", "Firebase onCancelled: ${error.message}")
                    _errorState.value = msg
                    AdminLogger.e("Qurilmalar", msg)
                }
            }

            devicesRef.addValueEventListener(listener)
            awaitClose { devicesRef.removeEventListener(listener) }
        }
    }

    fun getDevicesFlow(): Flow<List<DeviceItem>> = _devicesFlow

    /**
     * Yangi qurilmalar uchun global aktivatsiya kodini real vaqtda kuzatish (default 1984)
     */
    private val _globalCodeFlow: Flow<String> by lazy {
        callbackFlow {
            AdminLogger.i("AktivatsiyaKodi", "settings/globalCode tinglanmoqda...")
            val database = dbInstance
            if (database == null) {
                trySend("1984")
                close()
                return@callbackFlow
            }

            val codeRef = database.getReference("settings").child("globalCode")
            val listener = object : ValueEventListener {
                override fun onDataChange(snapshot: DataSnapshot) {
                    val code = snapshot.getValue(String::class.java) ?: "1984"
                    trySend(code)
                }

                override fun onCancelled(error: DatabaseError) {
                    val msg = "Aktivatsiya kodini o'qishda xatolik: ${error.message}. Firebase Console -> Rules'da .read: true bo'lishi shart."
                    AdminLogger.e("AktivatsiyaKodi", msg)
                }
            }

            codeRef.addValueEventListener(listener)
            awaitClose { codeRef.removeEventListener(listener) }
        }
    }

    fun getGlobalCodeFlow(): Flow<String> = _globalCodeFlow

    /**
     * Qurilmani masofadan turib faollashtirish yoki bloklash
     */
    fun toggleDeviceActivation(deviceId: String, isActivated: Boolean, onComplete: ((Boolean, String?) -> Unit)? = null) {
        val actionText = if (isActivated) "faollashtirish" else "bloklash"
        AdminLogger.i("Qurilmalar", "$deviceId ni $actionText so'rovi yuborilmoqda...")
        val database = dbInstance ?: run {
            val err = "Firebase ulanishi mavjud emas"
            AdminLogger.e("Qurilmalar", err)
            onComplete?.invoke(false, err)
            return
        }
        database.getReference("devices").child(deviceId).child("isActivated")
            .setValue(isActivated)
            .addOnCompleteListener { task ->
                if (task.isSuccessful) {
                    AdminLogger.s("Qurilmalar", "$deviceId muvaffaqiyatli ${if (isActivated) "faollashtirildi" else "bloklandi"}")
                    onComplete?.invoke(true, null)
                } else {
                    val err = task.exception?.localizedMessage ?: "Noma'lum xatolik"
                    AdminLogger.e("Qurilmalar", "$deviceId holatini o'zgartirishda xatolik: $err")
                    onComplete?.invoke(false, err)
                }
            }
    }

    /**
     * Qurilmani ro'yxatdan o'chirish
     */
    fun deleteDevice(deviceId: String, onComplete: ((Boolean, String?) -> Unit)? = null) {
        AdminLogger.i("Qurilmalar", "$deviceId ni o'chirish so'rovi yuborilmoqda...")
        val database = dbInstance ?: run {
            val err = "Firebase ulanishi mavjud emas"
            AdminLogger.e("Qurilmalar", err)
            onComplete?.invoke(false, err)
            return
        }
        database.getReference("devices").child(deviceId)
            .removeValue()
            .addOnCompleteListener { task ->
                if (task.isSuccessful) {
                    AdminLogger.s("Qurilmalar", "$deviceId o'chirildi")
                    onComplete?.invoke(true, null)
                } else {
                    val err = task.exception?.localizedMessage ?: "Noma'lum xatolik"
                    AdminLogger.e("Qurilmalar", "$deviceId ni o'chirishda xatolik: $err")
                    onComplete?.invoke(false, err)
                }
            }
    }



    /**
     * Global aktivatsiya kodini yangilash
     */
    fun updateGlobalCode(newCode: String, onComplete: (Boolean, String?) -> Unit) {
        val trimmed = newCode.trim()
        AdminLogger.i("AktivatsiyaKodi", "Yangi kod saqlanmoqda: '$trimmed'...")
        val database = dbInstance ?: run {
            val err = "Firebase ulanishi topilmadi"
            AdminLogger.e("AktivatsiyaKodi", err)
            onComplete(false, err)
            return
        }
        database.getReference("settings").child("globalCode")
            .setValue(trimmed)
            .addOnCompleteListener { task ->
                if (task.isSuccessful) {
                    AdminLogger.s("AktivatsiyaKodi", "Yangi aktivatsiya kodi Firebase'ga saqlandi: '$trimmed' ✅")
                    onComplete(true, null)
                } else {
                    val err = task.exception?.localizedMessage ?: "Noma'lum xatolik"
                    AdminLogger.e("AktivatsiyaKodi", "Kodni saqlashda xatolik: $err. Firebase Console -> Rules bo'limida .write: true qiling!", task.exception)
                    onComplete(false, err)
                }
            }
    }

    /**
     * Firebase ulanishini test qilish (Ping)
     */
    fun pingFirebase(onResult: (Boolean, String) -> Unit) {
        AdminLogger.i("Test", "Firebase ulanishi tekshirilmoqda...")
        val database = dbInstance ?: run {
            val msg = "Firebase instansiyasi mavjud emas"
            AdminLogger.e("Test", msg)
            onResult(false, msg)
            return
        }
        database.getReference(".info/connected").addListenerForSingleValueEvent(object : ValueEventListener {
            override fun onDataChange(snapshot: DataSnapshot) {
                val connected = snapshot.getValue(Boolean::class.java) ?: false
                if (connected) {
                    val msg = "Firebase serveri bilan aloqa bor (Connected = true)"
                    AdminLogger.s("Test", msg)
                    onResult(true, msg)
                } else {
                    val msg = "Firebase serveriga ulanish kutilmoqda (Connected = false)"
                    AdminLogger.w("Test", msg)
                    onResult(false, msg)
                }
            }

            override fun onCancelled(error: DatabaseError) {
                val msg = "Firebase ulanish xatosi: ${error.message}"
                AdminLogger.e("Test", msg)
                onResult(false, msg)
            }
        })
    }
}
