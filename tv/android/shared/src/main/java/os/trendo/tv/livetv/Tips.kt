package os.trendo.tv.livetv

import java.util.Calendar

/** Ein Tipp: Sendung + Sender. */
data class Tip(val channel: Channel, val programme: Programme)

/** „Heute-Tipps & Sport": baut Reihen aus dem Programmführer – Favoriten zuerst. */
object Tips {
    private val sportWords = listOf("sport", "fußball", "fussball", "bundesliga", "champions league", "europa league", "dfb", "formel 1", "tennis",
        "handball", "basketball", "eishockey", "boxen", "ufc", "golf", "darts", "radsport", "ski", "biathlon", "nfl", "nba", "motogp", "live:")
    private val filmWords = listOf("spielfilm", "film", "movie", "kino", "thriller", "komödie", "drama", "action", "krimi", "horror", "western")
    private val seriesWords = listOf("serie", "series", "folge", "staffel", "episode", "sitcom", "soap")
    private val newsWords = listOf("nachrichten", "news", "tagesschau", "tagesthemen", "heute journal", "heute ", "rtl aktuell", "zib", "magazin", "wetter")
    private val kidsWords = listOf("kinder", "kids", "zeichentrick", "animation", "cartoon", "kika", "junior")

    private fun has(p: Programme, words: List<String>): Boolean {
        val t = (" " + p.category + " " + p.title + " ").lowercase()
        return words.any { t.contains(it) }
    }

    fun isSport(p: Programme) = has(p, sportWords)
    fun isFilm(p: Programme) = has(p, filmWords) && !has(p, seriesWords)
    fun isSeries(p: Programme) = has(p, seriesWords)
    fun isNews(p: Programme) = has(p, newsWords)
    fun isKids(p: Programme) = has(p, kidsWords)

    /** Heute 20:15 (Primetime) als Zeitpunkt. */
    fun primetime(now: Long): Long = Calendar.getInstance().apply {
        timeInMillis = now; set(Calendar.HOUR_OF_DAY, 20); set(Calendar.MINUTE, 15); set(Calendar.SECOND, 0); set(Calendar.MILLISECOND, 0)
    }.timeInMillis

    data class Row(val title: String, val tips: List<Tip>)

    fun rows(channels: List<Channel>, epg: Map<String, List<Programme>>, favorites: Set<String>, now: Long, kidsMode: Boolean = false, max: Int = 30): List<Row> {
        val all = ArrayList<Tip>()
        for (c in channels) {
            val progs = c.epgId?.let { epg[it] } ?: continue
            for (p in progs) if (p.stop > now && p.start < now + 12 * 3_600_000L) all += Tip(c, p)
        }
        fun pick(f: (Tip) -> Boolean) = all.filter(f).distinctBy { it.programme.title + it.programme.start }
            .sortedWith(compareBy<Tip>({ if (it.channel.id in favorites) 0 else 1 }, { it.programme.start })).take(max)
        val pt = primetime(now)
        val rows = if (kidsMode) listOf(Row("Kinder – jetzt", pick { it.programme.isOn(now) && isKids(it.programme) }))
        else listOf(
            Row("Jetzt live: Sport", pick { it.programme.isOn(now) && isSport(it.programme) }),
            Row("Filme – jetzt & gleich", pick { it.programme.start < now + 30 * 60_000L && isFilm(it.programme) }),
            Row("Heute Abend 20:15", pick { kotlin.math.abs(it.programme.start - pt) <= 20 * 60_000L }.sortedBy { if (isFilm(it.programme)) 0 else 1 }),
            Row("Sport heute", pick { !it.programme.isOn(now) && isSport(it.programme) }),
            Row("Serien jetzt", pick { it.programme.isOn(now) && isSeries(it.programme) }),
            Row("Nachrichten", pick { it.programme.start < now + 60 * 60_000L && isNews(it.programme) }),
            Row("Kinder", pick { it.programme.isOn(now) && isKids(it.programme) }),
        )
        return rows.filter { it.tips.isNotEmpty() }
    }
}

/** Sprachbefehle für Trendo Live TV (lokal, feste Regeln). */
sealed class LiveCommand {
    data class Play(val channel: Channel) : LiveCommand()
    data class Remind(val tip: Tip) : LiveCommand()
    data class Tab(val name: String, val filter: String = "") : LiveCommand()
    data class Search(val query: String) : LiveCommand()
    data class Unknown(val text: String) : LiveCommand()
}

object LiveVoice {
    private fun norm(s: String) = s.lowercase().replace(Regex("""\b(hd|fhd|uhd|4k|sd|hevc)\b"""), " ").replace(Regex("""^[a-z]{2,3}\s*[|:•-]\s*"""), "")
        .replace(Regex("""[^a-z0-9äöüß+]"""), "")

    fun channelByName(name: String, channels: List<Channel>): Channel? {
        val n = norm(name).ifEmpty { return null }
        return channels.firstOrNull { norm(it.name) == n } ?: channels.firstOrNull { norm(it.name).startsWith(n) }
            ?: channels.firstOrNull { n.length >= 3 && norm(it.name).contains(n) }
    }

    fun parse(raw: String, channels: List<Channel>, epg: Map<String, List<Programme>>, now: Long): LiveCommand {
        val t = raw.trim().lowercase().replace(Regex("""[?!.,]"""), "").replace(Regex("""\s+"""), " ")
        Regex("""^(erinnere mich an|erinnerung für|erinner mich an)\s+(.+)$""").find(t)?.let { m ->
            val q = m.groupValues[2].trim()
            val tip = channels.asSequence().flatMap { c -> (c.epgId?.let { epg[it] } ?: emptyList()).asSequence().map { Tip(c, it) } }
                .filter { it.programme.start > now && it.programme.title.lowercase().contains(q) }.minByOrNull { it.programme.start }
            return if (tip != null) LiveCommand.Remind(tip) else LiveCommand.Unknown(raw)
        }
        if (Regex("""was läuft|was kommt|tipps|programm heute""").containsMatchIn(t)) {
            val genre = Regex("""(krimi|film|filme|serie|serien|sport|fußball|nachrichten|kinder|doku|komödie|thriller)""").find(t)?.value ?: ""
            return LiveCommand.Tab("TIPS", genre)
        }
        Regex("""^(suche|suche nach|finde)\s+(.+)$""").find(t)?.let { return LiveCommand.Search(it.groupValues[2]) }
        when {
            t.contains("programmführer") || t.contains("tv guide") || t.contains("epg") -> return LiveCommand.Tab("GUIDE")
            t == "filme" || t.startsWith("zeig filme") || t.startsWith("öffne filme") -> return LiveCommand.Tab("MOVIES")
            t == "serien" || t.startsWith("zeig serien") || t.startsWith("öffne serien") -> return LiveCommand.Tab("SERIES")
        }
        val name = Regex("""^(schalte auf|schalt auf|zeig|zeige|öffne|starte|wechsel zu|wechsle zu|geh auf|mach)\s+(.+?)( an| ein)?$""").find(t)?.groupValues?.get(2) ?: t
        channelByName(name, channels)?.let { return LiveCommand.Play(it) }
        return LiveCommand.Unknown(raw)
    }
}
