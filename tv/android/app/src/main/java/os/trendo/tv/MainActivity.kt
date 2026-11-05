package os.trendo.tv

import android.os.Bundle
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.setValue
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import os.trendo.tv.apps.AndroidApps
import os.trendo.tv.apps.AppCatalog
import os.trendo.tv.apps.AppFilters
import os.trendo.tv.apps.DeviceStats
import os.trendo.tv.apps.FavoritesStore
import os.trendo.tv.ui.TrendoTvApp
import os.trendo.tv.ui.TvActions
import os.trendo.tv.ui.TvState

class MainActivity : ComponentActivity() {
    private lateinit var apps: AndroidApps
    private lateinit var store: FavoritesStore
    private var refresh by mutableIntStateOf(0)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        apps = AndroidApps(this)
        store = FavoritesStore(this)
        val version = runCatching { packageManager.getPackageInfo(packageName, 0).versionName }.getOrNull() ?: "?"
        val empty = TvState(emptyList(), emptyList(), emptyList(), null, null, emptyList(), DeviceStats(null, null, null, null, null), false, version)

        val actions = TvActions(
            launch = { pkg -> if (apps.launch(pkg)) store.lastLaunched = pkg else fail() },
            appInfo = { pkg -> if (!apps.openSystemInfo(pkg)) fail() },
            settings = { action -> if (!apps.openSettings(action)) fail() },
            originalHome = { if (!apps.openOriginalHome()) fail() },
            uninstallSelf = { if (!apps.uninstallSelf()) fail() },
            openInput = { input -> if (!apps.openInput(input)) fail() },
            banner = apps::banner,
        )

        setContent {
            val state by produceState(empty, refresh) {
                value = withContext(Dispatchers.IO) {
                    val list = apps.load()
                    val favPkgs = store.load() ?: AppCatalog.suggestedFavorites(list).also { if (list.isNotEmpty()) store.save(it) }
                    val favs = AppCatalog.favorites(list, favPkgs)
                    TvState(
                        apps = list,
                        favorites = favs,
                        myApps = AppFilters.myApps(list, favs),
                        lastLaunched = list.firstOrNull { it.packageName == store.lastLaunched },
                        liveTv = list.firstOrNull { it.label.lowercase().contains("tivimate") },
                        inputs = apps.hdmiInputs(),
                        stats = apps.stats(),
                        online = apps.online(),
                        version = version,
                    )
                }
            }
            TrendoTvApp(state, actions)
        }
    }

    /** Beim Zurückkehren neu einlesen (neu installierte Apps, zuletzt geöffnet); kein Hintergrund-Polling. */
    override fun onResume() {
        super.onResume()
        refresh++
    }

    private fun fail() {
        Toast.makeText(this, "Konnte nicht geöffnet werden.", Toast.LENGTH_SHORT).show()
    }
}
