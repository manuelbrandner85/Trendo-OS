package os.trendo.tv.apps

import android.content.Context

/** Favoriten nur im eigenen privaten Trendo-Speicher. Keine Daten fremder Apps. */
class FavoritesStore(context: Context) {
    private val prefs = context.getSharedPreferences("trendo_tv", Context.MODE_PRIVATE)

    fun load(): List<String>? = prefs.getString(KEY, null)?.split('\n')?.filter { it.isNotBlank() }

    fun save(packages: List<String>) { prefs.edit().putString(KEY, packages.joinToString("\n")).apply() }

    private companion object { const val KEY = "favorites_v1" }
}
