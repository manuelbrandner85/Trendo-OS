package os.trendo.tv.livetv

import java.io.BufferedInputStream
import java.io.InputStream
import java.net.HttpURLConnection
import java.net.URL
import java.util.zip.GZIPInputStream

/** Lädt Senderlisten, Programmführer, Filme und Serien aus den eigenen Quellen des Nutzers. */
class LiveRepo(private val store: LiveStore) {

    data class Report(val channels: Int, val programmes: Int, val vod: Int, val series: Int, val errors: List<String>)

    private fun open(url: String, timeoutMs: Int = 20_000): InputStream {
        val c = URL(url).openConnection() as HttpURLConnection
        c.connectTimeout = timeoutMs; c.readTimeout = 60_000
        c.setRequestProperty("User-Agent", "TrendoOS-TV")
        c.setRequestProperty("Accept-Encoding", "gzip")
        val code = c.responseCode
        if (code !in 200..299) { c.disconnect(); throw IllegalStateException("Server antwortet mit $code") }
        val raw = BufferedInputStream(c.inputStream)
        raw.mark(2)
        val b0 = raw.read(); val b1 = raw.read(); raw.reset()
        return if (b0 == 0x1f && b1 == 0x8b) GZIPInputStream(raw) else raw
    }

    private fun text(url: String) = open(url).bufferedReader().use { it.readText() }

    /** Neue Quelle anlegen (Passwort landet nur verschlüsselt im SecretBox). */
    fun addM3u(name: String, url: String, epg: String?): Source {
        val s = Source("m" + System.currentTimeMillis().toString(36), name.ifBlank { "Meine Liste" }, SourceKind.M3U, url.trim(), null, epg?.trim()?.ifBlank { null })
        store.saveSources(store.sources() + s); return s
    }

    fun addXtream(name: String, server: String, user: String, pass: String): Source {
        val s = Source("x" + System.currentTimeMillis().toString(36), name.ifBlank { "Mein Anbieter" }, SourceKind.XTREAM, Xtream.normalize(server), user.trim(), null)
        store.saveSources(store.sources() + s); store.secrets.put(s.id, pass); return s
    }

    /** Alles neu laden. Fehler je Quelle werden gesammelt (ohne Zugangsdaten im Text). */
    fun refresh(now: Long = System.currentTimeMillis(), withEpg: Boolean = true): Report {
        val errors = ArrayList<String>()
        var vodN = 0; var serN = 0
        val epgUrls = LinkedHashSet<String>()
        for (s in store.sources()) {
            try {
                when (s.kind) {
                    SourceKind.M3U -> {
                        val r = M3u.parse(text(s.url), s.id)
                        store.saveChannels(s.id, r.channels)
                        (s.epgUrl ?: r.epgUrl)?.let { epgUrls += it }
                    }
                    SourceKind.XTREAM -> {
                        val pass = store.secrets.get(s.id) ?: throw IllegalStateException("Passwort fehlt")
                        val u = s.user ?: ""
                        store.saveChannels(s.id, Xtream.parseLive(text(Xtream.api(s.url, u, pass, "get_live_streams")),
                            text(Xtream.api(s.url, u, pass, "get_live_categories")), s.url, u, pass, s.id))
                        runCatching {
                            val v = Xtream.parseVod(text(Xtream.api(s.url, u, pass, "get_vod_streams")), text(Xtream.api(s.url, u, pass, "get_vod_categories")), s.url, u, pass, s.id)
                            store.saveVod(s.id, v); vodN += v.size
                        }
                        runCatching {
                            val v = Xtream.parseSeries(text(Xtream.api(s.url, u, pass, "get_series")), text(Xtream.api(s.url, u, pass, "get_series_categories")), s.id)
                            store.saveSeries(s.id, v); serN += v.size
                        }
                        epgUrls += Xtream.epgUrl(s.url, u, pass)
                    }
                }
            } catch (e: Exception) {
                errors += "${s.name}: ${safeMessage(e)}"
            }
        }
        val channels = store.channels()
        var progs = 0
        if (withEpg && epgUrls.isNotEmpty()) {
            val wanted = channels.mapNotNull { it.epgId }.toSet()
            val all = ArrayList<Programme>()
            for (u in epgUrls) runCatching {
                open(u, 30_000).use { all += Xmltv.parse(it, wanted, now - 6 * 3_600_000L, now + 48 * 3_600_000L) }
            }.onFailure { errors += "Programmführer: ${safeMessage(it)}" }
            store.saveEpg(all); progs = all.size
        }
        return Report(channels.size, progs, vodN, serN, errors)
    }

    fun episodes(series: Series): List<Episode> {
        val s = store.sources().firstOrNull { it.id == series.sourceId } ?: return emptyList()
        val pass = store.secrets.get(s.id) ?: return emptyList()
        return runCatching {
            Xtream.parseEpisodes(text(Xtream.api(s.url, s.user ?: "", pass, "get_series_info", "&series_id=" + Xtream.rawSeriesId(series.id))), series.id, s.url, s.user ?: "", pass)
        }.getOrDefault(emptyList())
    }

    /** Fehlermeldung ohne Adressen (darin stünden Benutzername/Passwort). */
    private fun safeMessage(e: Throwable): String = when (e) {
        is java.net.UnknownHostException -> "Server nicht gefunden"
        is java.net.SocketTimeoutException -> "Zeitüberschreitung"
        is java.net.ConnectException -> "keine Verbindung"
        else -> (e.message ?: e.javaClass.simpleName).replace(Regex("""https?://\S+"""), "<Adresse>")
    }
}
