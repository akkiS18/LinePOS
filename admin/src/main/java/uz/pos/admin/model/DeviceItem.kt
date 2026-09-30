package uz.pos.admin.model

data class DeviceItem(
    val id: String = "",
    val model: String = "Noma'lum qurilma",
    val customName: String = "",
    val isActivated: Boolean = false,
    val lastActive: Long = 0L,
    val appVersion: String = "1.0.0"
) {
    val displayName: String
        get() = if (customName.isNotBlank()) customName else model

    val isOnline: Boolean
        get() = (System.currentTimeMillis() - lastActive) < 5 * 60 * 1000 // 5 daqiqa ichida faol bo'lgan bo'lsa
}
