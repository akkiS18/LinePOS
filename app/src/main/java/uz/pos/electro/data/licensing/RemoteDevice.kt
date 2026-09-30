package uz.pos.electro.data.licensing

data class RemoteDevice(
    val deviceId: String = "",
    val deviceModel: String = "",
    val isActivated: Boolean = false,
    val lastActive: Long = 0L,
    val customName: String = "",
    val appVersion: String = "1.0.0"
)
