package os.trendo.tv.apps

/** Eine startbare App, wie Android sie dem Launcher meldet. Reine Daten, keine Android-Typen. */
data class AppEntry(
    val packageName: String,
    val label: String,
    /** true = App hat einen eigenen TV-Starteintrag (LEANBACK_LAUNCHER). */
    val isTvApp: Boolean,
)

/**
 * Ordnet die gefundenen Apps für die Startseite.
 * Wichtig: Eine App, die hier fehlt, gilt als „nicht sichtbar", niemals als „deinstalliert".
 */
object AppCatalog {

    /** Führt TV- und Handy-Starteinträge zusammen; TV-Eintrag gewinnt, eigene App fällt raus. */
    fun merge(tv: List<AppEntry>, phone: List<AppEntry>, ownPackage: String): List<AppEntry> {
        val byPackage = LinkedHashMap<String, AppEntry>()
        for (e in tv) if (e.packageName != ownPackage) byPackage[e.packageName] = e.copy(isTvApp = true)
        for (e in phone) if (e.packageName != ownPackage) byPackage.putIfAbsent(e.packageName, e.copy(isTvApp = false))
        return byPackage.values.sortedWith(compareBy(String.CASE_INSENSITIVE_ORDER) { it.label })
    }

    /**
     * Vorgeschlagene Favoriten beim ersten Start: erkannt am angezeigten Namen, nicht an geratenen
     * Paket-IDs. Der Nutzer kann die Liste später selbst ändern.
     */
    private val suggestedLabels = listOf("tivimate", "play store", "google play")

    fun suggestedFavorites(apps: List<AppEntry>): List<String> =
        suggestedLabels.mapNotNull { key -> apps.firstOrNull { it.label.lowercase().contains(key) }?.packageName }
            .distinct()

    /** Favoriten in gespeicherter Reihenfolge; nicht (mehr) sichtbare Einträge werden übersprungen. */
    fun favorites(apps: List<AppEntry>, saved: List<String>): List<AppEntry> {
        val map = apps.associateBy { it.packageName }
        return saved.mapNotNull { map[it] }
    }
}
