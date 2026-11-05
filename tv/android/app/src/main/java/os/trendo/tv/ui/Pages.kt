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
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Apps
import androidx.compose.material.icons.outlined.Eco
import androidx.compose.material.icons.outlined.Folder
import androidx.compose.material.icons.outlined.Home
import androidx.compose.material.icons.outlined.House
import androidx.compose.material.icons.outlined.LiveTv
import androidx.compose.material.icons.outlined.Movie
import androidx.compose.material.icons.outlined.MusicNote
import androidx.compose.material.icons.outlined.Psychology
import androidx.compose.material.icons.outlined.Search
import androidx.compose.material.icons.outlined.Settings
import androidx.compose.material.icons.outlined.SettingsInputHdmi
import androidx.compose.material.icons.outlined.Shield
import androidx.compose.material.icons.outlined.Speed
import androidx.compose.material.icons.outlined.SportsEsports
import androidx.compose.material.icons.outlined.Timer
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.tv.material3.Icon
import androidx.tv.material3.Text
import os.trendo.tv.apps.AppEntry
import os.trendo.tv.apps.AppFilters
import os.trendo.tv.apps.AppKind

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
    TIMER("Timer", Icons.Outlined.Timer),
    ENERGY("Energiesparen", Icons.Outlined.Eco);

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
                actions, "Live-TV läuft über deine vorhandenen Apps. Ein eigener Programmführer ist geplant.",
            )
            Section.MOVIES -> AppGrid(AppFilters.ofKind(state.apps, AppKind.VIDEO), actions, "Apps, die sich selbst als Video-App melden.")
            Section.MUSIC -> AppGrid(AppFilters.ofKind(state.apps, AppKind.AUDIO), actions, "Apps, die sich selbst als Musik-App melden.")
            Section.GAMING -> AppGrid(AppFilters.ofKind(state.apps, AppKind.GAME), actions, "Spiele auf diesem Fernseher. Controller über Bluetooth koppeln.")
            Section.MEDIATHEK -> AppGrid(
                AppFilters.ofKind(state.apps, AppKind.IMAGE) + state.apps.filter { a -> listOf("datei", "file", "media", "foto", "photo").any { a.label.lowercase().contains(it) } },
                actions, "Eigener Player für Filme, Musik und Fotos von USB ist geplant. Bis dahin: deine vorhandenen Apps.",
            )
            Section.HDMI -> HdmiPage(state, actions)
            Section.PRIVACY -> ActionList(
                "Trendo selbst zeigt keine Werbung, sammelt keine Nutzungsdaten und liest keine Daten anderer Apps.",
                listOf(
                    "App-Berechtigungen" to { actions.settings(Settings.ACTION_APPLICATION_SETTINGS) },
                    "Standort" to { actions.settings(Settings.ACTION_LOCATION_SOURCE_SETTINGS) },
                    "Datenschutz (Android)" to { actions.settings(Settings.ACTION_PRIVACY_SETTINGS) },
                    "Konten & Anmeldung" to { actions.settings(Settings.ACTION_SYNC_SETTINGS) },
                ),
            )
            Section.SETTINGS -> SettingsPage(state, actions)
            Section.PERFORMANCE -> StatsPage(state)
            Section.SEARCH -> SearchPage(state, actions)
            Section.ENERGY -> ActionList(
                "Energieoptionen stellt der Fernseher selbst bereit.",
                listOf("Energie & Ruhezustand" to { actions.settings(Settings.ACTION_SETTINGS) }, "Bildschirmschoner" to { actions.settings(Settings.ACTION_DREAM_SETTINGS) }),
            )
            Section.SMART_HOME -> Planned("Smart Home kommt mit der gesicherten Verbindung zum Trendo-PC. Geräte werden nie ohne deine Zustimmung gesteuert.")
            Section.AI -> Planned("Trendo AI kommt mit der gesicherten Verbindung zum Trendo-PC. Der Fernseher bleibt ohne PC voll bedienbar.")
            Section.TIMER -> Planned("Der Einschlaf-Timer ist geplant.")
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
private fun ActionList(hint: String, items: List<Pair<String, () -> Unit>>) {
    Hint(hint)
    val first = remember { FocusRequester() }
    LaunchedEffect(Unit) { runCatching { first.requestFocus() } }
    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        items.forEachIndexed { i, (label, onClick) ->
            FocusSurface(onClick, Modifier.width(320.dp).height(38.dp).then(if (i == 0) Modifier.focusRequester(first) else Modifier), scale = 1.03f) {
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
private fun SettingsPage(state: TvState, actions: TvActions) {
    ActionList(
        "TRENDO OS TV ${state.version} – " + (if (state.isHome) "ist die Startseite dieses Fernsehers." else "ist noch nicht die Startseite.") +
            " Die Original-Oberfläche bleibt immer erhalten.",
        listOf(
            (if (state.isHome) "Standard-Startseite ändern (zurück zu Google)" else "Trendo als Startseite festlegen") to
                (if (state.isHome) actions.homeSettings else actions.makeHome),
            "TV-Einstellungen" to { actions.settings(Settings.ACTION_SETTINGS) },
            "Bild" to { actions.settings(Settings.ACTION_DISPLAY_SETTINGS) },
            "Ton" to { actions.settings(Settings.ACTION_SOUND_SETTINGS) },
            "Netzwerk" to { actions.settings(Settings.ACTION_WIFI_SETTINGS) },
            "Original-Startseite öffnen" to actions.originalHome,
            "Alles rückgängig (Trendo entfernen)" to actions.uninstallSelf,
        ),
    )
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
    )
    Column(Modifier.width(460.dp).background(TvColors.GlassStrong, RoundedCornerShape(10.dp)).border(1.dp, TvColors.Line, RoundedCornerShape(10.dp)).padding(14.dp),
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
    LazyVerticalGrid(GridCells.Fixed(5), horizontalArrangement = Arrangement.spacedBy(12.dp), verticalArrangement = Arrangement.spacedBy(12.dp),
        contentPadding = PaddingValues(6.dp)) {
        items(hits, key = { it.packageName }) { app ->
            Column {
                AppBannerTile(app, actions, Modifier.fillMaxWidth().height(80.dp))
                Spacer(Modifier.height(3.dp))
                Text(app.label, color = TvColors.TextSecondary, fontSize = 9.sp, maxLines = 1)
            }
        }
    }
}

@Composable
private fun Planned(text: String) {
    Box(Modifier.fillMaxWidth().background(TvColors.GlassStrong, RoundedCornerShape(10.dp)).border(1.dp, TvColors.Line, RoundedCornerShape(10.dp)).padding(16.dp)) {
        Column {
            Text("Geplant", color = TvColors.Magenta, fontSize = 11.sp, fontWeight = FontWeight.Bold, letterSpacing = 1.5.sp)
            Spacer(Modifier.height(6.dp))
            Text(text, color = TvColors.Text, fontSize = 13.sp)
        }
    }
}
