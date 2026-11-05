package os.trendo.tv.ui

import android.provider.Settings
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
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Apps
import androidx.compose.material.icons.outlined.AppSettingsAlt
import androidx.compose.material.icons.outlined.Eco
import androidx.compose.material.icons.outlined.Folder
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.House
import androidx.compose.material.icons.outlined.Image
import androidx.compose.material.icons.outlined.LiveTv
import androidx.compose.material.icons.outlined.Mic
import androidx.compose.material.icons.outlined.Movie
import androidx.compose.material.icons.outlined.MusicNote
import androidx.compose.material.icons.outlined.PlayArrow
import androidx.compose.material.icons.outlined.Psychology
import androidx.compose.material.icons.outlined.Search
import androidx.compose.material.icons.outlined.Settings
import androidx.compose.material.icons.outlined.SettingsInputHdmi
import androidx.compose.material.icons.outlined.Shield
import androidx.compose.material.icons.outlined.Speed
import androidx.compose.material.icons.outlined.SportsEsports
import androidx.compose.material.icons.outlined.Timer
import androidx.compose.material.icons.outlined.WbSunny
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.tv.material3.Icon
import androidx.tv.material3.Text
import os.trendo.tv.apps.AppEntry
import os.trendo.tv.apps.AppFilters
import os.trendo.tv.apps.AppKind
import os.trendo.tv.media.MediaFile
import os.trendo.tv.media.MediaKind
import os.trendo.tv.media.ResumeRules
import os.trendo.tv.smarthome.HomeDevice
import os.trendo.tv.weather.Place

enum class Section(val label: String, val icon: ImageVector) {
    START("Start", Icons.Outlined.Home),
    APPS("Apps", Icons.Outlined.Apps),
    LIVE_TV("Live-TV", Icons.Outlined.LiveTv),
    MOVIES("Filme", Icons.Outlined.Movie),
    MUSIC("Musik", Icons.Outlined.MusicNote),
    HDMI("HDMI", Icons.Outlined.SettingsInputHdmi),
    SMART_HOME("Smart Home", Icons.Outlined.House),
    GAMING("Gaming", Icons.Outlined.SportsEsports),
    MEDIATHEK("Mediathek", Icons.Outlined.Folder),
    PRIVACY("Datenschutz", Icons.Outlined.Shield),
    SETTINGS("Einstellungen", Icons.Outlined.Settings),
    SEARCH("Suche", Icons.Outlined.Search),
    AI("Trendo AI", Icons.Outlined.Psychology),
    PERFORMANCE("Performance", Icons.Outlined.Speed),
    TIMER("Einschlaf-Timer", Icons.Outlined.Timer),
    ENERGY("Energiesparen", Icons.Outlined.Eco),
    PHOTOS("Fotos", Icons.Outlined.Image),
    APP_MANAGE("Apps verwalten", Icons.Outlined.AppSettingsAlt),
    WEATHER("Wetter-Ort", Icons.Outlined.WbSunny);

    companion object {
        val sidebar = listOf(START, APPS, LIVE_TV, MOVIES, MUSIC, HDMI, SMART_HOME, GAMING, MEDIATHEK, PRIVACY, SETTINGS)
    }
}

@Composable
fun SectionPage(section: Section, state: TvState, actions: TvActions, go: (Section) -> Unit) {
    Column(Modifier.fillMaxSize().padding(top = 18.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Icon(section.icon, null, tint = TvColors.Cyan, modifier = Modifier.size(24.dp))
            Spacer(Modifier.width(10.dp))
            Text(section.label, color = TvColors.Text, fontSize = 24.sp, fontWeight = FontWeight.Bold)
        }
        Spacer(Modifier.height(12.dp))
        when (section) {
            Section.APPS -> AppGrid(state.apps, actions, "Alle Apps auf diesem Fernseher – unverändert, Start über die App selbst.")
            Section.LIVE_TV -> AppGrid(
                listOfNotNull(state.liveTv) + AppFilters.ofKind(state.apps, AppKind.VIDEO).filter { it.packageName != state.liveTv?.packageName },
                actions, "Live-TV läuft über deine vorhandenen Apps, z. B. TiviMate. Ein eigener Programmführer braucht eine eigene Senderliste.",
            )
            Section.MOVIES -> MediaPage(state, actions, go, listOf(MediaKind.VIDEO), AppFilters.ofKind(state.apps, AppKind.VIDEO))
            Section.MUSIC -> MediaPage(state, actions, go, listOf(MediaKind.AUDIO), AppFilters.ofKind(state.apps, AppKind.AUDIO))
            Section.PHOTOS -> MediaPage(state, actions, go, listOf(MediaKind.IMAGE), AppFilters.ofKind(state.apps, AppKind.IMAGE))
            Section.MEDIATHEK -> MediaPage(state, actions, go, listOf(MediaKind.VIDEO, MediaKind.AUDIO, MediaKind.IMAGE),
                AppFilters.ofKind(state.apps, AppKind.IMAGE) + state.apps.filter { a -> listOf("datei", "file", "media", "foto", "photo").any { a.label.lowercase().contains(it) } })
            Section.GAMING -> AppGrid(AppFilters.ofKind(state.apps, AppKind.GAME), actions, "Spiele auf diesem Fernseher. Controller über Bluetooth koppeln (Einstellungen → Fernbedienungen & Zubehör).")
            Section.HDMI -> HdmiPage(state, actions)
            Section.PRIVACY -> ActionList(
                "Trendo selbst zeigt keine Werbung, sammelt keine Nutzungsdaten und liest keine Daten anderer Apps.",
                listOf(
                    "App-Berechtigungen" to { actions.settings(Settings.ACTION_APPLICATION_SETTINGS) },
                    "Apps verwalten / entfernen" to { go(Section.APP_MANAGE) },
                    "Standort" to { actions.settings(Settings.ACTION_LOCATION_SOURCE_SETTINGS) },
                    "Datenschutz (Android)" to { actions.settings(Settings.ACTION_PRIVACY_SETTINGS) },
                    "Konten & Anmeldung" to { actions.settings(Settings.ACTION_SYNC_SETTINGS) },
                ),
            )
            Section.SETTINGS -> SettingsPage(state, actions, go)
            Section.PERFORMANCE -> StatsPage(state)
            Section.SEARCH -> SearchPage(state, actions)
            Section.ENERGY -> ActionList(
                "Energieoptionen stellt der Fernseher selbst bereit. Der Einschlaf-Timer von Trendo hält die Wiedergabe an.",
                listOf(
                    "Einschlaf-Timer" to { go(Section.TIMER) },
                    "Energie & Ruhezustand (TV)" to { actions.settings(Settings.ACTION_SETTINGS) },
                    "Bildschirmschoner" to { actions.settings(Settings.ACTION_DREAM_SETTINGS) },
                ),
            )
            Section.TIMER -> TimerPage(state, actions)
            Section.AI -> AiPage(state, actions)
            Section.SMART_HOME -> SmartHomePage(actions)
            Section.APP_MANAGE -> AppManagePage(state, actions)
            Section.WEATHER -> WeatherPage(state, actions)
            Section.START -> Unit
        }
    }
}

@Composable
private fun Hint(text: String) {
    Text(text, color = TvColors.TextSecondary, fontSize = 11.sp)
    Spacer(Modifier.height(10.dp))
}

@Composable
private fun Panel(content: @Composable () -> Unit) {
    Box(Modifier.fillMaxWidth().background(TvColors.GlassStrong, RoundedCornerShape(10.dp)).border(1.dp, TvColors.Line, RoundedCornerShape(10.dp)).padding(14.dp)) {
        content()
    }
}

@Composable
private fun AppGrid(apps: List<AppEntry>, actions: TvActions, hint: String) {
    Hint(hint)
    val list = apps.distinctBy { it.packageName }
    if (list.isEmpty()) {
        Text("Hier ist noch nichts installiert.", color = TvColors.Text, fontSize = 13.sp); return
    }
    val first = remember { FocusRequester() }
    LaunchedEffect(Unit) { runCatching { first.requestFocus() } }
    LazyVerticalGrid(GridCells.Fixed(5), horizontalArrangement = Arrangement.spacedBy(12.dp), verticalArrangement = Arrangement.spacedBy(12.dp),
        contentPadding = PaddingValues(6.dp)) {
        items(list, key = { it.packageName }) { app ->
            Column {
                AppBannerTile(app, actions, Modifier.fillMaxWidth().height(80.dp).then(if (app == list.first()) Modifier.focusRequester(first) else Modifier))
                Spacer(Modifier.height(3.dp))
                Text(app.label, color = TvColors.TextSecondary, fontSize = 9.sp, maxLines = 1)
            }
        }
    }
}

@Composable
private fun AppRow(title: String, apps: List<AppEntry>, actions: TvActions) {
    if (apps.isEmpty()) return
    Text(title, color = TvColors.Text, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
    LazyRow(horizontalArrangement = Arrangement.spacedBy(10.dp), contentPadding = PaddingValues(vertical = 6.dp, horizontal = 4.dp)) {
        items(apps.distinctBy { it.packageName }, key = { it.packageName }) { AppBannerTile(it, actions, Modifier.width(120.dp).height(68.dp)) }
    }
}

// ---------- Medien ----------

@Composable
private fun MediaPage(state: TvState, actions: TvActions, go: (Section) -> Unit, kinds: List<MediaKind>, apps: List<AppEntry>) {
    if (!state.mediaAllowed) {
        ActionList(
            "Trendo kann Filme, Musik und Fotos von USB-Sticks und vom TV-Speicher abspielen. Dafür fragt Android einmal nach deiner Erlaubnis.",
            listOf("Zugriff auf Medien erlauben" to actions.allowMedia),
        )
        Spacer(Modifier.height(14.dp)); AppRow("Medien-Apps", apps, actions); return
    }
    val first = remember { FocusRequester() }
    var focused by remember { mutableStateOf(false) }
    LazyColumn(verticalArrangement = Arrangement.spacedBy(14.dp)) {
        val resume = state.resume.filter { it.kind in kinds }
        if (resume.isNotEmpty()) item("resume") {
            MediaRow("Weitersehen", resume.map { MediaFile(0, it.uri, it.title, it.kind, it.durationMs, 0, 0, "") }, state, actions,
                firstFocus = if (!focused) first else null, progressOf = { f -> resume.firstOrNull { it.uri == f.uri }?.let { ResumeRules.progress(it.positionMs, it.durationMs) } })
            focused = true
        }
        for (k in kinds) {
            val files = when (k) { MediaKind.VIDEO -> state.videos; MediaKind.AUDIO -> state.audio; MediaKind.IMAGE -> state.images }
            if (files.isNotEmpty()) item(k.name) {
                MediaRow(when (k) { MediaKind.VIDEO -> "Filme & Videos"; MediaKind.AUDIO -> "Musik"; MediaKind.IMAGE -> "Fotos" } + " (${files.size})",
                    files, state, actions, firstFocus = if (!focused) first else null)
                focused = true
            }
        }
        val none = kinds.all { k -> when (k) { MediaKind.VIDEO -> state.videos; MediaKind.AUDIO -> state.audio; MediaKind.IMAGE -> state.images }.isEmpty() }
        if (none) item("none") {
            Panel { Text("Keine eigenen Medien gefunden. Steck einen USB-Stick mit Filmen, Musik oder Fotos ein – Trendo zeigt sie dann hier.", color = TvColors.Text, fontSize = 13.sp) }
        }
        if (apps.isNotEmpty()) item("apps") { Column { AppRow("Medien-Apps", apps, actions) } }
    }
    LaunchedEffect(state.videos.size, state.audio.size, state.images.size, state.resume.size) { runCatching { first.requestFocus() } }
}

@Composable
private fun MediaRow(title: String, files: List<MediaFile>, state: TvState, actions: TvActions, firstFocus: FocusRequester?, progressOf: (MediaFile) -> Float? = { null }) {
    Column {
        Text(title, color = TvColors.Text, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
        LazyRow(horizontalArrangement = Arrangement.spacedBy(10.dp), contentPadding = PaddingValues(vertical = 6.dp, horizontal = 4.dp)) {
            itemsIndexed(files, key = { _, f -> f.uri }) { i, f ->
                val mod = Modifier.width(150.dp).height(84.dp).then(if (i == 0 && firstFocus != null) Modifier.focusRequester(firstFocus) else Modifier)
                FocusSurface({
                    if (f.kind == MediaKind.IMAGE) actions.showPhoto(state.images.indexOfFirst { it.uri == f.uri }.coerceAtLeast(0))
                    else actions.play(f.uri, f.title, f.kind)
                }, mod, container = TvColors.Panel, scale = 1.08f) {
                    Box(Modifier.fillMaxSize()) {
                        MediaThumb(f)
                        Box(Modifier.fillMaxSize().background(androidx.compose.ui.graphics.Brush.verticalGradient(0.45f to Color.Transparent, 1f to TvColors.Ink.copy(alpha = 0.9f))))
                        Column(Modifier.fillMaxSize().padding(7.dp), verticalArrangement = Arrangement.Bottom) {
                            Text(f.title, fontSize = 9.sp, color = TvColors.Text, maxLines = 2, overflow = TextOverflow.Ellipsis)
                            val meta = listOfNotNull(f.durationMs.takeIf { it > 0 }?.let { ResumeRules.formatTime(it) }, f.sizeBytes.takeIf { it > 0 }?.let { ResumeRules.formatSize(it) }).joinToString(" · ")
                            if (meta.isNotEmpty()) Text(meta, fontSize = 7.5.sp, color = TvColors.TextSecondary)
                            progressOf(f)?.let { p ->
                                Spacer(Modifier.height(3.dp))
                                Box(Modifier.fillMaxWidth().height(3.dp).background(TvColors.Line, RoundedCornerShape(2.dp))) {
                                    Box(Modifier.fillMaxWidth(p).fillMaxHeight().background(TvColors.Prism, RoundedCornerShape(2.dp)))
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

// ---------- Timer ----------

@Composable
private fun TimerPage(state: TvState, actions: TvActions) {
    val left = state.timerMinutes
    Hint(if (left != null) "Timer läuft – noch etwa $left Minuten. Danach hält Trendo die Wiedergabe an (auch in TiviMate) und dunkelt den Bildschirm ab."
         else "Nach Ablauf hält Trendo die Wiedergabe an – auch in TiviMate und anderen Apps – und dunkelt den Bildschirm ab. Ganz ausschalten kann nur der Fernseher selbst.")
    val first = remember { FocusRequester() }
    LaunchedEffect(Unit) { runCatching { first.requestFocus() } }
    Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
        listOf(15, 30, 45, 60, 90, 120).forEachIndexed { i, m ->
            FocusSurface({ actions.setTimer(m) }, Modifier.width(90.dp).height(60.dp).then(if (i == 0) Modifier.focusRequester(first) else Modifier)) {
                Column(Modifier.fillMaxSize(), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center) {
                    Text(if (m < 60) "$m" else "${m / 60}${if (m % 60 != 0) ",5" else ""}", fontSize = 20.sp, fontWeight = FontWeight.Bold, color = TvColors.Text)
                    Text(if (m < 60) "Minuten" else if (m == 60) "Stunde" else "Stunden", fontSize = 9.sp, color = TvColors.TextSecondary)
                }
            }
        }
        if (left != null) FocusSurface(actions.cancelTimer, Modifier.width(110.dp).height(60.dp)) {
            Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) { Text("Timer aus", fontSize = 13.sp, color = TvColors.Magenta) }
        }
    }
    Spacer(Modifier.height(14.dp))
    ActionList("", listOf("Ausschalt-Timer des Fernsehers (Einstellungen)" to { actions.settings(Settings.ACTION_SETTINGS) }), autoFocus = false)
}

// ---------- Trendo AI ----------

@Composable
private fun AiPage(state: TvState, actions: TvActions) {
    var text by remember { mutableStateOf("") }
    var reply by remember { mutableStateOf("Sag oder schreib, was du möchtest.") }
    Hint("Trendo AI versteht feste Befehle direkt am Fernseher – ohne Cloud, ohne PC. Sprache nutzt die Spracherkennung des Fernsehers.")
    val mic = remember { FocusRequester() }
    LaunchedEffect(Unit) { runCatching { mic.requestFocus() } }
    Row(verticalAlignment = Alignment.CenterVertically) {
        if (state.voiceAvailable) {
            FocusSurface({ actions.voice { said -> text = said; actions.command(said) { reply = it } } }, Modifier.size(56.dp).focusRequester(mic),
                shape = RoundedCornerShape(50), container = TvColors.Panel, scale = 1.1f) {
                Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) { Icon(Icons.Outlined.Mic, "Sprechen", tint = TvColors.Cyan, modifier = Modifier.size(26.dp)) }
            }
            Spacer(Modifier.width(14.dp))
        }
        BasicTextField(
            value = text, onValueChange = { text = it }, singleLine = true,
            textStyle = TextStyle(color = TvColors.Text, fontSize = 15.sp), cursorBrush = SolidColor(TvColors.Cyan),
            keyboardOptions = KeyboardOptions(imeAction = ImeAction.Go),
            keyboardActions = KeyboardActions(onGo = { actions.command(text) { reply = it } }),
            modifier = Modifier.width(420.dp).height(40.dp).then(if (!state.voiceAvailable) Modifier.focusRequester(mic) else Modifier)
                .background(TvColors.GlassStrong, RoundedCornerShape(50)).border(1.5.dp, TvColors.FocusRing, RoundedCornerShape(50))
                .padding(horizontal = 16.dp, vertical = 10.dp),
        )
        Spacer(Modifier.width(10.dp))
        PillButton("Ausführen", { actions.command(text) { reply = it } })
    }
    Spacer(Modifier.height(12.dp))
    Panel { Text(reply, color = TvColors.Text, fontSize = 14.sp) }
    Spacer(Modifier.height(12.dp))
    Text("Beispiele", color = TvColors.TextSecondary, fontSize = 11.sp, fontWeight = FontWeight.SemiBold)
    Spacer(Modifier.height(6.dp))
    val examples = listOf("Öffne TiviMate", "HDMI 1", "Timer 30 Minuten", "Lauter", "Pause", "Suche Kodi", "Zeige Musik", "Wie ist das Wetter?")
    LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp), contentPadding = PaddingValues(4.dp)) {
        items(examples) { ex -> PillButton(ex, { text = ex; actions.command(ex) { reply = it } }) }
    }
}

// ---------- Smart Home ----------

@Composable
private fun SmartHomePage(actions: TvActions) {
    var devices by remember { mutableStateOf(emptyList<HomeDevice>()) }
    DisposableEffect(Unit) {
        actions.discovery(true) { devices = it }
        onDispose { actions.discovery(false) {} }
    }
    Hint("Trendo sucht nur, solange diese Seite offen ist, nach Geräten in deinem Heimnetz. Gesteuert wird nichts ohne deine Freigabe.")
    if (devices.isEmpty()) {
        Panel { Text("Suche läuft … Gefundene Geräte (z. B. Home Assistant, Hue, Chromecast, Lautsprecher, NAS, Drucker) erscheinen hier.", color = TvColors.Text, fontSize = 13.sp) }
        return
    }
    LazyVerticalGrid(GridCells.Fixed(3), horizontalArrangement = Arrangement.spacedBy(10.dp), verticalArrangement = Arrangement.spacedBy(10.dp), contentPadding = PaddingValues(4.dp)) {
        items(devices, key = { it.type + it.name }) { d ->
            FocusSurface({}, Modifier.fillMaxWidth().height(64.dp)) {
                Column(Modifier.fillMaxSize().padding(10.dp), verticalArrangement = Arrangement.Center) {
                    Text(d.name, color = TvColors.Text, fontSize = 12.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                    Text(d.kind, color = TvColors.Cyan, fontSize = 9.sp)
                }
            }
        }
    }
}

// ---------- Apps verwalten ----------

@Composable
private fun AppManagePage(state: TvState, actions: TvActions) {
    Hint("App wählen → Android öffnet die Infoseite der App. Dort kannst du sie selbst deinstallieren oder deaktivieren. Trendo selbst entfernt keine Apps.")
    val list = state.apps.filter { it.packageName != "com.android.vending" }
    val first = remember { FocusRequester() }
    LaunchedEffect(Unit) { runCatching { first.requestFocus() } }
    LazyVerticalGrid(GridCells.Fixed(4), horizontalArrangement = Arrangement.spacedBy(10.dp), verticalArrangement = Arrangement.spacedBy(10.dp), contentPadding = PaddingValues(4.dp)) {
        items(list, key = { it.packageName }) { app ->
            val protected = os.trendo.tv.apps.ProtectedApps.isProtected(app)
            FocusSurface({ actions.appInfo(app.packageName) }, Modifier.fillMaxWidth().height(52.dp).then(if (app == list.firstOrNull()) Modifier.focusRequester(first) else Modifier)) {
                Column(Modifier.fillMaxSize().padding(horizontal = 10.dp), verticalArrangement = Arrangement.Center) {
                    Text(app.label, color = TvColors.Text, fontSize = 12.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                    Text(when { protected -> "wichtig – besser behalten"; app.isSystem -> "System-App · nur deaktivierbar"; else -> "Infoseite · deinstallierbar" },
                        color = if (protected) TvColors.Magenta else TvColors.TextSecondary, fontSize = 8.5.sp)
                }
            }
        }
    }
}

// ---------- Wetter-Ort ----------

@Composable
private fun WeatherPage(state: TvState, actions: TvActions) {
    var query by remember { mutableStateOf(state.place?.name ?: "") }
    var results by remember { mutableStateOf(emptyList<Place>()) }
    var info by remember { mutableStateOf(state.place?.let { "Aktuell: ${it.name}${if (it.region.isNotBlank()) ", " + it.region else ""}" } ?: "Noch kein Ort gewählt.") }
    Hint("Wetter von Open-Meteo (frei, ohne Konto). Trendo ortet dich nicht – du gibst den Ort selbst ein.")
    val focus = remember { FocusRequester() }
    LaunchedEffect(Unit) { runCatching { focus.requestFocus() } }
    Row(verticalAlignment = Alignment.CenterVertically) {
        BasicTextField(
            value = query, onValueChange = { query = it }, singleLine = true,
            textStyle = TextStyle(color = TvColors.Text, fontSize = 15.sp), cursorBrush = SolidColor(TvColors.Cyan),
            keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
            keyboardActions = KeyboardActions(onSearch = { info = "Suche …"; actions.searchPlace(query) { results = it; info = if (it.isEmpty()) "Nichts gefunden (oder offline)." else "Ort auswählen:" } }),
            modifier = Modifier.width(360.dp).height(40.dp).focusRequester(focus)
                .background(TvColors.GlassStrong, RoundedCornerShape(50)).border(1.5.dp, TvColors.FocusRing, RoundedCornerShape(50))
                .padding(horizontal = 16.dp, vertical = 10.dp),
        )
        Spacer(Modifier.width(10.dp))
        PillButton("Suchen", { info = "Suche …"; actions.searchPlace(query) { results = it; info = if (it.isEmpty()) "Nichts gefunden (oder offline)." else "Ort auswählen:" } })
        if (state.place != null) { Spacer(Modifier.width(8.dp)); PillButton("Wetter aus", { actions.setPlace(null); info = "Wetter ausgeschaltet." }) }
    }
    Spacer(Modifier.height(10.dp))
    Text(info, color = TvColors.TextSecondary, fontSize = 11.sp)
    Spacer(Modifier.height(8.dp))
    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        results.forEach { p ->
            FocusSurface({ actions.setPlace(p); info = "Gespeichert: ${p.name}" }, Modifier.width(420.dp).height(38.dp), scale = 1.03f) {
                Box(Modifier.fillMaxSize().padding(horizontal = 14.dp), contentAlignment = Alignment.CenterStart) {
                    Text("${p.name} · ${p.region}", fontSize = 12.sp, color = TvColors.Text, maxLines = 1)
                }
            }
        }
    }
}

// ---------- Listen / Einstellungen / Werte / Suche ----------

@Composable
private fun ActionList(hint: String, items: List<Pair<String, () -> Unit>>, autoFocus: Boolean = true) {
    if (hint.isNotEmpty()) Hint(hint)
    val first = remember { FocusRequester() }
    if (autoFocus) LaunchedEffect(Unit) { runCatching { first.requestFocus() } }
    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        items.forEachIndexed { i, (label, onClick) ->
            FocusSurface(onClick, Modifier.width(360.dp).height(36.dp).then(if (i == 0) Modifier.focusRequester(first) else Modifier), scale = 1.03f) {
                Box(Modifier.fillMaxSize().padding(horizontal = 14.dp), contentAlignment = Alignment.CenterStart) {
                    Text(label, fontSize = 13.sp, color = TvColors.Text)
                }
            }
        }
    }
}

@Composable
private fun HdmiPage(state: TvState, actions: TvActions) {
    if (state.inputs.isEmpty()) {
        ActionList("Der Fernseher meldet seine Eingänge nicht an Apps. Umschalten geht über die TV-Einstellungen oder die Eingangstaste der Fernbedienung.",
            listOf("TV-Einstellungen öffnen" to { actions.settings(Settings.ACTION_SETTINGS) }))
    } else {
        ActionList("Eingang wählen – der Fernseher schaltet selbst um.", state.inputs.map { it.label to { actions.openInput(it) } })
    }
}

@Composable
private fun SettingsPage(state: TvState, actions: TvActions, go: (Section) -> Unit) {
    LazyColumn {
        item {
            ActionList(
                "TRENDO OS TV ${state.version} – " + (if (state.isHome) "ist die Startseite dieses Fernsehers." else "ist noch nicht die Startseite.") +
                    " Die Original-Oberfläche bleibt immer erhalten.",
                listOf(
                    (if (state.isHome) "Startseiten-Wächter ausschalten (zurück zu Google)" else "Trendo als Startseite festlegen") to
                        (if (state.isHome) actions.homeSettings else actions.makeHome),
                    "Bildschirmschoner: TRENDO OS wählen" to { actions.settings(Settings.ACTION_DREAM_SETTINGS) },
                    "Wetter-Ort festlegen" to { go(Section.WEATHER) },
                    "Apps verwalten / entfernen" to { go(Section.APP_MANAGE) },
                    "Einschlaf-Timer" to { go(Section.TIMER) },
                    "TV-Einstellungen" to { actions.settings(Settings.ACTION_SETTINGS) },
                    "Bild" to { actions.settings(Settings.ACTION_DISPLAY_SETTINGS) },
                    "Ton" to { actions.settings(Settings.ACTION_SOUND_SETTINGS) },
                    "Netzwerk" to { actions.settings(Settings.ACTION_WIFI_SETTINGS) },
                    "Original-Startseite öffnen" to actions.originalHome,
                    "Alles rückgängig (Trendo entfernen)" to actions.uninstallSelf,
                ),
            )
        }
    }
}

@Composable
private fun StatsPage(state: TvState) {
    Hint("Echte Werte dieses Fernsehers. Was das Gerät nicht meldet, steht als „nicht verfügbar“.")
    val s = state.stats
    fun mb(v: Long?) = if (v == null) "nicht verfügbar" else if (v >= 1024) String.format(java.util.Locale.GERMANY, "%.1f GB", v / 1024.0) else "$v MB"
    val rows = listOf(
        "Arbeitsspeicher gesamt" to mb(s.ramTotalMb),
        "Arbeitsspeicher frei" to mb(s.ramFreeMb),
        "Speicher knapp" to (s.lowMemory?.let { if (it) "ja" else "nein" } ?: "nicht verfügbar"),
        "App-Speicher frei" to mb(s.storageFreeMb),
        "App-Speicher gesamt" to mb(s.storageTotalMb),
        "Internet" to if (state.online) "verbunden" else "offline",
        "Installierte Apps mit Startsymbol" to state.apps.size.toString(),
        "Eigene Medien (Video/Musik/Foto)" to if (state.mediaAllowed) "${state.videos.size} / ${state.audio.size} / ${state.images.size}" else "kein Zugriff erlaubt",
    )
    Column(Modifier.width(480.dp).background(TvColors.GlassStrong, RoundedCornerShape(10.dp)).border(1.dp, TvColors.Line, RoundedCornerShape(10.dp)).padding(14.dp),
        verticalArrangement = Arrangement.spacedBy(6.dp)) {
        rows.forEach { (k, v) ->
            Row { Text(k, color = TvColors.TextSecondary, fontSize = 12.sp, modifier = Modifier.weight(1f)); Text(v, color = TvColors.Text, fontSize = 12.sp) }
        }
    }
}

@Composable
private fun SearchPage(state: TvState, actions: TvActions) {
    var query by remember { mutableStateOf("") }
    val focus = remember { FocusRequester() }
    LaunchedEffect(Unit) { runCatching { focus.requestFocus() } }
    BasicTextField(
        value = query, onValueChange = { query = it }, singleLine = true,
        textStyle = TextStyle(color = TvColors.Text, fontSize = 15.sp), cursorBrush = SolidColor(TvColors.Cyan),
        modifier = Modifier.width(420.dp).height(38.dp).focusRequester(focus)
            .background(TvColors.GlassStrong, RoundedCornerShape(50)).border(1.5.dp, TvColors.FocusRing, RoundedCornerShape(50))
            .padding(horizontal = 16.dp, vertical = 9.dp),
    )
    Spacer(Modifier.height(12.dp))
    val hits = AppFilters.search(state.apps, query)
    val media = if (query.isBlank()) emptyList() else (state.videos + state.audio).filter { it.title.lowercase().contains(query.trim().lowercase()) }.take(20)
    LazyColumn(verticalArrangement = Arrangement.spacedBy(10.dp)) {
        if (media.isNotEmpty()) item { MediaRow("Eigene Medien", media, state, actions, null) }
        item {
            Column {
                Text("Apps", color = TvColors.Text, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
                LazyRow(horizontalArrangement = Arrangement.spacedBy(10.dp), contentPadding = PaddingValues(6.dp)) {
                    items(hits, key = { it.packageName }) { app -> AppBannerTile(app, actions, Modifier.width(120.dp).height(68.dp)) }
                }
            }
        }
    }
}

@Suppress("unused")
private val playIcon = Icons.Outlined.PlayArrow
