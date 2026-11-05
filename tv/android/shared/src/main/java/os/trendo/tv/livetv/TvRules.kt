package os.trendo.tv.livetv

/** Filterregeln wie in Trendo OS für Windows (`TvCommands`), gleiche Ergebnisse auf beiden Geräten. */
object TvRules {
    private val countryPrefix = Regex("""^\s*\|?\s*([A-Za-z]{2,5})\s*(?:[•|:\-–]|\s{2,})""")
    private val adultMark = Regex("""(^|[^a-z0-9])(xxx|18\+|adult\w*|erwachsen\w*|porn\w*|erotik\w*)([^a-z0-9]|$)""", RegexOption.IGNORE_CASE)
    private val kidsWords = listOf("kids", "kinder", "kika", "junior", "disney channel", "nick", "cartoon", "toggo", "super rtl", "baby", "family", "familie", "boomerang")

    val countries: Map<String, List<String>> = linkedMapOf(
        "de" to listOf("de", "ger", "germany", "german", "deutsch", "deutschland", "🇩🇪"),
        "at" to listOf("at", "austria", "österreich", "oesterreich", "🇦🇹"),
        "ch" to listOf("ch", "swiss", "schweiz", "switzerland", "🇨🇭"),
        "uk" to listOf("uk", "gb", "united kingdom", "england", "british", "🇬🇧"),
        "us" to listOf("us", "usa", "united states", "american", "🇺🇸"),
        "tr" to listOf("tr", "turkey", "türkei", "turkiye", "türkiye", "turkish", "🇹🇷"),
        "fr" to listOf("fr", "france", "frankreich", "french", "🇫🇷"),
        "it" to listOf("it", "italy", "italien", "italia", "🇮🇹"),
        "es" to listOf("es", "spain", "spanien", "españa", "🇪🇸"),
        "pl" to listOf("pl", "poland", "polen", "polska", "🇵🇱"),
        "nl" to listOf("nl", "netherlands", "niederlande", "holland", "🇳🇱"),
        "ar" to listOf("ar", "arabic", "arabisch", "arab"),
    )
    val germanSpeaking = setOf("de", "at", "ch")

    fun isCountry(text: String, code: String): Boolean {
        val keys = countries[code] ?: return false
        val t = text.lowercase()
        return keys.any { k ->
            if (k.length <= 3 && k.all { it in 'a'..'z' }) Regex("""(^|[^a-z])${Regex.escape(k)}([^a-z]|$)""").containsMatchIn(t) else t.contains(k)
        }
    }

    fun countryOf(category: String): String? {
        countryPrefix.find(category)?.let { m ->
            var t = m.groupValues[1].lowercase()
            t = when (t) { "ger", "deu" -> "de"; "gb", "en", "eng" -> "uk"; "usa" -> "us"; "tur" -> "tr"; "ita" -> "it"; "fra" -> "fr"; "esp" -> "es"; else -> t }
            if (t !in setOf("xxx", "vod", "hd", "fhd", "uhd", "4k", "the")) return t
        }
        return countries.keys.firstOrNull { isCountry(category, it) }
    }

    fun isAdult(text: String?): Boolean = !text.isNullOrEmpty() && adultMark.containsMatchIn(text)

    fun isKids(group: String, name: String?): Boolean {
        val t = (group + " " + (name ?: "")).lowercase()
        return kidsWords.any { t.contains(it) }
    }

    /** Wie `TvCommands.Allowed`: Erwachsene nur wenn erlaubt; „nur Deutsch" lässt Einträge ohne Länderkennung stehen. */
    fun allowed(group: String, name: String?, onlyGerman: Boolean, showAdult: Boolean): Boolean {
        if (isAdult(group) || isAdult(name)) return showAdult
        if (!onlyGerman) return true
        val c = countryOf(group) ?: name?.let { countryOf(it) }
        return c == null || c in germanSpeaking
    }

    private val qualityWords = listOf("uhd", "4k", "fhd", "hd", "sd", "hevc", "h265", "raw", "backup", "low", "50fps", "60fps")

    /** „DE: ZDF HD" und „ZDF FHD" → „zdf" (für das Zusammenführen doppelter Sender). */
    fun baseName(name: String): String {
        var t = name.lowercase().replace(Regex("""^\s*\|?\s*[a-z]{2,4}\s*[•|:\-–]\s*"""), "")
        t = t.replace(Regex("""[\[\]()|•:]"""), " ")
        val words = t.split(Regex("""\s+""")).filter { it.isNotBlank() && it !in qualityWords && !it.matches(Regex("""\d{3,4}p""")) }
        return words.joinToString(" ").trim()
    }

    /** Höhere Zahl = bessere Qualität. */
    fun qualityRank(name: String): Int {
        val t = name.lowercase()
        return when {
            Regex("""\b(uhd|4k)\b""").containsMatchIn(t) -> 4
            Regex("""\b(fhd|1080p?)\b""").containsMatchIn(t) -> 3
            Regex("""\bhd\b|720p""").containsMatchIn(t) -> 2
            Regex("""\bsd\b|low""").containsMatchIn(t) -> 0
            else -> 1
        }
    }

    /** Doppelte Sender (gleicher Grundname, gleiche Gruppe) zu einem zusammenfassen – beste Qualität gewinnt. */
    fun mergeDuplicates(channels: List<Channel>): List<Channel> {
        // Linear (wichtig bei 10.000+ Sendern): je Grundname den besten merken, Reihenfolge des ersten Auftretens behalten.
        val best = LinkedHashMap<String, Channel>(channels.size)
        for (c in channels) {
            val key = c.group + "|" + baseName(c.name)
            val cur = best[key]
            if (cur == null || qualityRank(c.name) > qualityRank(cur.name)) best[key] = c
        }
        val firstIndex = HashMap<String, Int>(best.size)
        channels.forEachIndexed { i, c -> firstIndex.putIfAbsent(c.group + "|" + baseName(c.name), i) }
        return best.entries.sortedBy { firstIndex[it.key] ?: 0 }.map { it.value }
    }

    /** Wendet alle Einstellungen an. Gesperrte Gruppen bleiben sichtbar (Schloss), solange sie nicht entsperrt sind. */
    fun visible(channels: List<Channel>, s: LiveSettings): List<Channel> {
        var list = channels.filter { it.group !in s.hiddenGroups && allowed(it.group, it.name, s.onlyGerman, s.showAdult) }
        if (s.kidsMode) list = list.filter { isKids(it.group, it.name) && !isAdult(it.group) && !isAdult(it.name) }
        if (s.mergeDuplicates) list = mergeDuplicates(list)
        return list
    }

    fun groups(channels: List<Channel>): List<String> = channels.map { it.group }.distinct()

    /** Jetzt/Danach für einen Sender. */
    fun nowNext(progs: List<Programme>?, now: Long): Pair<Programme?, Programme?> {
        if (progs.isNullOrEmpty()) return null to null
        val i = progs.indexOfFirst { it.isOn(now) }
        if (i < 0) return null to progs.firstOrNull { it.start > now }
        return progs[i] to progs.getOrNull(i + 1)
    }

    /** PIN nie im Klartext speichern. */
    fun pinHash(pin: String, salt: String): String {
        val md = java.security.MessageDigest.getInstance("SHA-256")
        return md.digest("$salt:$pin".toByteArray()).joinToString("") { "%02x".format(it) }
    }
}
