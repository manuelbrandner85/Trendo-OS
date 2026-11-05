package os.trendo.tv.ui

import androidx.compose.material.icons.outlined.LiveTv
import androidx.compose.material.icons.outlined.Movie
import androidx.compose.material.icons.outlined.VideoLibrary
import androidx.compose.material.icons.outlined.DateRange
import androidx.compose.material.icons.outlined.Star
import androidx.compose.material.icons.outlined.Subscriptions
import androidx.compose.material.icons.outlined.SmartDisplay
import androidx.compose.material.icons.outlined.FiberManualRecord
import androidx.compose.material.icons.outlined.Search
import androidx.compose.material.icons.outlined.Settings
import androidx.compose.material.icons.outlined.Mic
import androidx.compose.material.icons.outlined.OpenInNew
import androidx.compose.ui.focus.onFocusChanged
import android.content.Context
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.input.VisualTransformation
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.tv.material3.Text
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import os.trendo.tv.LiveActivity
import os.trendo.tv.LiveSession
import os.trendo.tv.PlayerActivity
import os.trendo.tv.Reminders
import os.trendo.tv.livetv.Channel
import os.trendo.tv.livetv.LiveRepo
import os.trendo.tv.livetv.LiveSettings
import os.trendo.tv.livetv.LiveStore
import os.trendo.tv.livetv.Programme
import os.trendo.tv.livetv.Reminder
import os.trendo.tv.livetv.Series
import os.trendo.tv.livetv.Episode
import os.trendo.tv.livetv.Source
import os.trendo.tv.livetv.SourceKind
import os.trendo.tv.livetv.TvRules
import os.trendo.tv.livetv.Vod
import os.trendo.tv.media.MediaKind
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/** Reihenfolge wie gewünscht: Live TV, Filme, Serien, Programmführer – dann Suche und Einstellungen. */
private enum class LiveTab(val label: String) { CHANNELS("Live TV"), MOVIES("Filme"), SERIES("Serien"), GUIDE("Programmführer"), TIPS("Heute"), STREAMING("Mediatheken & Streaming"), VIDEOS("YouTube & Videos"), RECORDINGS("Aufnahmen"), SEARCH("Suche"), SOURCES("Quellen"), FILTER("Jugendschutz & Filter") }

/** Alles, was Live TV anzeigt – aus dem privaten Trendo-Speicher geladen. */
private data class LiveData(
    val sources: List<Source> = emptyList(),
    val all: List<Channel> = emptyList(),
    val visible: List<Channel> = emptyList(),
    val epg: Map<String, List<Programme>> = emptyMap(),
    val vod: List<Vod> = emptyList(),
    val series: List<Series> = emptyList(),
    val settings: LiveSettings = LiveSettings(),
    val reminders: List<Reminder> = emptyList(),
    val loaded: Boolean = false,
)

/**
 * Schreibt nur die NAMEN der Kategorien ins Geräteprotokoll (keine Adressen, keine Zugangsdaten),
 * damit die Anbieter-Zuordnung an die echte Liste angepasst werden kann.
 */
private var categoriesLogged = false
private fun logCategories(all: List<Channel>, vod: List<Vod>, series: List<Series>) {
    if (categoriesLogged || all.isEmpty()) return
    categoriesLogged = true
    fun log(kind: String, names: List<String>) = names.chunked(40).forEachIndexed { i, chunk ->
        android.util.Log.i("TrendoLiveKat", "$kind[$i] " + chunk.joinToString(" | "))
    }
    log("LIVE", all.map { it.group }.distinct())
    log("FILME", vod.map { it.category }.distinct())
    log("SERIEN", series.map { it.category }.distinct())
}

/** Stufe 1: Sender + Einstellungen (schnell, sofort sichtbar). */
private fun loadChannels(store: LiveStore, prof: os.trendo.tv.livetv.Profile?, old: LiveData): LiveData {
    // Alle Sprachen frei – gefiltert wird je Reiter über die Sprachleiste. Kinderprofil: nur Kinderinhalte.
    val s = os.trendo.tv.livetv.KidRules.effective(store.settings().copy(onlyGerman = false), prof)
    val all = store.channels()
    val vis = TvRules.visible(all, s).let { v -> v.filter { it.id in s.favorites } + v.filter { it.id !in s.favorites } }
    return old.copy(sources = store.sources(), all = all, visible = vis, settings = s, reminders = store.reminders(), loaded = true)
}

/** Stufe 3: Filme & Serien (groß, kommen zuletzt). */
private fun loadVod(store: LiveStore, d: LiveData): LiveData {
    val s = d.settings
    val vodRaw = store.vod(); val serRaw = store.series()
    logCategories(d.all, vodRaw, serRaw)
    return d.copy(
        vod = vodRaw.filter { TvRules.allowed(it.category, it.name, s.onlyGerman, s.showAdult) && (!s.kidsMode || TvRules.isKids(it.category, it.name)) }.map { if ((it.plot?.length ?: 0) > 220) it.copy(plot = it.plot!!.take(220) + "…") else it },
        series = serRaw.filter { TvRules.allowed(it.category, it.name, s.onlyGerman, s.showAdult) && (!s.kidsMode || TvRules.isKids(it.category, it.name)) }.map { if ((it.plot?.length ?: 0) > 220) it.copy(plot = it.plot!!.take(220) + "…") else it },
    )
}

/** Solange die App im Speicher ist: zuletzt geladene Daten je Profil – kein erneutes Einlesen beim Zurückkehren. */
private object LiveCache { val byProfile = HashMap<String, LiveData>(); var stamp = 0 }

/** Gastgeber von Live TV: Trendo-Startseite oder die eigene App „Trendo Live TV". */
class LiveHost(
    /** Film oder Folge abspielen (mit Weitersehen). */
    val playVod: (url: String, title: String) -> Unit,
    /** Optional: Knopf für eine andere App, z. B. „TiviMate öffnen". */
    val externalLabel: String? = null,
    val onExternal: () -> Unit = {},
)

@Composable
fun LiveTvPage(host: LiveHost, profile: String) {
    val ctx = LocalContext.current
    val store = remember(profile) { LiveStore(ctx, profile) }
    val repo = remember(store) { LiveRepo(store) }
    var tick by remember { mutableIntStateOf(0) }
    var tab by remember { mutableStateOf(LiveTab.CHANNELS) }
    var status by remember { mutableStateOf("") }
    var openSeries by remember { mutableStateOf<Series?>(null) }
    var tipsFilter by remember { mutableStateOf("") }
    // Zukünftige Sendung gewählt: „Erinnern" oder „Aufnehmen"?
    var choice by remember { mutableStateOf<Pair<Channel, Programme>?>(null) }
    var langLive by remember(store) { mutableStateOf(store.language("live")) }
    var langVod by remember(store) { mutableStateOf(store.language("vod")) }
    var langSer by remember(store) { mutableStateOf(store.language("ser")) }
    var voiceHandler by remember { mutableStateOf<(String) -> Unit>({}) }
    val voiceLauncher = androidx.activity.compose.rememberLauncherForActivityResult(androidx.activity.result.contract.ActivityResultContracts.StartActivityForResult()) { r ->
        r.data?.getStringArrayListExtra(android.speech.RecognizerIntent.EXTRA_RESULTS)?.firstOrNull()?.let { voiceHandler(it) }
    }
    val voiceOk = remember { ctx.packageManager.queryIntentActivities(android.content.Intent(android.speech.RecognizerIntent.ACTION_RECOGNIZE_SPEECH), 0).isNotEmpty() }
    val scope = rememberCoroutineScope()
    val prof = remember(profile) { os.trendo.tv.livetv.ProfileStore(ctx).list().firstOrNull { it.id == profile } }
    LiveSession.profile = profile
    val produced by produceState(LiveCache.byProfile[profile] ?: LiveData(), tick) {
        val cached = LiveCache.byProfile[profile]
        if (cached != null && tick == 0 && LiveCache.stamp == store.sourcesAt.toInt() + store.epgAt.toInt()) { value = cached; return@produceState }
        // In Stufen laden: Sender sofort, dann Programmführer, dann Filme & Serien – vorhandene Anzeige bleibt dabei stehen.
        val t0 = android.os.SystemClock.elapsedRealtime()
        value = withContext(Dispatchers.IO) { store.dedupeSources(); loadChannels(store, prof, value) }
        val t1 = android.os.SystemClock.elapsedRealtime()
        value = value.copy(epg = withContext(Dispatchers.IO) { store.epg() })
        val t2 = android.os.SystemClock.elapsedRealtime()
        value = withContext(Dispatchers.IO) { loadVod(store, value) }
        android.util.Log.i("TrendoLiveZeit", "Sender ${value.all.size} in ${t1 - t0} ms, Programmführer in ${t2 - t1} ms, Filme ${value.vod.size}/Serien ${value.series.size} in ${android.os.SystemClock.elapsedRealtime() - t2} ms")
        LiveCache.byProfile[profile] = value; LiveCache.stamp = store.sourcesAt.toInt() + store.epgAt.toInt()
        // Aufnahme-Regeln auf den frischen Programmführer anwenden.
        withContext(Dispatchers.IO) { os.trendo.tv.Recordings.applyRules(ctx, value.visible, value.epg, System.currentTimeMillis()) }
    }
    // Einstellungen geändert (Favorit, ausblenden, sperren …): nur im Speicher neu filtern – sofort sichtbar, ohne alles neu einzulesen.
    var override by remember { mutableStateOf<LiveData?>(null) }
    LaunchedEffect(produced) { override = null }
    val data = override ?: produced
    fun settingsChanged() {
        val base = data
        scope.launch {
            override = withContext(Dispatchers.Default) {
                val st = os.trendo.tv.livetv.KidRules.effective(store.settings().copy(onlyGerman = false), prof)
                val vis = TvRules.visible(base.all, st).let { v -> v.filter { it.id in st.favorites } + v.filter { it.id !in st.favorites } }
                base.copy(settings = st, visible = vis, reminders = store.reminders())
            }
            LiveCache.byProfile[profile] = override!!
        }
    }
    // Still im Hintergrund aktualisieren: Senderliste höchstens 1× am Tag, Programmführer alle 12 Stunden.
    LaunchedEffect(data.loaded) {
        if (!data.loaded || data.sources.isEmpty()) return@LaunchedEffect
        val now = System.currentTimeMillis()
        if (now - store.sourcesAt > 24 * 3_600_000L || now - store.epgAt > 12 * 3_600_000L) {
            val r = withContext(Dispatchers.IO) { repo.refresh() }
            if (r.errors.isNotEmpty()) status = "Aktualisierung: " + r.errors.joinToString("; ")
            tick++
        }
    }

    fun refresh() {
        status = "Lade Sender, Programmführer, Filme und Serien …"
        scope.launch {
            val r = withContext(Dispatchers.IO) { repo.refresh() }
            status = "Fertig: ${r.channels} Sender, ${r.programmes} Sendungen, ${r.vod} Filme, ${r.series} Serien." +
                if (r.errors.isNotEmpty()) " Probleme: " + r.errors.joinToString("; ") else ""
            tick++
        }
    }

    fun playAt(c: Channel, start: Long) {
        PreviewHub.release()
        LiveSession.channels = data.visible; LiveSession.allChannels = data.all; LiveSession.epg = data.epg; LiveSession.locked = data.settings.lockedGroups; LiveSession.profile = profile
        LiveSession.favorites = data.settings.favorites
        ctx.startActivity(LiveActivity.intent(ctx, data.visible.indexOf(c).coerceAtLeast(0), start))
    }

    /** Sprachbefehl ausführen (lokal, feste Regeln). */
    fun onVoice(text: String) {
        when (val cmd = os.trendo.tv.livetv.LiveVoice.parse(text, data.visible, data.epg, System.currentTimeMillis())) {
            is os.trendo.tv.livetv.LiveCommand.Play -> { status = "„$text“ → ${cmd.channel.name}"; playAt(cmd.channel, 0) }
            is os.trendo.tv.livetv.LiveCommand.Remind -> { toggleReminder(ctx, store, data, cmd.tip.channel, cmd.tip.programme); status = "Erinnerung gesetzt: ${cmd.tip.programme.title}"; tick++ }
            is os.trendo.tv.livetv.LiveCommand.Tab -> { tab = runCatching { LiveTab.valueOf(cmd.name) }.getOrDefault(LiveTab.TIPS); tipsFilter = cmd.filter; status = "„$text“" }
            is os.trendo.tv.livetv.LiveCommand.Search -> { tab = LiveTab.SEARCH; status = "Suche: ${cmd.query} – bitte im Suchfeld eingeben." }
            is os.trendo.tv.livetv.LiveCommand.Unknown -> status = "Nicht verstanden: „$text“. Beispiele: „Schalte auf ZDF“, „Was läuft heute Abend für Krimis?“, „Erinnere mich an Tatort“."
        }
    }

    fun play(c: Channel) {
        PreviewHub.release()
        LiveSession.channels = data.visible; LiveSession.allChannels = data.all; LiveSession.epg = data.epg; LiveSession.locked = data.settings.lockedGroups; LiveSession.profile = profile
        ctx.startActivity(LiveActivity.intent(ctx, data.visible.indexOf(c).coerceAtLeast(0)))
    }

    // Kinderprofil: keine Quellen, keine Filter, keine Streaming-Dienste.
    val railTabs = if (prof?.kid == true) listOf(LiveTab.CHANNELS, LiveTab.MOVIES, LiveTab.SERIES, LiveTab.GUIDE, LiveTab.TIPS) else LiveTab.entries.filter { it != LiveTab.FILTER }
    var menuFor by remember { mutableStateOf<Channel?>(null) }
    Row(Modifier.fillMaxSize()) {
    NavRail(railTabs, if (tab == LiveTab.FILTER) LiveTab.SOURCES else tab, { tab = it },
        voice = if (voiceOk) ({
            voiceHandler = { onVoice(it) }
            runCatching { voiceLauncher.launch(android.content.Intent(android.speech.RecognizerIntent.ACTION_RECOGNIZE_SPEECH)
                .putExtra(android.speech.RecognizerIntent.EXTRA_LANGUAGE_MODEL, android.speech.RecognizerIntent.LANGUAGE_MODEL_FREE_FORM)
                .putExtra(android.speech.RecognizerIntent.EXTRA_LANGUAGE, "de-DE")
                .putExtra(android.speech.RecognizerIntent.EXTRA_PROMPT, "z. B. „Schalte auf ZDF“")) }
        }) else null,
        external = host.externalLabel?.let { it to host.onExternal })
    Spacer(Modifier.width(12.dp))
    Column(Modifier.weight(1f).fillMaxHeight()) {
        // Einstellungen: Unterbereiche
        if (tab == LiveTab.SOURCES || tab == LiveTab.FILTER) Row(horizontalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.padding(bottom = 6.dp)) {
            PillButton(if (tab == LiveTab.SOURCES) "● Quellen" else "Quellen", { tab = LiveTab.SOURCES })
            PillButton(if (tab == LiveTab.FILTER) "● Profile, Jugendschutz & Sortierung" else "Profile, Jugendschutz & Sortierung", { tab = LiveTab.FILTER })
        }
        if (false) LazyRow {
            val tabs = emptyList<LiveTab>()
            items(tabs) { t ->
                FocusSurface({ tab = t }, Modifier.height(34.dp).then(if (t == tab) Modifier.border(1.5.dp, TvColors.FocusRing, RoundedCornerShape(50)) else Modifier),
                    shape = RoundedCornerShape(50), container = if (t == tab) TvColors.Panel else TvColors.Glass, scale = 1.06f) {
                    Box(Modifier.padding(horizontal = 18.dp).fillMaxHeight(), contentAlignment = Alignment.Center) {
                        Text(t.label, fontSize = 12.sp, fontWeight = if (t == tab) FontWeight.Bold else FontWeight.Normal, color = if (t == tab) TvColors.Cyan else TvColors.Text)
                    }
                }
            }
            if (voiceOk) item {
                FocusSurface({
                    voiceHandler = { onVoice(it) }
                    runCatching { voiceLauncher.launch(android.content.Intent(android.speech.RecognizerIntent.ACTION_RECOGNIZE_SPEECH)
                        .putExtra(android.speech.RecognizerIntent.EXTRA_LANGUAGE_MODEL, android.speech.RecognizerIntent.LANGUAGE_MODEL_FREE_FORM)
                        .putExtra(android.speech.RecognizerIntent.EXTRA_LANGUAGE, "de-DE")
                        .putExtra(android.speech.RecognizerIntent.EXTRA_PROMPT, "z. B. „Schalte auf ZDF“")) }
                }, Modifier.height(34.dp), shape = RoundedCornerShape(50), scale = 1.06f) {
                    Box(Modifier.padding(horizontal = 14.dp).fillMaxHeight(), contentAlignment = Alignment.Center) { Text("🎤 Sprechen", fontSize = 12.sp, color = TvColors.Text) }
                }
            }
            host.externalLabel?.let { label -> item {
                FocusSurface(host.onExternal, Modifier.height(30.dp), shape = RoundedCornerShape(50), scale = 1.05f) {
                    Box(Modifier.padding(horizontal = 14.dp).fillMaxHeight(), contentAlignment = Alignment.Center) { Text(label, fontSize = 11.sp, color = TvColors.Text) }
                }
            } }
        }
        menuFor?.let { c -> ChannelMenu(c, data, store, ctx, close = { menuFor = null; settingsChanged() }, playAt = { ch, st -> menuFor = null; playAt(ch, st) },
            multi = { ch -> menuFor = null; PreviewHub.release(); LiveSession.channels = data.visible; LiveSession.favorites = data.settings.favorites
                ctx.startActivity(os.trendo.tv.MultiViewActivity.intent(ctx, data.visible.indexOf(ch).coerceAtLeast(0), 2)) },
            status = { status = it }) }
        choice?.let { (c, p) ->
            val focus = remember { FocusRequester() }
            LaunchedEffect(p) { runCatching { focus.requestFocus() } }
            Row(Modifier.fillMaxWidth().background(TvColors.GlassStrong, RoundedCornerShape(10.dp)).border(1.dp, TvColors.FocusRing, RoundedCornerShape(10.dp)).padding(8.dp),
                verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("„${p.title}“ · ${c.name} · ${hm(p.start)}", color = TvColors.Text, fontSize = 12.sp, modifier = Modifier.weight(1f), maxLines = 1)
                Box(Modifier.focusRequester(focus)) { PillButton("⏰ Erinnern", { toggleReminder(ctx, store, data, c, p); choice = null; tick++ }) }
                PillButton("⏺ Aufnehmen (USB)", {
                    status = if (os.trendo.tv.Recordings.usbDir(ctx) == null) "Kein USB-Stick gefunden – bitte einstecken."
                        else if (os.trendo.tv.Recordings.schedule(ctx, c, p) != null) "Aufnahme geplant: ${p.title} um ${hm(p.start)}" else "Ist schon geplant."
                    choice = null
                })
                PillButton("Abbrechen", { choice = null })
            }
            Spacer(Modifier.height(4.dp))
        }
        if (status.isNotEmpty()) Text(status, color = TvColors.TextSecondary, fontSize = 10.sp, maxLines = 2, modifier = Modifier.padding(vertical = 4.dp))
        Spacer(Modifier.height(6.dp))
        if (!data.loaded) { Text("Gespeicherte Sender werden geöffnet …", color = TvColors.TextSecondary, fontSize = 11.sp); return@Column }
        if (data.loaded && data.sources.isEmpty() && tab != LiveTab.SOURCES) {
            Info("Noch keine Senderquelle. Füge unter „Quellen“ deine eigene M3U-Liste oder deinen Xtream-Zugang hinzu – genau wie in Trendo OS für Windows. Nutze nur Quellen, die du rechtmäßig nutzen darfst.")
            Spacer(Modifier.height(8.dp)); PillButton("Quelle hinzufügen", { tab = LiveTab.SOURCES })
            return@Column
        }
        when (tab) {
            LiveTab.CHANNELS -> {
                val history = remember(data.visible) { store.history() }
                val langs = remember(data.visible) { TvRules.languages(TvRules.groups(data.visible)) }
                LanguageBar(langs, langLive) { langLive = it; store.setLanguage("live", it) }
                val shownCh = remember(data.visible, langLive) { data.visible.filter { TvRules.matchesLanguage(it.group, it.name, langLive) } }
                val groups = remember(shownCh) {
                    val real = TvRules.groups(shownCh)
                    listOf("★ Favoriten", "🕘 Zuletzt gesehen", "Alle") + os.trendo.tv.livetv.Providers.present(real).map { "▸ $it" } + real
                }
                val byGroup = remember(shownCh) { shownCh.groupBy { it.group } }
                val byProvider = remember(shownCh) { shownCh.groupBy { os.trendo.tv.livetv.Providers.of(it.group) } }
                LiveChannelsView(shownCh, groups, data.epg, data.settings.favorites, data.settings.lockedGroups,
                    groupFilter = { g -> when (g) {
                        "Alle" -> shownCh
                        "★ Favoriten" -> shownCh.filter { it.id in data.settings.favorites }
                        "🕘 Zuletzt gesehen" -> { val byId = shownCh.associateBy { it.id }; history.mapNotNull { byId[it] } }
                        else -> if (g.startsWith("▸ ")) byProvider[g.removePrefix("▸ ")].orEmpty() else byGroup[g].orEmpty()
                    } },
                    play = ::play,
                    onFav = { c -> store.save(data.settings.copy(favorites = data.settings.favorites.let { if (c.id in it) it - c.id else it + c.id })); settingsChanged() },
                    autofocus = data.loaded && menuFor == null, onMenu = { menuFor = it })
            }
            LiveTab.GUIDE -> {
                val rows = data.visible.filter { it.epgId != null && data.epg[it.epgId]?.isNotEmpty() == true }.take(120)
                if (rows.isEmpty()) Info("Kein Programmführer geladen. Unter „Quellen“ auf „Alles aktualisieren“ gehen (bei M3U eine EPG-Adresse eintragen).")
                else GuideView(rows, data.epg, data.reminders.map { it.title to it.start }.toSet(), ::play, catchup = { c, start -> playAt(c, start) }) { c, p -> choice = c to p }
            }
            LiveTab.TIPS -> TipsView(remember(data.epg, data.visible) { os.trendo.tv.livetv.Tips.rows(data.visible, data.epg, data.settings.favorites, System.currentTimeMillis(), data.settings.kidsMode) }, tipsFilter) { t ->
                if (t.programme.isOn(System.currentTimeMillis())) playAt(t.channel, 0) else choice = t.channel to t.programme
            }
            LiveTab.STREAMING -> StreamingView(host.playVod)
            LiveTab.VIDEOS -> VideosView()
            LiveTab.RECORDINGS -> RecordingsTab(ctx, host)
            LiveTab.SEARCH -> SearchTab(data, ::play) { c, p -> toggleReminder(ctx, store, data, c, p); tick++ }
            LiveTab.MOVIES -> Column {
                val hidden = data.settings.hiddenGroups
                val langsV = remember(data.vod) { TvRules.languages(data.vod.map { it.category }.distinct()) }
                LanguageBar(langsV, langVod) { langVod = it; store.setLanguage("vod", it) }
                val itemsV = remember(data.vod, langVod, hidden) {
                    data.vod.filter { it.category !in hidden && TvRules.matchesLanguage(it.category, it.name, langVod) }
                        .map { v -> PosterItem(v.id, v.name, v.category, v.cover, v.year, v.rating, v.plot) { host.playVod(v.url, v.name) } }
                }
                PosterRowsView(itemsV,
                "Keine Filme in dieser Sprache. Filme kommen über einen Xtream-Zugang (Quellen → Alles aktualisieren).", autofocus = true)
            }
            LiveTab.SERIES -> if (openSeries != null) SeriesTab(listOf(openSeries!!), repo, host, initial = openSeries) { openSeries = null }
                else Column {
                    val hidden = data.settings.hiddenGroups
                    val langsS = remember(data.series) { TvRules.languages(data.series.map { it.category }.distinct()) }
                    LanguageBar(langsS, langSer) { langSer = it; store.setLanguage("ser", it) }
                    val itemsS = remember(data.series, langSer, hidden) {
                        data.series.filter { it.category !in hidden && TvRules.matchesLanguage(it.category, it.name, langSer) }
                            .map { s -> PosterItem(s.id, s.name, s.category, s.cover, s.year, s.rating, s.plot) { openSeries = s } }
                    }
                    PosterRowsView(itemsS,
                    "Keine Serien in dieser Sprache. Serien kommen über einen Xtream-Zugang (Quellen → Alles aktualisieren).", autofocus = true)
                }
            LiveTab.SOURCES -> SourcesTab(data, repo, store, onChanged = { tick++ }, onRefresh = ::refresh)
            LiveTab.FILTER -> Column {
                ProfilesSection(ctx, store)
                FilterTab(data, store) { settingsChanged() }
            }
        }
    }
    }
}

private fun railIcon(t: LiveTab): androidx.compose.ui.graphics.vector.ImageVector = when (t) {
    LiveTab.CHANNELS -> androidx.compose.material.icons.Icons.Outlined.LiveTv
    LiveTab.MOVIES -> androidx.compose.material.icons.Icons.Outlined.Movie
    LiveTab.SERIES -> androidx.compose.material.icons.Icons.Outlined.VideoLibrary
    LiveTab.GUIDE -> androidx.compose.material.icons.Icons.Outlined.DateRange
    LiveTab.TIPS -> androidx.compose.material.icons.Icons.Outlined.Star
    LiveTab.STREAMING -> androidx.compose.material.icons.Icons.Outlined.Subscriptions
    LiveTab.VIDEOS -> androidx.compose.material.icons.Icons.Outlined.SmartDisplay
    LiveTab.RECORDINGS -> androidx.compose.material.icons.Icons.Outlined.FiberManualRecord
    LiveTab.SEARCH -> androidx.compose.material.icons.Icons.Outlined.Search
    LiveTab.SOURCES, LiveTab.FILTER -> androidx.compose.material.icons.Icons.Outlined.Settings
}

private fun railLabel(t: LiveTab) = if (t == LiveTab.SOURCES) "Einstellungen" else t.label

/** Menüleiste links: schmal mit Symbolen, klappt mit Beschriftung auf, sobald sie den Fokus hat. */
@Composable
private fun NavRail(tabs: List<LiveTab>, current: LiveTab, onSelect: (LiveTab) -> Unit, voice: (() -> Unit)?, external: Pair<String, () -> Unit>?) {
    var open by remember { mutableStateOf(false) }
    Column(Modifier.width(if (open) 210.dp else 58.dp).fillMaxHeight().background(TvColors.GlassStrong, RoundedCornerShape(14.dp))
        .border(1.dp, TvColors.Line, RoundedCornerShape(14.dp)).padding(vertical = 8.dp, horizontal = 6.dp)
        .onFocusChanged { open = it.hasFocus },
        verticalArrangement = Arrangement.spacedBy(3.dp)) {
        @Composable fun item(icon: androidx.compose.ui.graphics.vector.ImageVector, label: String, selected: Boolean, onClick: () -> Unit) {
            FocusSurface(onClick, Modifier.fillMaxWidth().height(36.dp).then(if (selected) Modifier.border(1.5.dp, TvColors.FocusRing, RoundedCornerShape(9.dp)) else Modifier),
                shape = RoundedCornerShape(9.dp), container = if (selected) TvColors.Panel else Color.Transparent, scale = 1.04f, idleBorder = false) {
                Row(Modifier.fillMaxSize().padding(horizontal = 10.dp), verticalAlignment = Alignment.CenterVertically) {
                    androidx.tv.material3.Icon(icon, label, tint = if (selected) TvColors.Cyan else TvColors.Text, modifier = Modifier.size(18.dp))
                    if (open) { Spacer(Modifier.width(12.dp)); Text(label, color = if (selected) TvColors.Cyan else TvColors.Text, fontSize = 12.sp, maxLines = 1, fontWeight = if (selected) FontWeight.Bold else FontWeight.Normal) }
                }
            }
        }
        tabs.forEach { t -> item(railIcon(t), railLabel(t), t == current) { onSelect(t) } }
        Spacer(Modifier.weight(1f))
        voice?.let { item(androidx.compose.material.icons.Icons.Outlined.Mic, "Sprechen", false, it) }
        external?.let { (label, act) -> item(androidx.compose.material.icons.Icons.Outlined.OpenInNew, label, false, act) }
    }
}

/** Kontextmenü (langes Drücken auf OK): alles, was man mit einem Sender machen kann. */
@Composable
private fun ChannelMenu(c: Channel, data: LiveData, store: LiveStore, ctx: Context, close: () -> Unit, playAt: (Channel, Long) -> Unit,
                        multi: (Channel) -> Unit, status: (String) -> Unit) {
    val now = System.currentTimeMillis()
    val (cur, next) = TvRules.nowNext(c.epgId?.let { data.epg[it] }, now)
    val s = data.settings
    val focus = remember { FocusRequester() }
    LaunchedEffect(c) { runCatching { focus.requestFocus() } }
    val items = buildList<Pair<String, () -> Unit>> {
        add((if (c.id in s.favorites) "★ Aus Favoriten entfernen" else "☆ Zu Favoriten") to { store.save(s.copy(favorites = if (c.id in s.favorites) s.favorites - c.id else s.favorites + c.id)); close() })
        if (cur != null && TvRules.canCatchup(c, cur.start, now)) add("⏮ „${cur.title}“ von Anfang an" to { playAt(c, cur.start) })
        if (cur != null) add("⏺ „${cur.title}“ aufnehmen (USB)" to {
            status(if (os.trendo.tv.Recordings.usbDir(ctx) == null) "Kein USB-Stick gefunden." else { os.trendo.tv.Recordings.now(ctx, c, cur.title, cur.stop); "Aufnahme gestartet: ${cur.title}" }); close()
        })
        if (next != null) add("⏰ An „${next.title}“ (${hm(next.start)}) erinnern" to { toggleReminder(ctx, store, data, c, next); close() })
        if (next != null) add("⏺ „${next.title}“ aufnehmen planen" to {
            status(if (os.trendo.tv.Recordings.usbDir(ctx) == null) "Kein USB-Stick gefunden." else if (os.trendo.tv.Recordings.schedule(ctx, c, next) != null) "Aufnahme geplant: ${next.title}" else "Ist schon geplant."); close()
        })
        add("▦ Multi-View mit diesem Sender" to { multi(c) })
        add("🚫 Sender ausblenden" to { store.save(s.copy(hiddenChannels = s.hiddenChannels + c.id)); status("„${c.name}“ ausgeblendet – unter Einstellungen wieder einblenden."); close() })
        add("🚫 Gruppe „${c.group}“ ausblenden" to { store.save(s.copy(hiddenGroups = s.hiddenGroups + c.group)); close() })
        add("Schließen" to close)
    }
    Column(Modifier.fillMaxWidth().background(TvColors.GlassStrong, RoundedCornerShape(12.dp)).border(1.dp, TvColors.FocusRing, RoundedCornerShape(12.dp)).padding(10.dp),
        verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Text(c.name + (cur?.let { "  ·  jetzt: ${it.title}" } ?: ""), color = TvColors.Text, fontSize = 13.sp, fontWeight = FontWeight.Bold, maxLines = 1)
        LazyRow(horizontalArrangement = Arrangement.spacedBy(6.dp), contentPadding = PaddingValues(2.dp)) {
            items(items.size) { i -> Box(if (i == 0) Modifier.focusRequester(focus) else Modifier) { PillButton(items[i].first, items[i].second) } }
        }
    }
    Spacer(Modifier.height(6.dp))
}

/** Aufnahmen: Liste (abspielen, löschen, abbrechen), Aufnahme-Regeln, USB-Status. */
@Composable
private fun RecordingsTab(ctx: Context, host: LiveHost) {
    var tick by remember { mutableIntStateOf(0) }
    val list = remember(tick) { os.trendo.tv.Recordings.list(ctx).sortedByDescending { it.start } }
    var rules by remember(tick) { mutableStateOf(os.trendo.tv.Recordings.rules(ctx)) }
    var rule by remember { mutableStateOf("") }
    val usb = remember(tick) { os.trendo.tv.Recordings.usbDir(ctx) }
    LaunchedEffect(Unit) { while (true) { kotlinx.coroutines.delay(5000); tick++ } }
    LazyColumn(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        item { Info(if (usb != null) "USB-Stick erkannt. Aufnahmen landen dort im Ordner von Trendo Live TV." else "Kein USB-Stick erkannt – zum Aufnehmen bitte einen USB-Stick einstecken. Hinweis: Viele Anbieter erlauben nur eine Verbindung gleichzeitig.") }
        item { Text("Aufnahme-Regeln (plant passende Sendungen der nächsten 24 Stunden automatisch)", color = TvColors.Text, fontSize = 12.sp, fontWeight = FontWeight.SemiBold) }
        item {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                Field(rule, { rule = it }, "Stichwort, z. B. Tatort", width = 260)
                PillButton("+ Regel", { if (rule.trim().length >= 2) { os.trendo.tv.Recordings.setRules(ctx, rules + rule.trim()); rule = ""; tick++ } })
            }
        }
        items(rules) { r -> Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            Text("• $r", color = TvColors.Text, fontSize = 11.sp, modifier = Modifier.width(260.dp)); PillButton("Entfernen", { os.trendo.tv.Recordings.setRules(ctx, rules - r); tick++ }) } }
        item { Text("Aufnahmen", color = TvColors.Text, fontSize = 12.sp, fontWeight = FontWeight.SemiBold) }
        if (list.isEmpty()) item { Text("Noch keine Aufnahmen. Im Programmführer eine kommende Sendung wählen → „Aufnehmen“.", color = TvColors.TextSecondary, fontSize = 11.sp) }
        items(list, key = { it.id }) { r ->
            Row(Modifier.fillMaxWidth().background(TvColors.GlassStrong, RoundedCornerShape(8.dp)).padding(horizontal = 12.dp, vertical = 6.dp),
                verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Column(Modifier.weight(1f)) {
                    Text(r.title, color = TvColors.Text, fontSize = 12.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                    Text("${r.channelName} · ${day(r.start)} · ${r.state}" + (if (r.bytes > 0) " · ${os.trendo.tv.media.ResumeRules.formatSize(r.bytes)}" else ""), color = TvColors.TextSecondary, fontSize = 9.sp)
                }
                if (r.state == "fertig" && r.file != null) PillButton("▶ Abspielen", { host.playVod(android.net.Uri.fromFile(java.io.File(r.file)).toString(), r.title) })
                if (r.state == "geplant" || r.state == "läuft") PillButton(if (r.state == "läuft") "Stoppen" else "Absagen", { os.trendo.tv.Recordings.cancel(ctx, r.id); tick++ })
                else PillButton("Löschen", { os.trendo.tv.Recordings.delete(ctx, r); tick++ })
            }
        }
    }
}

/** Profile verwalten: anlegen, Kinderprofil, Zeitlimit, löschen. */
@Composable
private fun ProfilesSection(ctx: Context, store: LiveStore) {
    val ps = remember { os.trendo.tv.livetv.ProfileStore(ctx) }
    var list by remember { mutableStateOf(ps.list()) }
    var name by remember { mutableStateOf("") }
    Column(Modifier.padding(bottom = 10.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
        Text("Profile", color = TvColors.Text, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
        list.forEach { p ->
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                Text((if (p.kid) "🧒 " else "👤 ") + p.name + (if (p.kid) " · " + (if (p.minutesPerDay > 0) "${p.minutesPerDay} Min./Tag" else "ohne Zeitlimit") else ""),
                    color = TvColors.Text, fontSize = 11.sp, modifier = Modifier.width(260.dp))
                if (p.kid) PillButton("Zeitlimit ändern", {
                    val next = listOf(30, 60, 90, 120, 180, 0).let { l -> l[(l.indexOf(p.minutesPerDay).coerceAtLeast(0) + 1) % l.size] }
                    ps.update(p.copy(minutesPerDay = next)); list = ps.list()
                })
                if (p.id != os.trendo.tv.livetv.ProfileStore.DEFAULT.id) PillButton("Löschen", { ps.remove(p.id); list = ps.list() })
            }
        }
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            Field(name, { name = it }, "Name für neues Profil", width = 240)
            PillButton("+ Profil", { ps.add(name, kid = false); name = ""; list = ps.list() })
            PillButton("+ Kinderprofil", {
                if (!store.hasPin()) android.widget.Toast.makeText(ctx, "Bitte zuerst unten eine PIN festlegen – sie schützt das Kinderprofil.", android.widget.Toast.LENGTH_LONG).show()
                else { ps.add(name, kid = true); name = ""; list = ps.list() }
            })
        }
    }
}

/** Sprachleiste: „Alle" + die Sprachen, die in der Liste vorkommen. */
@Composable
private fun LanguageBar(codes: List<String>, current: String?, onPick: (String?) -> Unit) {
    if (codes.isEmpty()) return
    LazyRow(horizontalArrangement = Arrangement.spacedBy(6.dp), contentPadding = PaddingValues(vertical = 2.dp, horizontal = 2.dp), modifier = Modifier.padding(bottom = 6.dp)) {
        item { Text("Sprache:", color = TvColors.TextSecondary, fontSize = 10.sp, modifier = Modifier.padding(top = 6.dp, end = 4.dp)) }
        item { PillButton(if (current == null) "● Alle" else "Alle", { onPick(null) }) }
        items(codes.take(14)) { c -> val l = TvRules.languageLabel(c); PillButton(if (current == c) "● $l" else l, { onPick(c) }) }
    }
}

private fun toggleReminder(ctx: Context, store: LiveStore, data: LiveData, c: Channel?, p: Programme) {
    val list = data.reminders
    val existing = list.firstOrNull { it.title == p.title && it.start == p.start }
    if (existing != null) { store.saveReminders(list - existing); Reminders.cancel(ctx, existing) }
    else {
        val r = Reminder(c?.id ?: p.channel, c?.name ?: p.channel, p.title, p.start)
        store.saveReminders(list + r); Reminders.schedule(ctx, r)
        android.widget.Toast.makeText(ctx, "Erinnerung: ${p.title} um ${hm(p.start)}", android.widget.Toast.LENGTH_SHORT).show()
    }
}

private fun hm(ms: Long) = SimpleDateFormat("HH:mm", Locale.GERMANY).format(Date(ms))
private fun day(ms: Long) = SimpleDateFormat("EE d.M. HH:mm", Locale.GERMANY).format(Date(ms))

@Composable
private fun Info(text: String) {
    Box(Modifier.fillMaxWidth().background(TvColors.GlassStrong, RoundedCornerShape(10.dp)).border(1.dp, TvColors.Line, RoundedCornerShape(10.dp)).padding(14.dp)) {
        Text(text, color = TvColors.Text, fontSize = 12.sp)
    }
}

// ---------- Sender ----------
@Composable
private fun ChannelsTab(data: LiveData, history: List<String>, play: (Channel) -> Unit, onFav: (Channel) -> Unit) {
    val groups = remember(data.visible) { listOf("★ Favoriten", "🕘 Zuletzt gesehen", "Alle") + TvRules.groups(data.visible) }
    var group by remember { mutableStateOf("Alle") }
    val now = System.currentTimeMillis()
    val list = when (group) {
        "Alle" -> data.visible
        "★ Favoriten" -> data.visible.filter { it.id in data.settings.favorites }
        "🕘 Zuletzt gesehen" -> { val byId = data.visible.associateBy { it.id }; history.mapNotNull { byId[it] } }
        else -> data.visible.filter { it.group == group }
    }
    val first = remember { FocusRequester() }
    LaunchedEffect(data.loaded) { runCatching { first.requestFocus() } }
    Row(Modifier.fillMaxSize()) {
        LazyColumn(Modifier.width(170.dp), verticalArrangement = Arrangement.spacedBy(3.dp)) {
            items(groups) { g ->
                val locked = g in data.settings.lockedGroups
                FocusSurface({ group = g }, Modifier.fillMaxWidth().height(28.dp), container = if (g == group) TvColors.Panel else Color.Transparent, scale = 1.03f, idleBorder = false) {
                    Box(Modifier.fillMaxSize().padding(horizontal = 8.dp), contentAlignment = Alignment.CenterStart) {
                        Text((if (locked) "🔒 " else "") + g, fontSize = 10.sp, color = if (g == group) TvColors.Cyan else TvColors.Text, maxLines = 1, overflow = TextOverflow.Ellipsis)
                    }
                }
            }
        }
        Spacer(Modifier.width(10.dp))
        if (list.isEmpty()) { Info(if (group == "★ Favoriten") "Noch keine Favoriten. Wähle bei einem Sender den Stern." else "Keine Sender – Filter prüfen oder unter „Quellen“ aktualisieren."); return@Row }
        LazyColumn(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(4.dp)) {
            items(list, key = { it.id }) { c ->
                val (cur, next) = TvRules.nowNext(c.epgId?.let { data.epg[it] }, now)
                Row(verticalAlignment = Alignment.CenterVertically) {
                    FocusSurface({ play(c) }, Modifier.weight(1f).height(48.dp).then(if (c == list.first()) Modifier.focusRequester(first) else Modifier), scale = 1.02f) {
                        Row(Modifier.fillMaxSize().padding(horizontal = 10.dp), verticalAlignment = Alignment.CenterVertically) {
                            Text("${c.number}", color = TvColors.Cyan, fontSize = 11.sp, modifier = Modifier.width(38.dp))
                            Box(Modifier.size(width = 52.dp, height = 30.dp), contentAlignment = Alignment.Center) { NetImage(c.logo, Modifier.fillMaxSize(), 120, 70, crop = false) }
                            Spacer(Modifier.width(8.dp))
                            Column(Modifier.weight(1f)) {
                                Text((if (c.group in data.settings.lockedGroups) "🔒 " else "") + c.name, color = TvColors.Text, fontSize = 12.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                                Text(cur?.let { "${hm(it.start)} ${it.title}" + (next?.let { n -> " · danach ${n.title}" } ?: "") } ?: c.group,
                                    color = TvColors.TextSecondary, fontSize = 9.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                                cur?.let { p -> Box(Modifier.padding(top = 2.dp).width(200.dp).height(2.dp).background(TvColors.Line)) { Box(Modifier.fillMaxWidth(p.progress(now)).fillMaxHeight().background(TvColors.Prism)) } }
                            }
                        }
                    }
                    Spacer(Modifier.width(6.dp))
                    FocusSurface({ onFav(c) }, Modifier.size(36.dp), shape = RoundedCornerShape(50), scale = 1.1f) {
                        Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) { Text(if (c.id in data.settings.favorites) "★" else "☆", color = TvColors.Cyan, fontSize = 15.sp) }
                    }
                }
            }
        }
    }
}

// ---------- Programmführer (Zeitraster) ----------
@Composable
private fun GuideTab(data: LiveData, play: (Channel) -> Unit, remind: (Channel, Programme) -> Unit) {
    val now = System.currentTimeMillis()
    val from = now - now % (30 * 60_000L)
    val to = from + 3 * 3_600_000L
    val pxPerMin = 3.2f
    val rows = data.visible.filter { it.epgId != null && data.epg[it.epgId]?.isNotEmpty() == true }.take(80)
    if (rows.isEmpty()) { Info("Kein Programmführer geladen. Unter „Quellen“ auf „Alles aktualisieren“ gehen (bei M3U eine EPG-Adresse eintragen)."); return }
    val first = remember { FocusRequester() }
    LaunchedEffect(Unit) { runCatching { first.requestFocus() } }
    Column {
        Row { Spacer(Modifier.width(130.dp)); for (h in 0 until 6) Text(hm(from + h * 30 * 60_000L), color = TvColors.TextSecondary, fontSize = 9.sp, modifier = Modifier.width((30 * pxPerMin).dp)) }
        LazyColumn(verticalArrangement = Arrangement.spacedBy(3.dp)) {
            items(rows, key = { it.id }) { c ->
                Row(verticalAlignment = Alignment.CenterVertically) {
                    FocusSurface({ play(c) }, Modifier.width(126.dp).height(36.dp), scale = 1.03f) {
                        Box(Modifier.fillMaxSize().padding(horizontal = 6.dp), contentAlignment = Alignment.CenterStart) { Text(c.name, fontSize = 10.sp, color = TvColors.Text, maxLines = 1, overflow = TextOverflow.Ellipsis) }
                    }
                    Spacer(Modifier.width(4.dp))
                    LazyRow(horizontalArrangement = Arrangement.spacedBy(2.dp)) {
                        val progs = data.epg[c.epgId].orEmpty().filter { it.stop > from && it.start < to }
                        items(progs, key = { it.start }) { p ->
                            val mins = ((minOf(p.stop, to) - maxOf(p.start, from)) / 60_000L).coerceAtLeast(8)
                            val on = p.isOn(now)
                            val rem = data.reminders.any { it.title == p.title && it.start == p.start }
                            FocusSurface({ if (on) play(c) else remind(c, p) }, Modifier.width((mins * pxPerMin).dp).height(36.dp)
                                .then(if (c == rows.first() && p == progs.first()) Modifier.focusRequester(first) else Modifier),
                                container = if (on) TvColors.Panel else TvColors.Glass, scale = 1.03f, shape = RoundedCornerShape(5.dp)) {
                                Column(Modifier.fillMaxSize().padding(horizontal = 5.dp), verticalArrangement = Arrangement.Center) {
                                    Text((if (rem) "⏰ " else "") + p.title, fontSize = 9.sp, color = if (on) TvColors.Cyan else TvColors.Text, maxLines = 1, overflow = TextOverflow.Ellipsis)
                                    Text("${hm(p.start)}–${hm(p.stop)}", fontSize = 7.5.sp, color = TvColors.TextSecondary, maxLines = 1)
                                }
                            }
                        }
                    }
                }
            }
        }
        Text("Laufende Sendung: OK schaut. Spätere Sendung: OK setzt oder löscht eine Erinnerung (⏰).", color = TvColors.TextSecondary, fontSize = 9.sp)
    }
}

// ---------- Sendung suchen ----------
@Composable
private fun SearchTab(data: LiveData, play: (Channel) -> Unit, remind: (Channel?, Programme) -> Unit) {
    var q by remember { mutableStateOf("") }
    val byEpg = remember(data.visible) { data.visible.filter { it.epgId != null }.associateBy { it.epgId!! } }
    val now = System.currentTimeMillis()
    val hits = remember(q, data.epg) {
        if (q.trim().length < 2) emptyList() else data.epg.values.flatten().filter { it.stop > now && (it.title.contains(q.trim(), true) || it.description.contains(q.trim(), true)) }
            .sortedBy { it.start }.take(60)
    }
    Field(q, { q = it }, "Titel suchen, z. B. Tatort, Tagesschau, FC Bayern", autofocus = true)
    Spacer(Modifier.height(8.dp))
    LazyColumn(verticalArrangement = Arrangement.spacedBy(4.dp)) {
        items(hits, key = { it.channel + it.start }) { p ->
            val c = byEpg[p.channel]
            FocusSurface({ if (p.isOn(now) && c != null) play(c) else remind(c, p) }, Modifier.fillMaxWidth().height(40.dp), scale = 1.02f) {
                Row(Modifier.fillMaxSize().padding(horizontal = 10.dp), verticalAlignment = Alignment.CenterVertically) {
                    Text(day(p.start), color = TvColors.Cyan, fontSize = 10.sp, modifier = Modifier.width(110.dp))
                    Text(p.title, color = TvColors.Text, fontSize = 12.sp, modifier = Modifier.weight(1f), maxLines = 1, overflow = TextOverflow.Ellipsis)
                    Text(c?.name ?: p.channel, color = TvColors.TextSecondary, fontSize = 10.sp, maxLines = 1)
                }
            }
        }
    }
}

// ---------- Filme ----------
@Composable
private fun MoviesTab(list: List<Vod>, host: LiveHost) {
    if (list.isEmpty()) { Info("Keine Filme. Filme kommen über einen Xtream-Zugang (Quellen → Alles aktualisieren)."); return }
    val cats = remember(list) { listOf("Alle") + list.map { it.category }.distinct() }
    var cat by remember { mutableStateOf("Alle") }
    var q by remember { mutableStateOf("") }
    val shown = list.filter { (cat == "Alle" || it.category == cat) && (q.isBlank() || it.name.contains(q.trim(), true)) }.take(300)
    Row(verticalAlignment = Alignment.CenterVertically) {
        Field(q, { q = it }, "Film suchen …", width = 260)
        Spacer(Modifier.width(8.dp))
        LazyRow(horizontalArrangement = Arrangement.spacedBy(6.dp)) { items(cats.take(60)) { c -> PillButton(c, { cat = c }) } }
    }
    Spacer(Modifier.height(8.dp))
    LazyVerticalGrid(GridCells.Fixed(7), horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(8.dp), contentPadding = PaddingValues(4.dp)) {
        items(shown, key = { it.id }) { v ->
            FocusSurface({ host.playVod(v.url, v.name) }, Modifier.fillMaxWidth().height(150.dp), container = TvColors.Panel, scale = 1.08f) {
                Box(Modifier.fillMaxSize()) {
                    NetImage(v.cover, Modifier.fillMaxSize(), 200, 300)
                    Column(Modifier.align(Alignment.BottomStart).fillMaxWidth().background(Color(0xCC050A14)).padding(4.dp)) {
                        Text(v.name, fontSize = 8.5.sp, color = TvColors.Text, maxLines = 2, overflow = TextOverflow.Ellipsis)
                        Text(listOfNotNull(v.year, v.rating?.let { "★ $it" }).joinToString(" · "), fontSize = 7.sp, color = TvColors.TextSecondary)
                    }
                }
            }
        }
    }
}

// ---------- Serien ----------
@Composable
private fun SeriesTab(list: List<Series>, repo: LiveRepo, host: LiveHost, initial: Series? = null, onBack: () -> Unit = {}) {
    var open by remember { mutableStateOf(initial) }
    var eps by remember { mutableStateOf<List<Episode>?>(null) }
    val scope = rememberCoroutineScope()
    val o = open
    if (o != null) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            PillButton("‹ Zurück", { open = null; eps = null; onBack() })
            Spacer(Modifier.width(10.dp))
            Text(o.name, color = TvColors.Text, fontSize = 16.sp, fontWeight = FontWeight.Bold)
        }
        o.plot?.let { Text(it, color = TvColors.TextSecondary, fontSize = 10.sp, maxLines = 3, modifier = Modifier.padding(vertical = 6.dp)) }
        val e = eps
        if (e == null) Text("Lade Folgen …", color = TvColors.TextSecondary, fontSize = 11.sp)
        else LazyColumn(verticalArrangement = Arrangement.spacedBy(4.dp)) {
            items(e, key = { it.url }) { ep ->
                FocusSurface({ host.playVod(ep.url, "${o.name} S${ep.season}E${ep.episode}") }, Modifier.fillMaxWidth().height(38.dp), scale = 1.02f) {
                    Row(Modifier.fillMaxSize().padding(horizontal = 10.dp), verticalAlignment = Alignment.CenterVertically) {
                        Text("S${ep.season} · F${ep.episode}", color = TvColors.Cyan, fontSize = 10.sp, modifier = Modifier.width(80.dp))
                        Text(ep.title, color = TvColors.Text, fontSize = 12.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                    }
                }
            }
        }
        LaunchedEffect(o) { eps = withContext(Dispatchers.IO) { repo.episodes(o) } }
        return
    }
    if (list.isEmpty()) { Info("Keine Serien. Serien kommen über einen Xtream-Zugang (Quellen → Alles aktualisieren)."); return }
    var q by remember { mutableStateOf("") }
    Field(q, { q = it }, "Serie suchen …", width = 260)
    Spacer(Modifier.height(8.dp))
    LazyVerticalGrid(GridCells.Fixed(7), horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(8.dp), contentPadding = PaddingValues(4.dp)) {
        items(list.filter { q.isBlank() || it.name.contains(q.trim(), true) }.take(300), key = { it.id }) { s ->
            FocusSurface({ open = s; scope.launch { } }, Modifier.fillMaxWidth().height(150.dp), container = TvColors.Panel, scale = 1.08f) {
                Box(Modifier.fillMaxSize()) {
                    NetImage(s.cover, Modifier.fillMaxSize(), 200, 300)
                    Text(s.name, fontSize = 8.5.sp, color = TvColors.Text, maxLines = 2, modifier = Modifier.align(Alignment.BottomStart).fillMaxWidth().background(Color(0xCC050A14)).padding(4.dp))
                }
            }
        }
    }
}

// ---------- Quellen ----------
@Composable
private fun SourcesTab(data: LiveData, repo: LiveRepo, store: LiveStore, onChanged: () -> Unit, onRefresh: () -> Unit) {
    var kind by remember { mutableStateOf(SourceKind.XTREAM) }
    var name by remember { mutableStateOf("") }
    var url by remember { mutableStateOf("") }
    var epg by remember { mutableStateOf("") }
    var user by remember { mutableStateOf("") }
    var pass by remember { mutableStateOf("") }
    val ctx = LocalContext.current
    var phone by remember { mutableStateOf<os.trendo.tv.phone.PhoneInput?>(null) }
    var phoneUrl by remember { mutableStateOf<String?>(null) }
    var phoneMsg by remember { mutableStateOf("") }
    androidx.compose.runtime.DisposableEffect(Unit) { onDispose { phone?.stop() } }
    fun startPhone() {
        phone?.stop()
        val main = android.os.Handler(android.os.Looper.getMainLooper())
        val p = os.trendo.tv.phone.PhoneInput(ctx, "Senderquelle hinzufügen", listOf(
            os.trendo.tv.phone.PhoneInput.Field("link", "Komplette Adresse (get.php- oder M3U-Link)"),
            os.trendo.tv.phone.PhoneInput.Field("name", "Name (optional), z. B. Mein Anbieter"),
        )) { v ->
            val link = v["link"].orEmpty(); val n = v["name"].orEmpty()
            if (link.startsWith("http", true)) {
                val t = os.trendo.tv.livetv.Xtream.fromPlaylistUrl(link)
                if (t != null) repo.addXtream(n, t.first, t.second, t.third) else repo.addM3u(n, link, null)
                main.post { phoneUrl = null; phone = null; phoneMsg = "Vom Handy empfangen – lade Sender …"; onChanged(); onRefresh() }
            } else main.post { phoneMsg = "Das war keine gültige Adresse (muss mit http beginnen)."; phoneUrl = null; phone = null }
        }
        phoneUrl = p.start(); phone = p
        phoneMsg = if (phoneUrl == null) "Kein Heimnetz gefunden – ist der Fernseher per WLAN/LAN verbunden?" else ""
    }
    LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        item {
            Row(verticalAlignment = Alignment.CenterVertically) {
                PillButton(if (phoneUrl == null) "📱 Vom Handy einfügen (QR-Code)" else "QR-Code schließen", { if (phoneUrl == null) startPhone() else { phone?.stop(); phone = null; phoneUrl = null } })
                Spacer(Modifier.width(10.dp))
                Text(phoneMsg, color = TvColors.TextSecondary, fontSize = 10.sp)
            }
        }
        phoneUrl?.let { u -> item {
            Row(verticalAlignment = Alignment.CenterVertically) {
                QrCode(u, 170.dp)
                Spacer(Modifier.width(16.dp))
                Column {
                    Text("Mit dem Handy scannen", color = TvColors.Text, fontSize = 15.sp, fontWeight = FontWeight.Bold)
                    Text("Handy im selben WLAN. Auf der Seite deine Adresse einfügen und „Hinzufügen“ tippen.", color = TvColors.TextSecondary, fontSize = 11.sp)
                    Spacer(Modifier.height(6.dp))
                    Text(u, color = TvColors.Cyan, fontSize = 10.sp)
                    Text("Gilt 5 Minuten und nur für eine Eingabe.", color = TvColors.TextSecondary, fontSize = 9.sp)
                }
            }
        } }
        item {
            Text("Nur eigene, rechtmäßige Quellen. Passwörter werden verschlüsselt nur auf diesem Fernseher gespeichert und nie angezeigt.", color = TvColors.TextSecondary, fontSize = 10.sp)
        }
        items(data.sources, key = { it.id }) { s ->
            Row(Modifier.fillMaxWidth().background(TvColors.GlassStrong, RoundedCornerShape(10.dp)).border(1.dp, TvColors.Line, RoundedCornerShape(10.dp)).padding(horizontal = 14.dp, vertical = 8.dp),
                verticalAlignment = Alignment.CenterVertically) {
                Text("${s.name} · ${if (s.kind == SourceKind.XTREAM) "Xtream" else "M3U"} · ${data.all.count { it.sourceId == s.id }} Sender",
                    color = TvColors.Text, fontSize = 12.sp, modifier = Modifier.weight(1f))
                PillButton("Entfernen", { store.removeSource(s.id); onChanged() })
            }
        }
        if (data.sources.isNotEmpty()) item {
            Row { PillButton("Alles aktualisieren", onRefresh); Spacer(Modifier.width(10.dp))
                Text(if (store.epgAt > 0) "Programmführer zuletzt: ${day(store.epgAt)}" else "", color = TvColors.TextSecondary, fontSize = 10.sp) }
        }
        item {
            Text("Neue Quelle", color = TvColors.Text, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
            Spacer(Modifier.height(4.dp))
            Row { PillButton(if (kind == SourceKind.XTREAM) "● Xtream-Zugang" else "○ Xtream-Zugang", { kind = SourceKind.XTREAM }); Spacer(Modifier.width(8.dp))
                PillButton(if (kind == SourceKind.M3U) "● M3U-Adresse" else "○ M3U-Adresse", { kind = SourceKind.M3U }) }
        }
        item { Field(name, { name = it }, "Name, z. B. „Mein Anbieter“") }
        if (kind == SourceKind.XTREAM) {
            item { Field(url, { url = it }, "Server, z. B. http://anbieter.example:8080", type = KeyboardType.Uri) }
            item { Field(user, { user = it }, "Benutzername") }
            item { Field(pass, { pass = it }, "Passwort", password = true) }
        } else {
            item { Field(url, { url = it }, "Adresse der M3U-Liste (https://…)", type = KeyboardType.Uri) }
            item { Field(epg, { epg = it }, "Programmführer-Adresse XMLTV (optional)", type = KeyboardType.Uri) }
        }
        item {
            PillButton("Hinzufügen und laden", {
                if (url.isBlank() || (kind == SourceKind.XTREAM && (user.isBlank() || pass.isBlank()))) return@PillButton
                if (kind == SourceKind.XTREAM) {
                    // Wer eine komplette get.php-Adresse einfügt, bekommt Server/Benutzer/Passwort automatisch zerlegt.
                    val t = os.trendo.tv.livetv.Xtream.fromPlaylistUrl(url)
                    if (t != null) repo.addXtream(name, t.first, t.second, t.third) else repo.addXtream(name, url, user, pass)
                } else repo.addM3u(name, url, epg)
                name = ""; url = ""; epg = ""; user = ""; pass = ""
                onChanged(); onRefresh()
            })
        }
    }
}

// ---------- Filter & Jugendschutz ----------
@Composable
private fun FilterTab(data: LiveData, store: LiveStore, onChanged: () -> Unit) {
    // Sofort anzeigen, was man gedrückt hat (nicht erst nach dem Neufiltern).
    var s by remember(data.settings) { mutableStateOf(data.settings) }
    var kind by remember { mutableStateOf("live") }
    var groupQuery by remember { mutableStateOf("") }
    val allGroups = remember(data.all, data.vod, data.series, kind) {
        when (kind) { "vod" -> data.vod.map { it.category }.distinct(); "ser" -> data.series.map { it.category }.distinct(); else -> TvRules.groups(data.all) }
    }
    val groupList = remember(allGroups, groupQuery) { if (groupQuery.isBlank()) allGroups else allGroups.filter { TvRules.plain(it).contains(groupQuery.trim(), true) } }
    var pin by remember { mutableStateOf("") }
    var unlocked by remember { mutableStateOf(!store.hasPin()) }
    fun set(n: LiveSettings) { s = n; store.save(n); onChanged() }
    LazyColumn(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        if (!unlocked) {
            item { Info("Diese Einstellungen sind mit einer PIN geschützt.") }
            item { Field(pin, { pin = it.filter(Char::isDigit).take(8) }, "PIN", password = true, type = KeyboardType.NumberPassword, autofocus = true) }
            item { PillButton("Entsperren", { if (store.checkPin(pin)) { unlocked = true; LiveSession.unlocked = true; pin = "" } }) }
            return@LazyColumn
        }
        item { Toggle("Doppelte Sender zusammenführen (beste Qualität)", s.mergeDuplicates) { set(s.copy(mergeDuplicates = it)) } }
        item { Toggle("Kinder-Modus (nur Kindersender und -inhalte)", s.kidsMode) { set(s.copy(kidsMode = it)) } }
        item { Toggle("Inhalte für Erwachsene anzeigen", s.showAdult) { set(s.copy(showAdult = it)) } }
        item { Toggle("Beim Öffnen sofort den letzten Sender zeigen", store.autoplayLast) { store.autoplayLast = it; onChanged() } }
        item {
            val label = when (s.sortMode) { "number" -> "Sendernummer"; "name" -> "Name (A–Z)"; else -> "wie beim Anbieter" }
            Toggle("Sender sortieren: $label (ändern)", true) { set(s.copy(sortMode = when (s.sortMode) { "default" -> "number"; "number" -> "name"; else -> "default" })) }
        }
        if (s.hiddenChannels.isNotEmpty()) item { Toggle("${s.hiddenChannels.size} ausgeblendete Sender – alle wieder einblenden", false) { set(s.copy(hiddenChannels = emptySet())) } }
        item {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Field(pin, { pin = it.filter(Char::isDigit).take(8) }, if (store.hasPin()) "Neue PIN (4–8 Ziffern)" else "PIN festlegen (4–8 Ziffern)", password = true, type = KeyboardType.NumberPassword, width = 260)
                Spacer(Modifier.width(8.dp))
                PillButton("PIN speichern", { if (pin.length >= 4) { store.setPin(pin); pin = "" } })
            }
        }
        item {
            Text("Gruppen & Kategorien: ausblenden oder mit PIN sperren", color = TvColors.Text, fontSize = 12.sp, fontWeight = FontWeight.SemiBold)
            Row(horizontalArrangement = Arrangement.spacedBy(6.dp), verticalAlignment = Alignment.CenterVertically, modifier = Modifier.padding(vertical = 4.dp)) {
                listOf("live" to "Live TV", "vod" to "Filme", "ser" to "Serien").forEach { (k, l) -> PillButton(if (kind == k) "● $l" else l, { kind = k }) }
                Field(groupQuery, { groupQuery = it }, "Gruppe suchen …", width = 220)
            }
        }
        items(groupList, key = { it }) { g ->
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text((if (g in s.lockedGroups) "🔒 " else "") + g, color = if (g in s.hiddenGroups) TvColors.TextSecondary.copy(alpha = 0.5f) else TvColors.Text, fontSize = 11.sp, modifier = Modifier.width(300.dp), maxLines = 1, overflow = TextOverflow.Ellipsis)
                PillButton(if (g in s.hiddenGroups) "✓ ausgeblendet · Einblenden" else "Ausblenden", { set(s.copy(hiddenGroups = if (g in s.hiddenGroups) s.hiddenGroups - g else s.hiddenGroups + g)) })
                Spacer(Modifier.width(6.dp))
                PillButton(if (g in s.lockedGroups) "🔒 gesperrt · Entsperren" else "Sperren", { set(s.copy(lockedGroups = if (g in s.lockedGroups) s.lockedGroups - g else s.lockedGroups + g)) })
            }
        }
    }
}

@Composable
private fun Toggle(label: String, on: Boolean, onChange: (Boolean) -> Unit) {
    FocusSurface({ onChange(!on) }, Modifier.width(460.dp).height(36.dp), scale = 1.02f) {
        Row(Modifier.fillMaxSize().padding(horizontal = 12.dp), verticalAlignment = Alignment.CenterVertically) {
            Text(label, color = TvColors.Text, fontSize = 12.sp, modifier = Modifier.weight(1f))
            Text(if (on) "AN" else "AUS", color = if (on) TvColors.Cyan else TvColors.TextSecondary, fontSize = 11.sp, fontWeight = FontWeight.Bold)
        }
    }
}

@Composable
fun Field(value: String, onChange: (String) -> Unit, hint: String, password: Boolean = false, type: KeyboardType = KeyboardType.Text, width: Int = 460, autofocus: Boolean = false) {
    val focus = remember { FocusRequester() }
    if (autofocus) LaunchedEffect(Unit) { runCatching { focus.requestFocus() } }
    Box(Modifier.width(width.dp).height(38.dp).background(TvColors.GlassStrong, RoundedCornerShape(50)).border(1.dp, TvColors.FocusRing, RoundedCornerShape(50)).padding(horizontal = 16.dp),
        contentAlignment = Alignment.CenterStart) {
        if (value.isEmpty()) Text(hint, color = TvColors.TextSecondary, fontSize = 11.sp, maxLines = 1)
        BasicTextField(value, onChange, singleLine = true, textStyle = TextStyle(color = TvColors.Text, fontSize = 13.sp), cursorBrush = SolidColor(TvColors.Cyan),
            visualTransformation = if (password) PasswordVisualTransformation() else VisualTransformation.None,
            keyboardOptions = KeyboardOptions(keyboardType = type), modifier = Modifier.fillMaxWidth().focusRequester(focus))
    }
}

@Suppress("unused") private val keepPlayer = PlayerActivity::class
