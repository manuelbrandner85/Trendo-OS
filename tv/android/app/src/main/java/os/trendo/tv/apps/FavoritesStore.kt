package os.trendo.tv.apps

import android.content.Context

/** Favoriten und zuletzt geöffnete App nur im eigenen privaten Trendo-Speicher. Keine Daten fremder Apps. */
class FavoritesStore(context: Context) {
    private val prefs = context.getSharedPreferences("trendo_tv", Context.MODE_PRIVATE)

    fun load(): List<String>? = prefs.getString(KEY, null)?.split('\n')?.filter { it.isNotBlank() }

    fun save(packages: List<String>) { prefs.edit().putString(KEY, packages.joinToString("\n")).apply() }

    var lastLaunched: String?
        get() = prefs.getString(LAST, null)
        set(v) { prefs.edit().putString(LAST, v).apply() }

    /** true = Nutzer hat bewusst die Original-Startseite gewählt; der Wächter hält sich zurück, bis Trendo wieder geöffnet wird. */
    var originalHomeAllowed: Boolean
        get() = prefs.getBoolean(ORIGINAL, false)
        set(v) { prefs.edit().putBoolean(ORIGINAL, v).commit() }

    private companion object {
        const val ORIGINAL = "original_home_allowed_v1"
        const val KEY = "favorites_v1"
        const val LAST = "last_launched_v1"
    }
}
