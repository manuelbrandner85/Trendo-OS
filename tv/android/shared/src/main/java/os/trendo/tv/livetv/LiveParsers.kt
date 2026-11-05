package os.trendo.tv.livetv

import org.json.JSONArray
import org.json.JSONObject
import org.xmlpull.v1.XmlPullParser
import org.xmlpull.v1.XmlPullParserFactory
import java.io.InputStream
import java.net.URI
import java.net.URLDecoder
import java.net.URLEncoder
import java.util.Calendar
import java.util.TimeZone

/** M3U-Wiedergabelisten (wie `Trendo.Core.Tv.M3u`). */
object M3u {
    private val attr = Regex("""([A-Za-z0-9_-]+)="([^"]*)"""")

    data class Result(val channels: List<Channel>, val epgUrl: String?)

    fun parse(text: String, sourceId: String = "q"): Result {
        val list = ArrayList<Channel>()
        var epg: String? = null
        var attrs: Map<String, String>? = null
        var name: String? = null
        var grp: String? = null
        for (raw in text.split('\n')) {
            val line = raw.trim().trimStart('﻿')
            if (line.isEmpty()) continue
            when {
                line.startsWith("#EXTM3U", true) -> {
                    val h = attr.findAll(line).associate { it.groupValues[1].lowercase() to it.groupValues[2] }
                    epg = h["x-tvg-url"] ?: h["url-tvg"] ?: h["tvg-url"]
                    epg = epg?.split(',')?.firstOrNull()?.trim()?.ifEmpty { null }
                }
                line.startsWith("#EXTINF", true) -> {
                    attrs = attr.findAll(line).associate { it.groupValues[1].lowercase() to it.groupValues[2] }
                    name = line.substringAfterLast(',', "").trim().ifEmpty { attrs["tvg-name"] }
                    grp = attrs["group-title"]
                }
                line.startsWith("#EXTGRP:", true) -> grp = line.substringAfter(':').trim()
                line.startsWith("#") -> Unit
                else -> {
                    val a = attrs ?: emptyMap()
                    val n = name?.ifBlank { null } ?: a["tvg-name"] ?: "Sender ${list.size + 1}"
                    list += Channel(
                        // Stabile Kennung aus der Adresse (Reihenfolge in der Liste darf sich ändern).
                        id = "$sourceId:" + Integer.toHexString(line.hashCode()) + ":" + list.count { it.url == line }, name = n, url = line, group = grp?.ifBlank { null } ?: "Sender",
                        logo = a["tvg-logo"]?.ifBlank { null }, epgId = a["tvg-id"]?.ifBlank { null },
                        number = a["tvg-chno"]?.toIntOrNull() ?: (list.size + 1), sourceId = sourceId,
                        archiveDays = (a["catchup-days"] ?: a["tvg-rec"])?.toIntOrNull() ?: 0,
                        catchup = a["catchup-source"]?.ifBlank { null },
                    )
                    attrs = null; name = null; grp = null
                }
            }
        }
        return Result(list, epg)
    }
}

/** Xtream-Codes-Schnittstelle (wie `Trendo.Core.Tv.Xtream`). */
object Xtream {
    fun normalize(server: String): String {
        var s = server.trim().trimEnd('/')
        if (!s.startsWith("http", true)) s = "http://$s"
        return s
    }

    private fun e(s: String) = URLEncoder.encode(s, "UTF-8")

    fun playlistUrl(server: String, user: String, pass: String) =
        "${normalize(server)}/get.php?username=${e(user)}&password=${e(pass)}&type=m3u_plus&output=ts"

    fun api(server: String, user: String, pass: String, action: String, extra: String = "") =
        "${normalize(server)}/player_api.php?username=${e(user)}&password=${e(pass)}&action=$action$extra"

    fun epgUrl(server: String, user: String, pass: String) = "${normalize(server)}/xmltv.php?username=${e(user)}&password=${e(pass)}"

    /** Zugang aus einer get.php-Adresse zurückgewinnen (Server, Benutzer, Passwort). */
    fun fromPlaylistUrl(url: String): Triple<String, String, String>? = runCatching {
        val u = URI(url)
        if (!u.path.orEmpty().endsWith("get.php", true)) return null
        val q = u.rawQuery.orEmpty().split('&').mapNotNull { p -> p.split('=', limit = 2).takeIf { it.size == 2 } }
            .associate { it[0].lowercase() to URLDecoder.decode(it[1], "UTF-8") }
        val user = q["username"] ?: return null
        val pass = q["password"] ?: return null
        Triple("${u.scheme}://${u.authority}", user, pass)
    }.getOrNull()

    private fun categories(json: String): Map<String, String> {
        val arr = runCatching { JSONArray(json) }.getOrNull() ?: return emptyMap()
        return (0 until arr.length()).associate { i -> arr.getJSONObject(i).let { it.str("category_id") to it.str("category_name") } }
    }

    private fun JSONObject.str(k: String): String = if (has(k) && !isNull(k)) get(k).toString() else ""
    private fun JSONObject.ostr(k: String): String? = str(k).ifBlank { null }

    fun parseLive(streams: String, cats: String, server: String, user: String, pass: String, sourceId: String): List<Channel> {
        val c = categories(cats)
        val arr = runCatching { JSONArray(streams) }.getOrNull() ?: return emptyList()
        val s = normalize(server); val u = e(user); val p = e(pass)
        val out = ArrayList<Channel>()
        for (i in 0 until arr.length()) {
            val v = arr.getJSONObject(i)
            val id = v.str("stream_id"); if (id.isEmpty()) continue
            val archive = v.str("tv_archive") == "1"
            val days = v.str("tv_archive_duration").toIntOrNull() ?: 0
            // Stabile Kennung aus der Stream-Id: Favoriten/Verlauf bleiben bei jeder Aktualisierung erhalten.
            out += Channel("$sourceId:x$id", v.str("name"), "$s/live/$u/$p/$id.ts", c[v.str("category_id")] ?: "Sender",
                v.ostr("stream_icon"), v.ostr("epg_channel_id"), v.str("num").toIntOrNull() ?: (out.size + 1), sourceId,
                if (archive) maxOf(1, days) else 0, if (archive) "$s/timeshift/$u/$p/{dur}/{start}/$id.ts" else null)
        }
        return out
    }

    fun parseVod(streams: String, cats: String, server: String, user: String, pass: String, sourceId: String): List<Vod> {
        val c = categories(cats)
        val arr = runCatching { JSONArray(streams) }.getOrNull() ?: return emptyList()
        return (0 until arr.length()).mapNotNull { i ->
            val v = arr.getJSONObject(i)
            val id = v.str("stream_id").ifEmpty { return@mapNotNull null }
            val ext = v.str("container_extension").ifEmpty { "mp4" }
            Vod("$sourceId:v$id", v.str("name"), c[v.str("category_id")] ?: "Filme", "${normalize(server)}/movie/${e(user)}/${e(pass)}/$id.$ext",
                v.ostr("stream_icon"), v.ostr("plot"), v.ostr("year") ?: (v.ostr("releasedate") ?: v.ostr("added"))?.split('-')?.firstOrNull(), v.ostr("rating"), sourceId)
        }
    }

    fun parseSeries(series: String, cats: String, sourceId: String): List<Series> {
        val c = categories(cats)
        val arr = runCatching { JSONArray(series) }.getOrNull() ?: return emptyList()
        return (0 until arr.length()).mapNotNull { i ->
            val v = arr.getJSONObject(i)
            val id = v.str("series_id").ifEmpty { return@mapNotNull null }
            Series("$sourceId:s$id", v.str("name"), c[v.str("category_id")] ?: "Serien", v.ostr("cover"), v.ostr("plot"),
                (v.ostr("releaseDate") ?: v.ostr("release_date"))?.split('-')?.firstOrNull(), v.ostr("rating"), sourceId)
        }
    }

    fun parseEpisodes(json: String, seriesId: String, server: String, user: String, pass: String): List<Episode> {
        val root = runCatching { JSONObject(json) }.getOrNull() ?: return emptyList()
        val eps = root.optJSONObject("episodes") ?: return emptyList()
        val out = ArrayList<Episode>()
        for (season in eps.keys()) {
            val arr = eps.optJSONArray(season) ?: continue
            for (i in 0 until arr.length()) {
                val ep = arr.getJSONObject(i)
                val id = ep.str("id")
                if (id.isEmpty()) continue
                val ext = ep.str("container_extension").ifEmpty { "mp4" }
                val info = ep.optJSONObject("info")
                out += Episode(seriesId, ep.str("season").toIntOrNull() ?: season.toIntOrNull() ?: 1, ep.str("episode_num").toIntOrNull() ?: (i + 1),
                    ep.str("title"), "${normalize(server)}/series/${e(user)}/${e(pass)}/$id.$ext", info?.optString("plot")?.ifBlank { null },
                    info?.optString("duration_secs")?.toIntOrNull() ?: 0)
            }
        }
        return out.sortedWith(compareBy({ it.season }, { it.episode }))
    }

    /** Id hinter „quelle:s123" → „123" (für get_series_info). */
    fun rawSeriesId(id: String) = id.substringAfterLast(":s")
}

/** XMLTV-Programmführer: gestreamt gelesen, nur bekannte Sender und ein Zeitfenster (Speicher!). */
object Xmltv {
    /** „20261027180000 +0100" → Millisekunden UTC. */
    fun parseTime(s: String): Long {
        val t = s.trim()
        val main = if (t.length >= 14) t.substring(0, 14) else t.padEnd(14, '0')
        val cal = Calendar.getInstance(TimeZone.getTimeZone("UTC")).apply {
            clear()
            set(main.substring(0, 4).toInt(), main.substring(4, 6).toInt() - 1, main.substring(6, 8).toInt(),
                main.substring(8, 10).toInt(), main.substring(10, 12).toInt(), main.substring(12, 14).toInt())
        }
        var ms = cal.timeInMillis
        val rest = if (t.length > 14) t.substring(14).trim() else ""
        Regex("""^([+-])(\d{2})(\d{2})""").find(rest)?.let { m ->
            val off = (m.groupValues[2].toInt() * 60 + m.groupValues[3].toInt()) * 60_000L
            ms -= if (m.groupValues[1] == "+") off else -off
        }
        return ms
    }

    /**
     * Liest Sendungen für [wanted] Sender-Ids im Fenster [from]..[to]. Höchstens [max] Einträge.
     * Leeres [wanted] = alle Sender.
     */
    fun parse(input: InputStream, wanted: Set<String>, from: Long, to: Long, max: Int = 120_000): List<Programme> {
        val p = XmlPullParserFactory.newInstance().newPullParser()
        p.setInput(input, null)
        val out = ArrayList<Programme>()
        var ch: String? = null; var start = 0L; var stop = 0L
        var title = ""; var desc = ""; var cat = ""; var field: String? = null; var keep = false
        while (p.eventType != XmlPullParser.END_DOCUMENT && out.size < max) {
            when (p.eventType) {
                XmlPullParser.START_TAG -> when (p.name) {
                    "programme" -> {
                        ch = p.getAttributeValue(null, "channel")
                        start = runCatching { parseTime(p.getAttributeValue(null, "start") ?: "") }.getOrDefault(0)
                        stop = runCatching { parseTime(p.getAttributeValue(null, "stop") ?: "") }.getOrDefault(0)
                        keep = ch != null && (wanted.isEmpty() || ch in wanted) && stop > from && start < to
                        title = ""; desc = ""; cat = ""
                    }
                    "title", "desc", "category" -> field = if (keep) p.name else null
                }
                XmlPullParser.TEXT -> when (field) {
                    "title" -> if (title.isEmpty()) title = p.text.trim()
                    "desc" -> if (desc.isEmpty()) desc = p.text.trim().take(600)
                    "category" -> if (cat.isEmpty()) cat = p.text.trim()
                }
                XmlPullParser.END_TAG -> {
                    if (p.name == "title" || p.name == "desc" || p.name == "category") field = null
                    if (p.name == "programme" && keep && ch != null) out += Programme(ch, start, stop, title.ifBlank { "Sendung" }, desc, cat)
                }
            }
            p.next()
        }
        return out
    }
}
