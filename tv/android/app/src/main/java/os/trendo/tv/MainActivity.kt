package os.trendo.tv

import android.content.Intent
import android.os.Bundle
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
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
    /** Zählt Home-Tastendrücke; die Oberfläche springt dann auf „Start". */
    private var homeTick by mutableIntStateOf(0)

    private val roleRequest = registerForActivityResult(ActivityResultContracts.StartActivityForResult()) {
        refresh++
        if (!apps.isHomeGuardEnabled()) {
            // Gerät hat abgelehnt oder Nutzer hat abgebrochen: Android-Auswahlseite als Ausweg.
            if (!apps.openHomeSettings()) hint("Bitte die Home-Taste drücken und „TRENDO OS“ als Startseite wählen.")
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        apps = AndroidApps(this)
        store = FavoritesStore(this)
        val version = runCatching { packageManager.getPackageInfo(packageName, 0).versionName }.getOrNull() ?: "?"
        val empty = TvState(emptyList(), emptyList(), emptyList(), null, null, emptyList(), DeviceStats(null, null, null, null, null), false, version, false)

        val actions = TvActions(
            launch = { pkg -> if (apps.launch(pkg)) store.lastLaunched = pkg else fail() },
            appInfo = { pkg -> if (!apps.openSystemInfo(pkg)) fail() },
            settings = { action -> if (!apps.openSettings(action)) fail() },
            originalHome = { store.originalHomeAllowed = true; if (!apps.openOriginalHome()) { store.originalHomeAllowed = false; fail() } },
            uninstallSelf = { if (!apps.uninstallSelf()) fail() },
            openInput = { input -> if (!apps.openInput(input)) fail() },
            banner = apps::banner,
            makeHome = ::requestHome,
            homeSettings = {
                if (apps.isHomeGuardEnabled()) { hint("„TRENDO OS Startseite“ ausschalten – dann gilt wieder die Original-Startseite."); apps.openAccessibilitySettings() }
                else if (!apps.openHomeSettings()) hint("Einstellungen → Apps → Standard-Apps → Startseite")
            },
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
                        // Nicht isDefaultHome(): manche TVs speichern die Wahl, die Home-Taste ignoriert sie aber.
                        isHome = apps.isHomeGuardEnabled(),
                    )
                }
            }
            TrendoTvApp(state, actions, homeTick)
        }
        if (intent?.getBooleanExtra(EXTRA_REQUEST_HOME, false) == true) requestHome()
    }

    /** Trendo als Startseite anbieten – entscheiden tut immer der Nutzer im Android-Dialog. */
    private fun requestHome() {
        apps.setHomeEntry(true)
        when {
            apps.isHomeGuardEnabled() -> hint("Trendo ist bereits die Startseite.")
            // Viele Android-TV-Geräte übernehmen eine andere Standard-Startseite nicht – dann der Wächter.
            else -> {
                hint("In den Bedienungshilfen „TRENDO OS Startseite“ wählen und einschalten.")
                if (!apps.openAccessibilitySettings()) {
                    hint("Einstellungen → Geräteeinstellungen → Bedienungshilfen → „TRENDO OS Startseite“ einschalten.")
                    val req = apps.homeRoleRequest()
                    if (req != null) runCatching { roleRequest.launch(req) } else apps.openHomeSettings()
                }
            }
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        if (intent.hasCategory(Intent.CATEGORY_HOME)) homeTick++
        if (intent.getBooleanExtra(EXTRA_REQUEST_HOME, false)) requestHome()
    }

    /** Beim Zurückkehren neu einlesen (neu installierte Apps, zuletzt geöffnet); kein Hintergrund-Polling. */
    override fun onResume() {
        super.onResume()
        // Trendo ist wieder vorne: der Wächter darf die Original-Startseite wieder abfangen.
        store.originalHomeAllowed = false
        refresh++
    }

    private fun fail() = hint("Konnte nicht geöffnet werden.")

    private fun hint(text: String) {
        Toast.makeText(this, text, Toast.LENGTH_LONG).show()
    }

    companion object {
        const val EXTRA_REQUEST_HOME = "os.trendo.tv.REQUEST_HOME"
    }
}
