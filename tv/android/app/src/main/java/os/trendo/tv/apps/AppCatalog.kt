package os.trendo.tv.apps

/** Eine startbare App, wie Android sie dem Launcher meldet. Reine Daten, keine Android-Typen. */
data class AppEntry(
    val packageName: String,
    val label: String,
    /** true = App hat einen eigenen TV-Starteintrag (LEANBACK_LAUNCHER). */
    val isTvApp: Boolean,
    /** Art laut App selbst (Android `ApplicationInfo.category`), z. B. Spiel, Video, Audio. */
    val kind: AppKind = AppKind.OTHER,
    /** Vom Hersteller fest eingebaut (lässt sich nur deaktivieren, nicht deinstallieren). */
    val isSystem: Boolean = false,
)

/** Apps, die für Fernsehen, HDMI, Updates oder den Play Store gebraucht werden – nie zum Entfernen vorschlagen. */
object ProtectedApps {
    private val prefixes = listOf(
        "com.android.vending", "com.google.android.gms", "com.google.android.gsf", "com.google.android.tv", "com.android.tv",
        "com.google.android.tvlauncher", "com.google.android.tvrecommendations", "com.mediatek", "com.mstar", "com.android.providers",
        "ar.tvplayer.tv", "com.google.android.katniss", "com.google.android.backdrop",
    )
    private val words = listOf("setup", "tvapi", "update", "input", "tuner", "hdmi", "cec")

    fun isProtected(a: AppEntry): Boolean =
        prefixes.any { a.packageName == it || a.packageName.startsWith("$it.") } ||
            words.any { a.packageName.lowercase().contains(it) || a.label.lowercase().replace(" ", "").contains(it) } ||
            a.label.lowercase().let { it == "live tv" || it.contains("tivimate") }
}

enum class AppKind { GAME, VIDEO, AUDIO, IMAGE, OTHER }

/** Reine Filter für die Bereiche der Seitenleiste. */
object AppFilters {
    fun ofKind(apps: List<AppEntry>, kind: AppKind) = apps.filter { it.kind == kind }

    fun search(apps: List<AppEntry>, query: String): List<AppEntry> {
        val q = query.trim().lowercase()
        if (q.isEmpty()) return apps
        return apps.filter { it.label.lowercase().contains(q) }
            .sortedBy { if (it.label.lowercase().startsWith(q)) 0 else 1 }
    }

    /** „Meine Apps": Favoriten zuerst, dann der Rest, höchstens [max] Einträge. */
    fun myApps(apps: List<AppEntry>, favorites: List<AppEntry>, max: Int = 10): List<AppEntry> =
        (favorites + apps.filter { a -> favorites.none { it.packageName == a.packageName } && a.isTvApp }).take(max)
}

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
