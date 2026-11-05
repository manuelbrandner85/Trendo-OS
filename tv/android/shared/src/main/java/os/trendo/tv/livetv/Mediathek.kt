package os.trendo.tv.livetv

import org.json.JSONArray
import org.json.JSONObject
import java.net.HttpURLConnection
import java.net.URL

/** Ein Beitrag aus den öffentlich-rechtlichen Mediatheken (über MediathekViewWeb, frei und ohne Konto). */
data class MtItem(
    val channel: String, val topic: String, val title: String, val description: String,
    val timestamp: Long, val durationSec: Int, val url: String, val urlHd: String?, val website: String?,
)

/** Ein Sender in den Mediatheken: Anzeigename und Kennung bei MediathekViewWeb. */
data class Broadcaster(val name: String, val key: String, val color: Long)

object Mediathek {
    const val API = "https://mediathekviewweb.de/api/query"

    val main = listOf(
        Broadcaster("ARD", "ARD", 0xFF1A4F9C), Broadcaster("ZDF", "ZDF", 0xFFFA7D19), Broadcaster("arte", "ARTE.DE", 0xFFE5501E),
        Broadcaster("3sat", "3Sat", 0xFFD6001C), Broadcaster("phoenix", "PHOENIX", 0xFF3A7BD5), Broadcaster("KiKA", "KiKA", 0xFF7BC043),
    )
    val regional = listOf(
        Broadcaster("BR", "BR", 0xFF0A78C8), Broadcaster("WDR", "WDR", 0xFF00346E), Broadcaster("NDR", "NDR", 0xFF0C4DA2),
        Broadcaster("MDR", "MDR", 0xFF1D3C78), Broadcaster("SWR", "SWR", 0xFF1E6FBA), Broadcaster("HR", "HR", 0xFF0065A4),
        Broadcaster("rbb", "rbb", 0xFFD4002D), Broadcaster("SR", "SR", 0xFF0057A6), Broadcaster("Radio Bremen", "Radio Bremen TV", 0xFF2E8B57),
    )

    /** Rubriken innerhalb eines Senders bzw. über alle Sender. Suchwort leer = „Neu". */
    val categories = listOf("Neu" to "", "Filme" to "film", "Serien" to "serie", "Dokus" to "doku", "Krimis" to "krimi")

    /** Anfrage im Format von MediathekViewWeb (mehrere Abfragen = UND). */
    fun queryJson(channelKey: String?, text: String, minSec: Int, size: Int, offset: Int = 0): String {
        val q = JSONArray()
        if (channelKey != null) q.put(JSONObject().put("fields", JSONArray(listOf("channel"))).put("query", channelKey.lowercase()))
        if (text.isNotBlank()) q.put(JSONObject().put("fields", JSONArray(listOf("topic", "title"))).put("query", text.lowercase()))
        return JSONObject().put("queries", q).put("sortBy", "timestamp").put("sortOrder", "desc").put("future", false)
            .put("offset", offset).put("size", size).put("duration_min", minSec).toString()
    }

    fun parse(json: String): List<MtItem> {
        val arr = runCatching { JSONObject(json).getJSONObject("result").getJSONArray("results") }.getOrNull() ?: return emptyList()
        return (0 until arr.length()).mapNotNull { i ->
            val o = arr.getJSONObject(i)
            val url = o.optString("url_video").ifBlank { return@mapNotNull null }
            MtItem(o.optString("channel"), o.optString("topic"), o.optString("title"), o.optString("description").take(500),
                o.optLong("timestamp") * 1000, o.optInt("duration"), url, o.optString("url_video_hd").ifBlank { null }, o.optString("url_website").ifBlank { null })
        }.distinctBy { it.title + it.topic }
    }

    /** Lädt Beiträge (Netz). Leere Liste bei Fehler oder offline. */
    fun load(channelKey: String?, text: String, minSec: Int = 1200, size: Int = 40): List<MtItem> = runCatching {
        val c = URL(API).openConnection() as HttpURLConnection
        c.requestMethod = "POST"; c.doOutput = true; c.connectTimeout = 8000; c.readTimeout = 15000
        c.setRequestProperty("Content-Type", "text/plain"); c.setRequestProperty("User-Agent", "TrendoOS-TV")
        c.outputStream.use { it.write(queryJson(channelKey, text, minSec, size).toByteArray()) }
        try { parse(c.inputStream.bufferedReader().use { it.readText() }) } finally { c.disconnect() }
    }.getOrDefault(emptyList())

    fun minutes(sec: Int) = if (sec >= 3600) "${sec / 3600} Std. ${(sec % 3600) / 60} Min." else "${sec / 60} Min."
}

/** Ein Streamingdienst als Kachel: öffnet die offizielle App, sonst den Play Store. */
data class StreamService(val name: String, val keys: List<String>, val free: Boolean, val note: String)

object StreamServices {
    val freeWithAds = listOf(
        StreamService("Pluto TV", listOf("pluto"), true, "Kostenlose Sender und Filme mit Werbung"),
        StreamService("Rakuten TV", listOf("rakuten"), true, "Kostenloser Bereich mit Werbung"),
        StreamService("Plex", listOf("plex"), true, "Kostenlose Filme mit Werbung"),
        StreamService("Joyn", listOf("joyn"), true, "Sender und Mediathek, kostenlos mit Werbung"),
    )
    val paid = listOf(
        StreamService("Netflix", listOf("netflix"), false, "Abo"), StreamService("Disney+", listOf("disney"), false, "Abo"),
        StreamService("Prime Video", listOf("prime video", "amazon"), false, "Abo"), StreamService("Apple TV", listOf("apple tv"), false, "Abo"),
        StreamService("DAZN", listOf("dazn"), false, "Sport-Abo"), StreamService("WOW", listOf("wow"), false, "Abo"),
        StreamService("MagentaTV", listOf("magenta"), false, "Abo"), StreamService("RTL+", listOf("rtl+", "rtl plus"), false, "Abo, teils kostenlos"),
        StreamService("Paramount+", listOf("paramount"), false, "Abo"),
    )
    /** Offizielle Mediathek-Apps (Erkennung am angezeigten Namen). */
    val mediathekApps = mapOf("ARD" to listOf("ard"), "ZDF" to listOf("zdf"), "arte" to listOf("arte"), "3sat" to listOf("3sat"),
        "phoenix" to listOf("phoenix"), "KiKA" to listOf("kika"))

    /** Passende installierte App am Namen finden (keine geratenen Paket-IDs). */
    fun match(keys: List<String>, installed: List<Pair<String, String>>): String? =
        installed.firstOrNull { (_, label) -> val l = label.lowercase(); keys.any { k -> l == k || l.startsWith(k) || l.contains(k) } }?.first
}
