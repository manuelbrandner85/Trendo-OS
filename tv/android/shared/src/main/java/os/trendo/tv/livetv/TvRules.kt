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

    /** Suchmuster einmal vorab bauen (nicht für jeden Sender neu – wichtig bei 10.000+ Sendern). */
    private val countryPatterns: Map<String, List<Any>> = countries.mapValues { (_, keys) ->
        keys.map { k -> if (k.length <= 3 && k.all { it in 'a'..'z' }) Regex("""(^|[^a-z])${Regex.escape(k)}([^a-z]|$)""") else k }
    }

    fun isCountry(text: String, code: String): Boolean {
        val pats = countryPatterns[code] ?: return false
        val t = text.lowercase()
        return pats.any { p -> if (p is Regex) p.containsMatchIn(t) else t.contains(p as String) }
    }

    /** Ergebnisse je Gruppenname merken (es gibt nur wenige hundert verschiedene). */
    private val countryMemo = java.util.concurrent.ConcurrentHashMap<String, String>()
    private const val NONE = "-"

    /** Kapitälchen und andere Zierbuchstaben (z. B. „ᴅᴇ • SKY") in normale Buchstaben umwandeln. */
    private val smallCaps = mapOf('ᴀ' to 'a', 'ʙ' to 'b', 'ᴄ' to 'c', 'ᴅ' to 'd', 'ᴇ' to 'e', 'ꜰ' to 'f', 'ɢ' to 'g', 'ʜ' to 'h', 'ɪ' to 'i', 'ᴊ' to 'j',
        'ᴋ' to 'k', 'ʟ' to 'l', 'ᴍ' to 'm', 'ɴ' to 'n', 'ᴏ' to 'o', 'ᴘ' to 'p', 'ǫ' to 'q', 'ʀ' to 'r', 'ꜱ' to 's', 'ᴛ' to 't', 'ᴜ' to 'u', 'ᴠ' to 'v',
        'ᴡ' to 'w', 'ʏ' to 'y', 'ᴢ' to 'z')

    fun plain(s: String): String = buildString(s.length) { s.forEach { ch -> append(smallCaps[ch] ?: ch) } }

    /** Länderkürzel, wie Anbieter sie nutzen → Sprachkennung. */
    private val prefixMap = mapOf("ger" to "de", "deu" to "de", "gb" to "uk", "en" to "uk", "eng" to "uk", "usa" to "us", "tur" to "tr", "kktc" to "tr",
        "ita" to "it", "fra" to "fr", "esp" to "es", "alb" to "al", "cro" to "exyu", "bih" to "exyu", "slo" to "exyu", "mk" to "exyu", "srb" to "exyu",
        "crna" to "exyu", "gre" to "gr", "por" to "pt", "rom" to "ro", "bul" to "bg", "pol" to "pl", "ned" to "nl", "aze" to "az")
    private val notCountry = setOf("xxx", "vod", "hd", "fhd", "uhd", "qhd", "4k", "the", "vip", "ppv", "golf", "raw", "hevc", "sd", "live", "new", "top", "best")

    fun countryOf(category: String): String? {
        countryMemo[category]?.let { return if (it == NONE) null else it }
        return countryOfRaw(category).also { countryMemo[category] = it ?: NONE }
    }

    private fun countryOfRaw(category: String): String? {
        val cat = plain(category)
        countryPrefix.find(cat)?.let { m ->
            val t = m.groupValues[1].lowercase().let { prefixMap[it] ?: it }
            if (t !in notCountry) return t
        }
        return countries.keys.firstOrNull { isCountry(cat, it) }
    }

    private val adultMemo = java.util.concurrent.ConcurrentHashMap<String, Boolean>()
    fun isAdult(text: String?): Boolean = !text.isNullOrEmpty() && adultMemo.getOrPut(text) { adultMark.containsMatchIn(text) }

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
    private val basePrefix = Regex("""^\s*\|?\s*[a-z]{2,4}\s*[•|:\-–]\s*""")
    private val baseSep = Regex("""[\[\]()|•:]""")
    private val baseSpace = Regex("""\s+""")
    private val baseRes = Regex("""\d{3,4}p""")

    fun baseName(name: String): String {
        var t = plain(name).lowercase().replace(basePrefix, "")
        t = t.replace(baseSep, " ")
        val words = t.split(baseSpace).filter { it.isNotBlank() && it !in qualityWords && !it.matches(baseRes) }
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
        var list = channels.filter { it.group !in s.hiddenGroups && it.id !in s.hiddenChannels && allowed(it.group, it.name, s.onlyGerman, s.showAdult) }
        if (s.kidsMode) list = list.filter { isKids(it.group, it.name) && !isAdult(it.group) && !isAdult(it.name) }
        if (s.mergeDuplicates) list = mergeDuplicates(list)
        return when (s.sortMode) {
            "number" -> list.sortedBy { if (it.number > 0) it.number else Int.MAX_VALUE }
            "name" -> list.sortedWith(compareBy(String.CASE_INSENSITIVE_ORDER) { plain(it.name) })
            else -> list
        }
    }

    fun groups(channels: List<Channel>): List<String> = channels.map { it.group }.distinct()

    /** Jetzt/Danach für einen Sender. */
    fun nowNext(progs: List<Programme>?, now: Long): Pair<Programme?, Programme?> {
        if (progs.isNullOrEmpty()) return null to null
        val i = progs.indexOfFirst { it.isOn(now) }
        if (i < 0) return null to progs.firstOrNull { it.start > now }
        return progs[i] to progs.getOrNull(i + 1)
    }

    /** Anzeigename einer Sprache/Landeskennung (wie `TvCommands.LanguageLabel`). */
    fun languageLabel(code: String): String = when (code) {
        "de" -> "Deutsch"; "uk" -> "Englisch"; "us" -> "USA"; "tr" -> "Türkisch"; "fr" -> "Französisch"; "it" -> "Italienisch"
        "es" -> "Spanisch"; "pl" -> "Polnisch"; "nl" -> "Niederländisch"; "ar" -> "Arabisch"; "eu" -> "Europa/Mehrsprachig"
        "exyu" -> "Ex-Jugoslawien"; "al" -> "Albanisch"; "ru" -> "Russisch"; "ro" -> "Rumänisch"; "gr" -> "Griechisch"
        "pt" -> "Portugiesisch"; "kurd" -> "Kurdisch"; "at" -> "Österreich"; "ch" -> "Schweiz"; "bg" -> "Bulgarisch"; "az" -> "Aserbaidschanisch"
        "be" -> "Belgien"; "multi" -> "Mehrsprachig"; else -> code.uppercase()
    }

    /** Sprache eines Eintrags: DE/AT/CH zählen als „de"; ohne Kennung = null. */
    fun languageOf(group: String, name: String?): String? {
        val c = countryOf(group) ?: name?.let { countryOf(it) } ?: return null
        return if (c in germanSpeaking) "de" else c
    }

    /** Passt ein Eintrag zur gewählten Sprache? null = alle. „de" schließt Einträge ohne Kennung ein (viele deutsche Listen haben keine). */
    fun matchesLanguage(group: String, name: String?, lang: String?): Boolean {
        if (lang == null) return true
        val l = languageOf(group, name)
        return l == lang || (lang == "de" && l == null)
    }

    /** Sprachen, die in den Kategorien vorkommen – häufigste zuerst, Deutsch immer vorn. */
    fun languages(groups: List<String>): List<String> {
        val counts = groups.mapNotNull { languageOf(it, null) }.groupingBy { it }.eachCount()
        return counts.entries.sortedWith(compareBy({ if (it.key == "de") 0 else 1 }, { -it.value })).map { it.key }
    }

    /** Kann für diesen Sender eine Sendung aus dem Archiv geholt werden? */
    fun canCatchup(c: Channel, startMs: Long, now: Long): Boolean =
        c.catchup != null && c.archiveDays > 0 && startMs < now && now - startMs <= c.archiveDays * 86_400_000L

    /** Catch-up-Adresse ab [startMs] für [durMin] Minuten (Zeit in der Zeitzone des Fernsehers, wie bei Xtream üblich). */
    fun catchupUrl(c: Channel, startMs: Long, durMin: Int, tz: java.util.TimeZone = java.util.TimeZone.getDefault()): String? {
        val t = c.catchup ?: return null
        val f = java.text.SimpleDateFormat("yyyy-MM-dd:HH-mm", java.util.Locale.US).apply { timeZone = tz }
        return t.replace("{start}", f.format(java.util.Date(startMs))).replace("{dur}", durMin.coerceAtLeast(1).toString())
            .replace("{utc}", (startMs / 1000).toString())
    }

    /** PIN nie im Klartext speichern. */
    fun pinHash(pin: String, salt: String): String {
        val md = java.security.MessageDigest.getInstance("SHA-256")
        return md.digest("$salt:$pin".toByteArray()).joinToString("") { "%02x".format(it) }
    }
}
