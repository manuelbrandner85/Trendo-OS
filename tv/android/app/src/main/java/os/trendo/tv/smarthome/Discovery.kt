package os.trendo.tv.smarthome

import android.content.Context
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo

/** Ein im Heimnetz gefundenes Gerät (nur Name und Art – keine Steuerung ohne Freigabe). */
data class HomeDevice(val name: String, val kind: String, val type: String)

/**
 * Sucht Geräte im eigenen Heimnetz über mDNS (Bonjour). Nur solange die Seite offen ist,
 * danach wird die Suche beendet (kein Dauer-Scannen). Nichts wird gesteuert.
 */
class Discovery(context: Context, private val onChange: (List<HomeDevice>) -> Unit) {
    private val nsd = context.getSystemService(Context.NSD_SERVICE) as NsdManager
    private val found = LinkedHashMap<String, HomeDevice>()
    private val listeners = ArrayList<NsdManager.DiscoveryListener>()

    companion object {
        val TYPES = linkedMapOf(
            "_home-assistant._tcp." to "Home Assistant",
            "_hue._tcp." to "Philips Hue Bridge",
            "_hap._tcp." to "HomeKit-Gerät",
            "_googlecast._tcp." to "Chromecast / Google-Lautsprecher",
            "_spotify-connect._tcp." to "Spotify-Lautsprecher",
            "_airplay._tcp." to "AirPlay-Gerät",
            "_ipp._tcp." to "Drucker",
            "_smb._tcp." to "Netzwerkfreigabe (NAS)",
            "_shelly._tcp." to "Shelly",
            "_esphomelib._tcp." to "ESPHome",
        )
    }

    fun start() {
        for ((type, kind) in TYPES) {
            val l = object : NsdManager.DiscoveryListener {
                override fun onServiceFound(info: NsdServiceInfo) {
                    synchronized(found) { found["$type|${info.serviceName}"] = HomeDevice(info.serviceName, kind, type) }
                    onChange(synchronized(found) { found.values.toList() })
                }
                override fun onServiceLost(info: NsdServiceInfo) {
                    synchronized(found) { found.remove("$type|${info.serviceName}") }
                    onChange(synchronized(found) { found.values.toList() })
                }
                override fun onDiscoveryStarted(serviceType: String) = Unit
                override fun onDiscoveryStopped(serviceType: String) = Unit
                override fun onStartDiscoveryFailed(serviceType: String, errorCode: Int) = Unit
                override fun onStopDiscoveryFailed(serviceType: String, errorCode: Int) = Unit
            }
            runCatching { nsd.discoverServices(type, NsdManager.PROTOCOL_DNS_SD, l); listeners += l }
        }
    }

    fun stop() {
        listeners.forEach { runCatching { nsd.stopServiceDiscovery(it) } }
        listeners.clear()
    }
}
