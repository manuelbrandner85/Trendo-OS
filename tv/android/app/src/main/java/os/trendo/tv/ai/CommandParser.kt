package os.trendo.tv.ai

import os.trendo.tv.apps.AppEntry

/** Ein verstandener Befehl. Trendo AI am TV arbeitet mit festen Regeln – ohne Cloud, ohne PC. */
sealed class Command {
    data class OpenApp(val app: AppEntry) : Command()
    data class Search(val query: String) : Command()
    data class Input(val number: Int?) : Command()
    data class Timer(val minutes: Int) : Command()
    object TimerOff : Command()
    data class Volume(val up: Boolean) : Command()
    object Mute : Command()
    data class Media(val action: MediaAction) : Command()
    data class Page(val page: String) : Command()
    object Weather : Command()
    data class Unknown(val text: String) : Command()
}

enum class MediaAction { PLAY_PAUSE, NEXT, PREVIOUS }

object CommandParser {
    private val pages = mapOf(
        "einstellung" to "SETTINGS", "mediathek" to "MEDIATHEK", "filme" to "MOVIES", "film" to "MOVIES",
        "musik" to "MUSIC", "foto" to "PHOTOS", "bilder" to "PHOTOS", "spiele" to "GAMING", "gaming" to "GAMING",
        "datenschutz" to "PRIVACY", "smart home" to "SMART_HOME", "leistung" to "PERFORMANCE", "performance" to "PERFORMANCE",
        "apps" to "APPS", "start" to "START", "live" to "LIVE_TV", "fernsehen" to "LIVE_TV",
    )
    private val numberWords = mapOf(
        "eins" to 1, "ein" to 1, "eine" to 1, "einer" to 1, "einen" to 1, "zwei" to 2, "drei" to 3, "vier" to 4, "fünf" to 5, "zehn" to 10,
        "fünfzehn" to 15, "zwanzig" to 20, "dreißig" to 30, "vierzig" to 40, "fünfundvierzig" to 45, "sechzig" to 60, "neunzig" to 90,
    )

    fun parse(raw: String, apps: List<AppEntry>): Command {
        val t = raw.trim().lowercase().replace(Regex("""[.!?,]"""), " ").replace(Regex("""\s+"""), " ").trim()
        if (t.isEmpty()) return Command.Unknown(raw)

        if (Regex("""\b(timer|wecker)\b.*\b(aus|stopp|stop|abbrechen|beenden)\b""").containsMatchIn(t)) return Command.TimerOff
        if (t.contains("timer") || t.contains("einschlaf") || t.contains("ausschalten in")) {
            val n = number(t)
            val hours = t.contains("stunde")
            if (n != null) return Command.Timer(if (hours) n * 60 else n)
        }
        if (Regex("""\b(stumm|ton aus)\b""").containsMatchIn(t)) return Command.Mute
        if (Regex("""\b(lauter|leiser)\b""").containsMatchIn(t)) return Command.Volume(up = t.contains("lauter"))
        if (Regex("""\b(pause|weiter|abspielen|play)\b""").containsMatchIn(t) && !t.startsWith("öffne") && !t.startsWith("starte"))
            return Command.Media(MediaAction.PLAY_PAUSE)
        if (Regex("""\b(nächste|nächster|vor)\b""").containsMatchIn(t)) return Command.Media(MediaAction.NEXT)
        if (Regex("""\b(zurück|vorherige|vorheriger)\b""").containsMatchIn(t)) return Command.Media(MediaAction.PREVIOUS)
        if (t.contains("hdmi") || t.contains("eingang") || t.contains("quelle")) return Command.Input(number(t))
        if (t.contains("wetter")) return Command.Weather

        Regex("""^(suche|suche nach|finde)\s+(.+)$""").find(t)?.let { return Command.Search(it.groupValues[2].trim()) }

        val target = Regex("""^(öffne|starte|zeige|zeig|geh zu|gehe zu|wechsel zu|wechsle zu|mach|mache)\s+(.+?)(\s+auf)?$""").find(t)?.groupValues?.get(2) ?: t
        val cleaned = target.removePrefix("die ").removePrefix("den ").removePrefix("das ").removeSuffix(" app").trim()
        bestApp(cleaned, apps)?.let { return Command.OpenApp(it) }
        pages.entries.firstOrNull { cleaned.contains(it.key) }?.let { return Command.Page(it.value) }
        return Command.Unknown(raw)
    }

    /** Findet die passendste App am angezeigten Namen (genau > beginnt mit > enthält). */
    fun bestApp(name: String, apps: List<AppEntry>): AppEntry? {
        val n = name.lowercase().replace(" ", "")
        if (n.length < 2) return null
        fun key(a: AppEntry) = a.label.lowercase().replace(" ", "")
        return apps.firstOrNull { key(it) == n }
            ?: apps.firstOrNull { key(it).startsWith(n) }
            ?: apps.firstOrNull { key(it).contains(n) }
            ?: apps.firstOrNull { n.contains(key(it)) && key(it).length >= 4 }
    }

    fun number(t: String): Int? =
        Regex("""\d+""").find(t)?.value?.toIntOrNull()
            ?: t.split(' ').firstNotNullOfOrNull { numberWords[it] }
            ?: if (t.contains("halbe stunde")) 30 else null
}
