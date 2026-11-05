package os.trendo.tv

import android.content.Intent
import android.media.AudioManager
import android.os.Bundle
import android.provider.Settings
import android.speech.RecognizerIntent
import android.view.KeyEvent
import android.view.WindowManager
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import os.trendo.tv.ai.Command
import os.trendo.tv.ai.CommandParser
import os.trendo.tv.ai.MediaAction
import os.trendo.tv.apps.AndroidApps
import os.trendo.tv.apps.AppCatalog
import os.trendo.tv.apps.AppFilters
import os.trendo.tv.apps.DeviceStats
import os.trendo.tv.apps.FavoritesStore
import os.trendo.tv.media.MediaKind
import os.trendo.tv.media.MediaLibrary
import os.trendo.tv.media.ResumeStore
import os.trendo.tv.smarthome.Discovery
import os.trendo.tv.ui.GoodNight
import os.trendo.tv.ui.PhotoViewer
import os.trendo.tv.ui.Section
import os.trendo.tv.ui.TrendoTvApp
import os.trendo.tv.ui.TvActions
import os.trendo.tv.ui.TvState
import os.trendo.tv.weather.WeatherRepo

class MainActivity : ComponentActivity() {
    private lateinit var apps: AndroidApps
    private lateinit var store: FavoritesStore
    private lateinit var media: MediaLibrary
    private lateinit var resume: ResumeStore
    private lateinit var weather: WeatherRepo
    private var discovery: Discovery? = null
    private var refresh by mutableIntStateOf(0)
    private var section by mutableStateOf(Section.START)
    private var photo by mutableStateOf<Int?>(null)
    private var goodNight by mutableStateOf(false)
    private var lastState: TvState? = null
    private var voiceCallback: ((String) -> Unit)? = null

    private val roleRequest = registerForActivityResult(ActivityResultContracts.StartActivityForResult()) {
        refresh++
        if (!apps.isHomeGuardEnabled() && !apps.openHomeSettings()) hint("Bitte die Home-Taste drücken und „TRENDO OS“ als Startseite wählen.")
    }
    private val mediaPermission = registerForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) { refresh++ }
    private val voiceRequest = registerForActivityResult(ActivityResultContracts.StartActivityForResult()) { r ->
        val said = r.data?.getStringArrayListExtra(RecognizerIntent.EXTRA_RESULTS)?.firstOrNull()
        if (!said.isNullOrBlank()) voiceCallback?.invoke(said)
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        apps = AndroidApps(this)
        store = FavoritesStore(this)
        media = MediaLibrary(this)
        resume = ResumeStore(this)
        weather = WeatherRepo(this)
        val version = runCatching { packageManager.getPackageInfo(packageName, 0).versionName }.getOrNull() ?: "?"
        val empty = TvState(emptyList(), emptyList(), emptyList(), null, null, emptyList(), DeviceStats(null, null, null, null, null), false, version, false)
        val voiceOk = packageManager.queryIntentActivities(Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH), 0).isNotEmpty()

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
            play = { uri, title, kind -> startActivity(PlayerActivity.intent(this, uri, title, kind)) },
            showPhoto = { photo = it },
            allowMedia = { mediaPermission.launch(media.permissions()) },
            setTimer = { m -> SleepTimer.start(this, m); hint("Einschlaf-Timer: $m Minuten"); refresh++ },
            cancelTimer = { SleepTimer.cancel(this); hint("Timer aus"); refresh++ },
            searchPlace = { q, done -> lifecycleScope.launch { done(withContext(Dispatchers.IO) { weather.search(q) }) } },
            setPlace = { p -> weather.place = p; refresh++ },
            command = { text, reply -> lifecycleScope.launch { reply(run(text)) } },
            voice = { cb ->
                voiceCallback = cb
                runCatching {
                    voiceRequest.launch(Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH)
                        .putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM)
                        .putExtra(RecognizerIntent.EXTRA_LANGUAGE, "de-DE")
                        .putExtra(RecognizerIntent.EXTRA_PROMPT, "Was soll Trendo tun?"))
                }.onFailure { hint("Spracherkennung ist auf diesem Fernseher nicht verfügbar.") }
            },
            discovery = { on, cb ->
                discovery?.stop(); discovery = null
                if (on) discovery = Discovery(this) { list -> runOnUiThread { cb(list) } }.also { it.start() }
            },
        )

        setContent {
            // Schneller Start: zuerst nur Apps (lokal, schnell) – Medien, Wetter & Co. kommen danach dazu.
            val state by produceState(empty, refresh) {
                val base = withContext(Dispatchers.IO) {
                    val list = apps.load()
                    val favPkgs = store.load() ?: AppCatalog.suggestedFavorites(list).also { if (list.isNotEmpty()) store.save(it) }
                    val favs = AppCatalog.favorites(list, favPkgs)
                    (value.takeIf { it.apps.isNotEmpty() } ?: empty).copy(
                        apps = list,
                        favorites = favs,
                        myApps = AppFilters.myApps(list, favs),
                        lastLaunched = list.firstOrNull { it.packageName == store.lastLaunched },
                        liveTv = list.firstOrNull { it.label.lowercase().contains("tivimate") },
                        version = version,
                        // Nicht isDefaultHome(): manche TVs speichern die Wahl, die Home-Taste ignoriert sie aber.
                        isHome = apps.isHomeGuardEnabled(),
                        resume = resume.recent(),
                        place = weather.place,
                        timerMinutes = SleepTimer.remainingMinutes(this@MainActivity),
                        voiceAvailable = voiceOk,
                    )
                }
                value = base
                value = withContext(Dispatchers.IO) {
                    val allowed = media.hasPermission()
                    base.copy(
                        inputs = apps.hdmiInputs(),
                        stats = apps.stats(),
                        online = apps.online(),
                        videos = if (allowed) media.load(MediaKind.VIDEO) else emptyList(),
                        audio = if (allowed) media.load(MediaKind.AUDIO) else emptyList(),
                        images = if (allowed) media.load(MediaKind.IMAGE) else emptyList(),
                        mediaAllowed = allowed,
                    )
                }
                // Wetter zuletzt – darf nie den Start aufhalten (Netz).
                value = value.copy(weather = withContext(Dispatchers.IO) { runCatching { weather.current() }.getOrNull() })
            }
            lastState = state
            Box(Modifier.fillMaxSize()) {
                TrendoTvApp(state, actions, section) { section = it }
                photo?.let { i -> PhotoViewer(state.images, i, { photo = it }, { photo = null }) }
                if (goodNight) GoodNight { wake() }
            }
        }
        handleIntent(intent, first = true)
    }

    /** Führt einen Trendo-AI-Befehl aus und liefert die Antwort als Text. */
    private suspend fun run(text: String): String {
        val st = lastState ?: return "Einen Moment – Trendo lädt noch."
        val audio = getSystemService(AudioManager::class.java)
        return when (val c = CommandParser.parse(text, st.apps)) {
            is Command.OpenApp -> { if (apps.launch(c.app.packageName)) { store.lastLaunched = c.app.packageName; "Öffne ${c.app.label}." } else "${c.app.label} lässt sich nicht öffnen." }
            is Command.Search -> { section = Section.SEARCH; "Suche: ${c.query} – Ergebnisse auf der Suchseite." }
            is Command.Input -> {
                val target = c.number?.let { n -> st.inputs.firstOrNull { it.label.replace(" ", "").contains("HDMI$n", true) } }
                when {
                    target != null -> { apps.openInput(target); "Schalte auf ${target.label}." }
                    else -> { section = Section.HDMI; "Wähle den Eingang auf der HDMI-Seite." }
                }
            }
            is Command.Timer -> { SleepTimer.start(this, c.minutes); refresh++; "Einschlaf-Timer läuft: ${c.minutes} Minuten." }
            Command.TimerOff -> { SleepTimer.cancel(this); refresh++; "Timer ausgeschaltet." }
            is Command.Volume -> { audio.adjustStreamVolume(AudioManager.STREAM_MUSIC, if (c.up) AudioManager.ADJUST_RAISE else AudioManager.ADJUST_LOWER, AudioManager.FLAG_SHOW_UI); if (c.up) "Lauter." else "Leiser." }
            Command.Mute -> { audio.adjustStreamVolume(AudioManager.STREAM_MUSIC, AudioManager.ADJUST_TOGGLE_MUTE, AudioManager.FLAG_SHOW_UI); "Ton stumm / wieder an." }
            is Command.Media -> {
                val key = when (c.action) { MediaAction.PLAY_PAUSE -> KeyEvent.KEYCODE_MEDIA_PLAY_PAUSE; MediaAction.NEXT -> KeyEvent.KEYCODE_MEDIA_NEXT; MediaAction.PREVIOUS -> KeyEvent.KEYCODE_MEDIA_PREVIOUS }
                audio.dispatchMediaKeyEvent(KeyEvent(KeyEvent.ACTION_DOWN, key)); audio.dispatchMediaKeyEvent(KeyEvent(KeyEvent.ACTION_UP, key))
                "Erledigt."
            }
            is Command.Page -> { section = runCatching { Section.valueOf(c.page) }.getOrDefault(Section.START); "Öffne ${section.label}." }
            Command.Weather -> {
                val w = withContext(Dispatchers.IO) { runCatching { weather.current() }.getOrNull() }
                if (w != null) "${w.place}: ${w.tempC} °C, ${w.text}." else "Kein Wetter – bitte unter Einstellungen → Wetter-Ort einen Ort wählen."
            }
            is Command.Unknown -> "Nicht verstanden. Versuch z. B. „Öffne TiviMate“, „HDMI 1“, „Timer 30 Minuten“ oder „Lauter“."
        }
    }

    /** Trendo als Startseite anbieten – entscheiden tut immer der Nutzer. */
    private fun requestHome() {
        apps.setHomeEntry(true)
        when {
            apps.isHomeGuardEnabled() -> hint("Trendo ist bereits die Startseite.")
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

    private fun handleIntent(i: Intent?, first: Boolean) {
        if (i == null) return
        if (!first && i.hasCategory(Intent.CATEGORY_HOME)) { section = Section.START; photo = null }
        if (i.getBooleanExtra(EXTRA_REQUEST_HOME, false)) requestHome()
        if (i.getBooleanExtra(EXTRA_SCREENSAVER, false)) apps.openSettings(Settings.ACTION_DREAM_SETTINGS)
        if (i.getBooleanExtra(EXTRA_TIMER_DONE, false)) sleepScreen()
        i.getStringExtra(EXTRA_SECTION)?.let { name -> runCatching { Section.valueOf(name) }.getOrNull()?.let { section = it } }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        handleIntent(intent, first = false)
    }

    /** Timer abgelaufen: Bildschirm abdunkeln und den Fernseher sich selbst in Standby schicken lassen. */
    private fun sleepScreen() {
        goodNight = true
        window.clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        window.attributes = window.attributes.apply { screenBrightness = 0.01f }
    }

    private fun wake() {
        goodNight = false
        window.attributes = window.attributes.apply { screenBrightness = WindowManager.LayoutParams.BRIGHTNESS_OVERRIDE_NONE }
    }

    /** Beim Zurückkehren neu einlesen (neu installierte Apps, Medien, Weitersehen); kein Hintergrund-Polling. */
    override fun onResume() {
        super.onResume()
        // Trendo ist wieder vorne: der Wächter darf die Original-Startseite wieder abfangen.
        store.originalHomeAllowed = false
        refresh++
    }

    override fun onStop() {
        super.onStop()
        discovery?.stop(); discovery = null
    }

    private fun fail() = hint("Konnte nicht geöffnet werden.")

    private fun hint(text: String) {
        Toast.makeText(this, text, Toast.LENGTH_LONG).show()
    }

    companion object {
        const val EXTRA_REQUEST_HOME = "os.trendo.tv.REQUEST_HOME"
        const val EXTRA_TIMER_DONE = "os.trendo.tv.TIMER_DONE"
        const val EXTRA_SCREENSAVER = "os.trendo.tv.SCREENSAVER"
        /** Direkt eine Seite öffnen, z. B. „MEDIATHEK" (Verknüpfungen, Tests). */
        const val EXTRA_SECTION = "os.trendo.tv.SECTION"
    }
}
