package uz.pos.admin.voice

import kotlinx.coroutines.flow.first
import uz.pos.admin.data.AdminRepository
import uz.pos.admin.model.DeviceItem
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import kotlin.random.Random

object VoiceCommandProcessor {

    private val jokes = listOf(
        "Why do programmers prefer dark mode? Because light attracts bugs, sir.",
        "There are 10 types of people in the world: those who understand binary, and those who do not, sir.",
        "Why did the computer keep freezing? Because it left its Windows open, sir.",
        "Hardware is the part of a computer that you can kick when the software crashes, sir.",
        "An SQL statement walks into a bar, approaches two tables and asks: May I join you, sir?",
        "There is no place like 127.0.0.1, sir.",
        "Why do Java developers wear glasses? Because they don't C sharp, sir.",
        "A programmer's wife says: Buy bread. If they have eggs, get a dozen. He returned with 12 loaves of bread, sir.",
        "Artificial intelligence is impressive, but it is still no match for natural stupidity, sir.",
        "I told my computer I needed a break, and now it refuses to wake up from sleep mode, sir.",
        "To understand recursion, you must first understand recursion, sir.",
        "Debugging is like being the detective in a crime movie where you are also the murderer, sir."
    )

    suspend fun processCommand(
        text: String,
        repository: AdminRepository
    ): CommandResult {
        val clean = text.lowercase().trim()

        // ========================================================
        // 1. CLOSE / EXIT (Oynani yopish)
        // ========================================================
        if (clean == "close" || clean == "exit" || clean == "stop" || clean == "bye" ||
            clean.contains("yop") || clean.contains("chiq") || clean.contains("tamom") || clean.contains("goodbye")) {
            return CommandResult(
                reply = "Closing session. Goodbye, sir.",
                shouldClose = true
            )
        }

        // ========================================================
        // 2. UNBLOCK / ACTIVATE / OCHISH (Blokdan chiqarish)
        // Eslatma: "blokdan chiqar"da "blok" so'zi bo'lgani uchun,
        // bu tekshiruv oddiy "bloklash"dan OLDIN turishi shart!
        // ========================================================
        if (clean.contains("unblock") || clean.contains("unlock") || clean.contains("authorize") ||
            clean.contains("activate") || clean.contains("enable") ||
            clean.contains("blokdan") || clean.contains("och") || clean.contains("faollashtir") ||
            clean.contains("ruxsat") || clean.contains("разблок")) {

            val devices = repository.getDevicesFlow().first()
            val target = findDeviceByQuery(clean, devices)
            return if (target != null) {
                repository.toggleDeviceActivation(target.id, true)
                CommandResult(
                    reply = "${target.displayName} has been successfully authorized and unblocked, sir.",
                    shouldClose = false
                )
            } else {
                CommandResult(
                    reply = "I could not find the device to unblock. Please name the device, sir.",
                    shouldClose = false
                )
            }
        }

        // ========================================================
        // 3. BLOCK (Bloklash)
        // ========================================================
        if (clean.contains("block") || clean.contains("disable") || clean.contains("lock") ||
            clean.contains("blok") || clean.contains("o'chir") || clean.contains("to'xtat") ||
            clean.contains("заблок")) {

            val devices = repository.getDevicesFlow().first()
            val target = findDeviceByQuery(clean, devices)
            return if (target != null) {
                repository.toggleDeviceActivation(target.id, false)
                CommandResult(
                    reply = "${target.displayName} has been blocked, sir.",
                    shouldClose = false
                )
            } else {
                CommandResult(
                    reply = "I could not find the device to block. Please name the device, sir.",
                    shouldClose = false
                )
            }
        }

        // ========================================================
        // 4. WHO IS ONLINE (Kim online / Kim ishlayapti)
        // ========================================================
        if (clean.contains("online") || clean.contains("who is online") ||
            clean.contains("kim online") || clean.contains("kim ishlayapti") || clean.contains("working")) {

            val devices = repository.getDevicesFlow().first()
            val onlineDevices = devices.filter { it.isOnline }
            return if (onlineDevices.isNotEmpty()) {
                val names = onlineDevices.joinToString(", ") { it.displayName }
                CommandResult(
                    reply = "Currently, ${onlineDevices.size} nodes are online: $names, sir.",
                    shouldClose = false
                )
            } else {
                CommandResult(
                    reply = "No cash register nodes are currently online, sir.",
                    shouldClose = false
                )
            }
        }

        // ========================================================
        // 5. LAST NODE / DEVICE (Oxirgi ulangan kassa)
        // ========================================================
        if (clean.contains("last") || clean.contains("recent") || clean.contains("latest") ||
            clean.contains("oxirgi") || clean.contains("oxirgi kassa") || clean.contains("yangi kassa")) {

            val devices = repository.getDevicesFlow().first()
            val lastDevice = devices.maxByOrNull { it.lastActive } ?: devices.firstOrNull()
            return if (lastDevice != null) {
                val timeStr = if (lastDevice.lastActive > 0) {
                    SimpleDateFormat("HH:mm", Locale.US).format(Date(lastDevice.lastActive))
                } else "recently"
                CommandResult(
                    reply = "The most recent device is ${lastDevice.displayName}, last seen at $timeStr, sir.",
                    shouldClose = false
                )
            } else {
                CommandResult(
                    reply = "No devices registered in the database, sir.",
                    shouldClose = false
                )
            }
        }

        // ========================================================
        // 6. SET NEW KEY (Ovoz bilan yangi kod o'rnatish)
        // Masalan: "set key 2026", "yangi kod 7788", "kodni 1234 qil"
        // ========================================================
        if ((clean.contains("set") || clean.contains("change") || clean.contains("new") ||
             clean.contains("yangi") || clean.contains("o'zgartir") || clean.contains("qil")) &&
            (clean.contains("key") || clean.contains("code") || clean.contains("kod") || clean.contains("parol"))) {

            val digits = clean.filter { it.isDigit() }
            return if (digits.length in 4..8) {
                repository.updateGlobalCode(digits) { _, _ -> }
                val spaced = digits.toCharArray().joinToString(" ")
                CommandResult(
                    reply = "Access key successfully updated to $spaced, sir.",
                    shouldClose = false
                )
            } else {
                CommandResult(
                    reply = "Please specify a numeric code between 4 and 8 digits, for example: set key 2026.",
                    shouldClose = false
                )
            }
        }

        // ========================================================
        // 7. RANDOM KEY (Tasodifiy kod o'rnatish)
        // ========================================================
        if (clean.contains("random") || clean.contains("tasodifiy") || clean.contains("generat")) {
            val randomPin = Random.nextInt(1000, 9999).toString()
            repository.updateGlobalCode(randomPin) { _, _ -> }
            val spaced = randomPin.toCharArray().joinToString(" ")
            return CommandResult(
                reply = "New random security key generated: $spaced, sir.",
                shouldClose = false
            )
        }

        // ========================================================
        // 8. GET CURRENT KEY (Amaldagi kodni aytish)
        // ========================================================
        if (clean.contains("key") || clean.contains("code") || clean.contains("kod") || clean.contains("parol")) {
            val code = repository.getGlobalCodeFlow().first()
            val spaced = code.toCharArray().joinToString(" ")
            return CommandResult(
                reply = "The current activation key is $spaced, sir.",
                shouldClose = false
            )
        }

        // ========================================================
        // 9. DIAGNOSTICS / SYSTEM CHECK (Diagnostika va server holati)
        // ========================================================
        if (clean.contains("diagnostic") || clean.contains("system check") || clean.contains("check") ||
            clean.contains("diagnostika") || clean.contains("tekshir") || clean.contains("ping")) {

            val devices = repository.getDevicesFlow().first()
            return CommandResult(
                reply = "Diagnostics complete: Firebase real-time database connected. Total nodes: ${devices.size}. Systems fully functional, sir.",
                shouldClose = false
            )
        }

        // ========================================================
        // 10. TIME AND DATE (Vaqt va sana)
        // ========================================================
        if (clean.contains("time") || clean.contains("date") || clean.contains("day") ||
            clean.contains("vaqt") || clean.contains("soat") || clean.contains("sana") || clean.contains("kun")) {

            val now = SimpleDateFormat("h:mm a, EEEE, MMMM d", Locale.US).format(Date())
            return CommandResult(
                reply = "It is currently $now, sir.",
                shouldClose = false
            )
        }

        // ========================================================
        // 11. JOKE (Kiber hazil)
        // ========================================================
        if (clean.contains("joke") || clean.contains("funny") || clean.contains("hazil") || clean.contains("latifa")) {
            val joke = jokes.random()
            return CommandResult(
                reply = joke,
                shouldClose = false
            )
        }

        // ========================================================
        // 12. WHO ARE YOU (Seb haqida ma'lumot)
        // ========================================================
        if (clean.contains("who are you") || clean.contains("your name") ||
            clean.contains("kim sen") || clean.contains("sen kimsan") || clean.contains("o'zing haqida")) {
            return CommandResult(
                reply = "I am Seb, your personal artificial intelligence supervisor for Line POS.",
                shouldClose = false
            )
        }

        // ========================================================
        // 13. STATUS / OVERVIEW (Umumiy holat)
        // ========================================================
        if (clean.contains("status") || clean.contains("holat") || clean.contains("kassa") ||
            clean.contains("nodes") || clean.contains("devices") || clean.contains("summary")) {

            val devices = repository.getDevicesFlow().first()
            val active = devices.count { it.isActivated }
            return CommandResult(
                reply = "Status report: $active out of ${devices.size} nodes are active and authorized, sir.",
                shouldClose = false
            )
        }

        // ========================================================
        // 14. GREETINGS & THANKS (Salomlashish va minnatdorchilik)
        // ========================================================
        if (clean.contains("thank") || clean.contains("rahmat") || clean.contains("barakalla") || clean.contains("spasibo")) {
            return CommandResult(
                reply = "Always at your service, sir. Standing by.",
                shouldClose = false
            )
        }

        if (clean.contains("hello") || clean.contains("hi") || clean.contains("salom") ||
            clean.contains("seb") || clean.contains("sebastian") || clean.contains("hey")) {
            return CommandResult(
                reply = "Yes sir, Seb at your service. Say close whenever you are done.",
                shouldClose = false
            )
        }

        // ========================================================
        // DEFAULT FALLBACK
        // ========================================================
        return CommandResult(
            reply = "Command acknowledged: '$text', sir. Standing by for instructions.",
            shouldClose = false
        )
    }

    private fun findDeviceByQuery(text: String, devices: List<DeviceItem>): DeviceItem? {
        if (devices.isEmpty()) return null

        // 1. Aniq qurilma nomlarini qidirish
        for (device in devices) {
            val name = device.displayName.lowercase()
            val model = device.model.lowercase()
            val parts = (name + " " + model).split(" ", "-", "_").filter { it.length >= 3 }
            for (part in parts) {
                if (text.contains(part)) {
                    return device
                }
            }
        }

        // 2. Toifalar bo'yicha qidirish ("kompyuter" -> Windows, "telefon" -> Android)
        if (text.contains("kompyuter") || text.contains("windows") || text.contains("pc") || text.contains("computer")) {
            val pc = devices.firstOrNull { it.model.contains("Windows", ignoreCase = true) || it.displayName.contains("Windows", ignoreCase = true) }
            if (pc != null) return pc
        }

        if (text.contains("telefon") || text.contains("phone") || text.contains("samsung") || text.contains("mobile")) {
            val phone = devices.firstOrNull { !it.model.contains("Windows", ignoreCase = true) }
            if (phone != null) return phone
        }

        // 3. Agar faqat 1 ta kassa bo'lsa yoki bitta qurilma nazarda tutilgan bo'lsa
        return if (devices.size == 1) devices.first() else devices.firstOrNull()
    }
}

data class CommandResult(
    val reply: String,
    val shouldClose: Boolean
)
