package os.trendo.tv

import android.os.Bundle
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import os.trendo.tv.apps.AndroidApps
import os.trendo.tv.apps.AppCatalog
import os.trendo.tv.apps.AppEntry
import os.trendo.tv.apps.FavoritesStore
import os.trendo.tv.ui.HomeScreen
import os.trendo.tv.ui.Tile

class MainActivity : ComponentActivity() {
    private lateinit var apps: AndroidApps
    private lateinit var store: FavoritesStore
    private var refresh by mutableIntStateOf(0)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        apps = AndroidApps(this)
        store = FavoritesStore(this)
        setContent {
            val list by produceState(emptyList<AppEntry>(), refresh) {
                value = withContext(Dispatchers.IO) { apps.load() }
            }
            val favPackages = remember(list) {
                store.load() ?: AppCatalog.suggestedFavorites(list).also { if (list.isNotEmpty()) store.save(it) }
            }
            HomeScreen(
                favorites = AppCatalog.favorites(list, favPackages).map { it.tile("fav:") },
                apps = list.map { it.tile("app:") },
                system = listOf(
                    Tile("sys:settings", "TV-Einstellungen", null) { if (!apps.openTvSettings()) fail() },
                    Tile("sys:home", "Original-Startseite", null) { if (!apps.openOriginalHome()) fail() },
                    Tile("sys:undo", "Alles rückgängig (Trendo entfernen)", null) { if (!apps.uninstallSelf()) fail() },
                ),
                loadBanner = apps::banner,
            )
        }
    }

    /** Beim Zurückkehren neu einlesen (neu installierte Apps); kein Hintergrund-Polling. */
    override fun onResume() {
        super.onResume()
        refresh++
    }

    private fun AppEntry.tile(prefix: String) =
        Tile(prefix + packageName, label, this) { if (!apps.launch(packageName)) fail() }

    private fun fail() {
        Toast.makeText(this, "Konnte nicht geöffnet werden.", Toast.LENGTH_SHORT).show()
    }
}
