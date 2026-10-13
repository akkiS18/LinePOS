package uz.pos.electro.data.sync

import android.content.Context
import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkCapabilities
import android.os.SystemClock
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import org.json.JSONObject
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.Inet4Address
import java.net.InetAddress
import java.net.SocketTimeoutException
import java.security.MessageDigest
import java.util.UUID
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec

/** Local IPv4 discovery only. Never broadcasts a bearer token or financial data. */
internal class LanDesktopDiscovery(context: Context) {
    private val connectivity = context.getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager

    private fun networks(): List<Network> = connectivity.allNetworks.filter { network ->
        val caps = connectivity.getNetworkCapabilities(network)
        caps != null && !caps.hasTransport(NetworkCapabilities.TRANSPORT_VPN) &&
            (caps.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) || caps.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET))
    }

    fun networkFor(address: InetAddress): Network? = networks().firstOrNull { network ->
        connectivity.getLinkProperties(network)?.linkAddresses?.any { link ->
            val left = link.address.address
            val right = address.address
            left.size == right.size && link.prefixLength > 0 && (0 until link.prefixLength).all { bit ->
                ((left[bit / 8].toInt() xor right[bit / 8].toInt()) and (1 shl (7 - bit % 8))) == 0
            }
        } == true
    }

    suspend fun find(serverId: String, deviceId: String, token: String): String? {
        val nonce = UUID.randomUUID().toString()
        val key = MessageDigest.getInstance("SHA-256").digest(token.toByteArray(Charsets.UTF_8))
        val request = JSONObject().put("kind", "linepos-discover-v1").put("serverId", serverId)
            .put("deviceId", deviceId).put("nonce", nonce).toString().toByteArray(Charsets.UTF_8)
        for (network in networks()) {
            currentCoroutineContext().ensureActive()
            val links = connectivity.getLinkProperties(network)?.linkAddresses.orEmpty()
                .filter { it.address is Inet4Address && it.prefixLength in 1..30 }
            if (links.isEmpty()) continue
            try {
                DatagramSocket().use { socket ->
                    network.bindSocket(socket)
                    socket.broadcast = true
                    socket.soTimeout = 250
                    val targets = links.map { link ->
                        val bytes = link.address.address.copyOf()
                        for (bit in link.prefixLength until 32) {
                            bytes[bit / 8] = (bytes[bit / 8].toInt() or (1 shl (7 - bit % 8))).toByte()
                        }
                        InetAddress.getByAddress(bytes)
                    }.toSet()
                    val until = SystemClock.elapsedRealtime() + 1800
                    var nextSend = 0L
                    while (SystemClock.elapsedRealtime() < until) {
                        currentCoroutineContext().ensureActive()
                        if (SystemClock.elapsedRealtime() >= nextSend) {
                            for (target in targets) socket.send(DatagramPacket(request, request.size, target, 8080))
                            nextSend = SystemClock.elapsedRealtime() + 600
                        }
                        val packet = DatagramPacket(ByteArray(2048), 2048)
                        try { socket.receive(packet) } catch (_: SocketTimeoutException) { continue }
                        if (packet.port != 8080 || networkFor(packet.address) != network) continue
                        val reply = runCatching { JSONObject(String(packet.data, 0, packet.length, Charsets.UTF_8)) }.getOrNull() ?: continue
                        if (reply.optString("kind") != "linepos-discover-v1" || reply.optString("nonce") != nonce ||
                            reply.optString("serverId") != serverId || reply.optInt("port") != 8080) continue
                        val proof = reply.optString("proof")
                        if (!proof.matches(Regex("[0-9A-Fa-f]{64}"))) continue
                        val supplied = ByteArray(32) { i -> proof.substring(i * 2, i * 2 + 2).toInt(16).toByte() }
                        val mac = Mac.getInstance("HmacSHA256")
                        mac.init(SecretKeySpec(key, "HmacSHA256"))
                        val expected = mac.doFinal("linepos-discover-v1\n$nonce\n$serverId\n8080".toByteArray(Charsets.UTF_8))
                        if (MessageDigest.isEqual(expected, supplied)) return "http://${packet.address.hostAddress}:8080"
                    }
                }
            } catch (_: java.io.IOException) {
                // A network can disappear during discovery. The next retry uses fresh links.
            }
        }
        return null
    }
}
