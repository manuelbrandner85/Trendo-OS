package os.trendo.tv.livetv

/**
 * Streaming-Anbieter, nach denen viele Listen Filme, Serien und Sender einordnen
 * („DE • NETFLIX SERIEN", „DE | DISNEY+ FILME", „DE: SKY CINEMA"). Wie `TvCommands.Providers` in Windows, erweitert.
 */
object Providers {
    val list: List<Pair<String, List<String>>> = listOf(
        "Netflix" to listOf("netflix"),
        "Disney+" to listOf("disney"),
        "Prime Video" to listOf("amazon", "prime video", " prime "),
        "Apple TV+" to listOf("apple"),
        "Sky" to listOf("sky "),
        "WOW" to listOf("wow "),
        "HBO Max" to listOf("hbo", "hmax", "max original"),
        "Paramount+" to listOf("paramount"),
        "RTL+" to listOf("rtl+", "rtl plus", "tvnow"),
        "Joyn" to listOf("joyn"),
        "MagentaTV" to listOf("magenta"),
        "DAZN" to listOf("dazn"),
        "Crunchyroll" to listOf("crunchyroll"),
        "Discovery+" to listOf("discovery"),
        "ARD/ZDF" to listOf(" ard ", "zdf", "mediathek"),
    )

    /** Anbieter einer Kategorie (oder null). */
    fun of(category: String): String? {
        val c = " " + category.lowercase().replace('•', ' ').replace('|', ' ').replace(':', ' ').replace('-', ' ') + " "
        return list.firstOrNull { (_, keys) -> keys.any { c.contains(it) } }?.first
    }

    /** Anbieter, die in den Kategorien vorkommen – in fester Reihenfolge. */
    fun present(categories: Collection<String>): List<String> {
        val found = categories.mapNotNull { of(it) }.toSet()
        return list.map { it.first }.filter { it in found }
    }
}
