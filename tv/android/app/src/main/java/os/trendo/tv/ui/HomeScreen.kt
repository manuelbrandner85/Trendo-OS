package os.trendo.tv.ui

import android.graphics.drawable.Drawable
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.RowScope
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.Apps
import androidx.compose.material.icons.outlined.ChevronRight
import androidx.compose.material.icons.outlined.Eco
import androidx.compose.material.icons.outlined.Folder
import androidx.compose.material.icons.outlined.House
import androidx.compose.material.icons.outlined.Image
import androidx.compose.material.icons.outlined.Mic
import androidx.compose.material.icons.outlined.Psychology
import androidx.compose.material.icons.outlined.Search
import androidx.compose.material.icons.outlined.Settings
import androidx.compose.material.icons.outlined.Speed
import androidx.compose.material.icons.outlined.Timer
import androidx.compose.material.icons.outlined.Tv
import androidx.compose.material.icons.outlined.Wifi
import androidx.compose.material.icons.outlined.WifiOff
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.graphics.drawable.toBitmap
import androidx.tv.material3.Icon
import androidx.tv.material3.Text
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.withContext
import os.trendo.tv.R
import os.trendo.tv.apps.AppEntry
import os.trendo.tv.apps.DeviceStats
import os.trendo.tv.apps.TvInput
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/** Alles, was die Oberfläche anzeigt – nur echte Daten vom Gerät. */
data class TvState(
    val apps: List<AppEntry>,
    val favorites: List<AppEntry>,
    val myApps: List<AppEntry>,
    val lastLaunched: AppEntry?,
    val liveTv: AppEntry?,
    val inputs: List<TvInput>,
    val stats: DeviceStats,
    val online: Boolean,
    val version: String,
    /** true = Trendo ist die Standard-Startseite des Fernsehers. */
    val isHome: Boolean,
)

/** Alle Aktionen; Fremd-Apps werden ausschließlich gestartet oder deren Infoseite geöffnet. */
class TvActions(
    val launch: (String) -> Unit,
    val appInfo: (String) -> Unit,
    val settings: (String) -> Unit,
    val originalHome: () -> Unit,
    val uninstallSelf: () -> Unit,
    val openInput: (TvInput) -> Unit,
    val banner: (String) -> Drawable?,
    val makeHome: () -> Unit,
    val homeSettings: () -> Unit,
)

@Composable
fun TrendoTvApp(state: TvState, actions: TvActions, homeTick: Int = 0) {
    var section by rememberSaveable { mutableStateOf(Section.START) }
    // Home-Taste: immer zurück auf „Start".
    LaunchedEffect(homeTick) { section = Section.START }
    // Zurück-Taste: Unterseite → Start. Als Startseite schließt sich Trendo nicht selbst.
    BackHandler(enabled = section != Section.START || state.isHome) { section = Section.START }

    Box(Modifier.fillMaxSize().background(TvColors.Ink)) {
        Image(painterResource(R.drawable.tv_hero), contentDescription = null, contentScale = ContentScale.Crop, modifier = Modifier.fillMaxSize())
        // Lesbarkeit: links und unten abdunkeln; auf Unterseiten stärker.
        val dim = if (section == Section.START) 0f else 0.55f
        Box(Modifier.fillMaxSize().background(TvColors.Ink.copy(alpha = dim)))
        Box(Modifier.fillMaxSize().background(Brush.horizontalGradient(0f to TvColors.Ink.copy(alpha = 0.85f), 0.45f to Color.Transparent)))
        Box(Modifier.fillMaxSize().background(Brush.verticalGradient(0.5f to Color.Transparent, 1f to TvColors.Ink.copy(alpha = 0.9f))))
        // Farbiger Randschein der Oberfläche (statisch, keine echte TV-Hintergrundbeleuchtung).
        Box(Modifier.fillMaxSize().border(2.dp, Brush.sweepGradient(listOf(TvColors.Cyan, TvColors.ElectricBlue, TvColors.Violet, TvColors.Magenta, TvColors.Cyan)).let { it }, RoundedCornerShape(0.dp)))

        Column(Modifier.fillMaxSize()) {
            TopBar(state, onSearch = { section = Section.SEARCH }, onSettings = { section = Section.SETTINGS })
            Row(Modifier.fillMaxSize().padding(start = 14.dp, end = 20.dp, bottom = 14.dp)) {
                Sidebar(section) { section = it }
                Spacer(Modifier.width(14.dp))
                Box(Modifier.weight(1f).fillMaxHeight()) {
                    if (section == Section.START) StartPage(state, actions) { section = it }
                    else SectionPage(section, state, actions) { section = it }
                }
            }
        }
    }
}

@Composable
private fun TopBar(state: TvState, onSearch: () -> Unit, onSettings: () -> Unit) {
    Row(
        Modifier.fillMaxWidth().height(64.dp).padding(start = 18.dp, end = 20.dp, top = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Image(painterResource(R.drawable.ic_trendo_mark), contentDescription = null, modifier = Modifier.size(50.dp))
        Spacer(Modifier.width(8.dp))
        Column {
            Text(stringResource(R.string.app_name), color = TvColors.Text, fontSize = 21.sp, fontWeight = FontWeight.Bold, letterSpacing = 1.5.sp)
            Text(stringResource(R.string.edition), color = TvColors.Cyan, fontSize = 7.5.sp, letterSpacing = 2.5.sp)
        }
        Spacer(Modifier.width(10.dp))
        Text(stringResource(R.string.credit), color = TvColors.Text, fontSize = 22.sp, fontFamily = ScriptFont)
        Spacer(Modifier.weight(1f))
        FocusSurface(onSearch, Modifier.width(290.dp).height(30.dp), shape = RoundedCornerShape(50), container = Color(0x66050A14), scale = 1.03f) {
            Row(Modifier.fillMaxSize().padding(horizontal = 12.dp), verticalAlignment = Alignment.CenterVertically) {
                Icon(Icons.Outlined.Search, null, tint = TvColors.Text, modifier = Modifier.size(15.dp))
                Spacer(Modifier.width(8.dp))
                Text("Suche nach Apps …", color = TvColors.TextSecondary, fontSize = 10.sp, modifier = Modifier.weight(1f))
                Icon(Icons.Outlined.Mic, null, tint = TvColors.TextSecondary, modifier = Modifier.size(14.dp))
            }
        }
        Spacer(Modifier.weight(1f))
        Clock()
        Spacer(Modifier.width(14.dp))
        Icon(if (state.online) Icons.Outlined.Wifi else Icons.Outlined.WifiOff, contentDescription = if (state.online) "Online" else "Offline",
            tint = TvColors.Text, modifier = Modifier.size(17.dp))
        Spacer(Modifier.width(8.dp))
        FocusSurface(onSettings, Modifier.size(30.dp), shape = RoundedCornerShape(50), container = Color.Transparent) {
            Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Icon(Icons.Outlined.Settings, "Einstellungen", tint = TvColors.Text, modifier = Modifier.size(17.dp))
            }
        }
    }
}

@Composable
private fun Clock() {
    var now by remember { mutableStateOf(Date()) }
    LaunchedEffect(Unit) {
        // Einmal pro Minute aktualisieren, kein Dauer-Polling.
        while (true) {
            now = Date()
            delay(60_000L - System.currentTimeMillis() % 60_000L)
        }
    }
    Column(horizontalAlignment = Alignment.End) {
        Text(SimpleDateFormat("HH:mm", Locale.GERMANY).format(now), color = TvColors.Text, fontSize = 24.sp, fontWeight = FontWeight.Medium)
        Text(SimpleDateFormat("EE d. MMMM yyyy", Locale.GERMANY).format(now), color = TvColors.TextSecondary, fontSize = 8.5.sp)
    }
}

@Composable
private fun Sidebar(current: Section, onSelect: (Section) -> Unit) {
    Column(
        Modifier.width(126.dp).fillMaxHeight()
            .background(TvColors.GlassStrong, RoundedCornerShape(12.dp))
            .border(1.dp, TvColors.Line, RoundedCornerShape(12.dp))
            .padding(vertical = 8.dp, horizontal = 6.dp),
        verticalArrangement = Arrangement.spacedBy(2.dp),
    ) {
        for (s in Section.sidebar) {
            val selected = s == current
            FocusSurface(
                onClick = { onSelect(s) },
                modifier = Modifier.fillMaxWidth().height(31.dp)
                    .then(if (selected) Modifier.border(1.5.dp, TvColors.FocusRing, RoundedCornerShape(8.dp)) else Modifier),
                shape = RoundedCornerShape(8.dp),
                container = if (selected) TvColors.Panel else Color.Transparent,
                scale = 1.03f,
                idleBorder = false,
            ) {
                Row(Modifier.fillMaxSize().padding(horizontal = 10.dp), verticalAlignment = Alignment.CenterVertically) {
                    IconLabel(s.icon, s.label, iconSize = 15.dp, fontSize = 11)
                }
            }
        }
    }
}

@Composable
private fun StartPage(state: TvState, actions: TvActions, go: (Section) -> Unit) {
    val first = remember { FocusRequester() }
    LaunchedEffect(Unit) { runCatching { first.requestFocus() } }

    Column(Modifier.fillMaxSize()) {
        Spacer(Modifier.height(26.dp))
        Text("Deine\nUnterhaltung.", color = TvColors.Text, fontSize = 34.sp, lineHeight = 36.sp, fontWeight = FontWeight.Bold)
        Spacer(Modifier.height(4.dp))
        Text("Filme · Serien · Musik · Spiele · Mehr", color = TvColors.Text, fontSize = 11.sp, letterSpacing = 0.5.sp)
        Spacer(Modifier.height(12.dp))
        PillButton("Jetzt entdecken", { go(Section.APPS) }, icon = Icons.Outlined.ChevronRight)
        Spacer(Modifier.weight(1f))

        Text("Meine Apps", color = TvColors.Text, fontSize = 13.sp, fontWeight = FontWeight.SemiBold)
        // Feste Reihe ohne Scrollen: so bleibt TiviMate immer ganz links sichtbar.
        Row(Modifier.fillMaxWidth().padding(vertical = 6.dp, horizontal = 2.dp), horizontalArrangement = Arrangement.spacedBy(7.dp)) {
            for (app in state.myApps.take(9)) {
                AppBannerTile(app, actions, Modifier.weight(1f).height(43.dp))
            }
            run {
                FocusSurface({ go(Section.APPS) }, Modifier.weight(1f).height(43.dp)) {
                    Column(Modifier.fillMaxSize(), horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.Center) {
                        Icon(Icons.Outlined.Add, null, tint = TvColors.Cyan, modifier = Modifier.size(14.dp))
                        Text("Apps", fontSize = 9.sp, color = TvColors.Text)
                    }
                }
            }
        }
        Spacer(Modifier.height(4.dp))

        Row(Modifier.fillMaxWidth().height(112.dp).padding(horizontal = 2.dp), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            val live = state.liveTv
            FeatureCard("Live-TV", if (live != null) "${live.label} · Sender" else "Sender · TV-Guide", "Jetzt schauen", R.drawable.tv_card_livetv,
                Modifier.focusRequester(first)) { if (live != null) actions.launch(live.packageName) else go(Section.LIVE_TV) }
            val last = state.lastLaunched
            FeatureCard("Weitersehen", if (last != null) "Zuletzt: ${last.label}" else "Noch nichts geöffnet", "Fortsetzen", R.drawable.tv_card_weiter) {
                if (last != null) actions.launch(last.packageName) else go(Section.APPS)
            }
            FeatureCard("Mediathek", "Filme · Serien · Musik · Fotos", "Öffnen", R.drawable.tv_card_mediathek) { go(Section.MEDIATHEK) }
            FeatureCard(
                "HDMI",
                if (state.inputs.isNotEmpty()) state.inputs.joinToString(" · ") { it.label } else "Eingänge & Quellen",
                "Auswählen", R.drawable.tv_card_hdmi,
            ) { go(Section.HDMI) }
            FeatureCard("Gaming", "Spiele · Controller", "Starten", R.drawable.tv_card_gaming) { go(Section.GAMING) }
            FeatureCard("Privacy Center", "Berechtigungen · Datenschutz", "Einstellungen", R.drawable.tv_card_privacy) { go(Section.PRIVACY) }
        }
        Spacer(Modifier.height(10.dp))

        Row(Modifier.fillMaxWidth().height(48.dp).padding(horizontal = 2.dp), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            ToolTile(Icons.Outlined.Apps, "App Center", TvColors.ElectricBlue) { go(Section.APPS) }
            ToolTile(Icons.Outlined.Folder, "Dateien", TvColors.Cyan) { go(Section.MEDIATHEK) }
            ToolTile(Icons.Outlined.House, "Smart Home", Color(0xFF3DDC84)) { go(Section.SMART_HOME) }
            ToolTile(Icons.Outlined.Psychology, "Trendo AI", TvColors.Violet) { go(Section.AI) }
            ToolTile(Icons.Outlined.Speed, "Performance", TvColors.Cyan) { go(Section.PERFORMANCE) }
            ToolTile(Icons.Outlined.Tv, "Bild & Ton", TvColors.ElectricBlue) { actions.settings(android.provider.Settings.ACTION_DISPLAY_SETTINGS) }
            ToolTile(Icons.Outlined.Wifi, "Netzwerk", TvColors.Cyan) { actions.settings(android.provider.Settings.ACTION_WIFI_SETTINGS) }
            ToolTile(Icons.Outlined.Timer, "Timer", TvColors.Text) { go(Section.TIMER) }
            ToolTile(Icons.Outlined.Image, "Screensaver", TvColors.Text) { actions.settings(android.provider.Settings.ACTION_DREAM_SETTINGS) }
            ToolTile(Icons.Outlined.Eco, "Energiesparen", Color(0xFF3DDC84)) { go(Section.ENERGY) }
            ToolTile(Icons.Outlined.Settings, "System", TvColors.Text) { go(Section.SETTINGS) }
        }
    }
}

@Composable
private fun RowScope.FeatureCard(title: String, subtitle: String, button: String, image: Int, modifier: Modifier = Modifier, onClick: () -> Unit) {
    val shape = RoundedCornerShape(10.dp)
    FocusSurface(onClick, modifier.weight(1f).fillMaxHeight(), shape = shape, scale = 1.05f, ring = 2.5.dp) {
        Box(Modifier.fillMaxSize()) {
            Image(painterResource(image), null, contentScale = ContentScale.Crop, modifier = Modifier.fillMaxSize())
            Box(Modifier.fillMaxSize().background(Brush.verticalGradient(0f to TvColors.Ink.copy(alpha = 0.8f), 0.45f to Color.Transparent, 1f to TvColors.Ink.copy(alpha = 0.55f))))
            Column(Modifier.fillMaxSize().padding(8.dp)) {
                Text(title, fontSize = 12.sp, fontWeight = FontWeight.Bold, color = TvColors.Text, maxLines = 1, overflow = TextOverflow.Ellipsis)
                Text(subtitle, fontSize = 7.5.sp, color = TvColors.Text.copy(alpha = 0.85f), maxLines = 1, overflow = TextOverflow.Ellipsis)
                Spacer(Modifier.weight(1f))
                Row(
                    Modifier.background(Color(0x99050A14), RoundedCornerShape(50)).border(1.dp, TvColors.Text.copy(alpha = 0.6f), RoundedCornerShape(50))
                        .padding(horizontal = 9.dp, vertical = 3.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Text(button, fontSize = 8.sp, color = TvColors.Text)
                    Icon(Icons.Outlined.ChevronRight, null, tint = TvColors.Text, modifier = Modifier.size(10.dp))
                }
            }
        }
    }
}

@Composable
private fun RowScope.ToolTile(icon: ImageVector, label: String, tint: Color, onClick: () -> Unit) {
    FocusSurface(onClick, Modifier.weight(1f).fillMaxHeight(), shape = RoundedCornerShape(8.dp), scale = 1.08f) {
        Column(Modifier.fillMaxSize().padding(top = 6.dp), horizontalAlignment = Alignment.CenterHorizontally) {
            Icon(icon, null, tint = tint, modifier = Modifier.size(17.dp))
            Spacer(Modifier.height(4.dp))
            Text(label, fontSize = 7.5.sp, color = TvColors.Text, maxLines = 1, overflow = TextOverflow.Clip, softWrap = false)
        }
    }
}

/** App-Kachel mit dem Original-Banner der App. Lange drücken öffnet die Android-Infoseite. */
@Composable
fun AppBannerTile(app: AppEntry, actions: TvActions, modifier: Modifier) {
    val banner by produceState<ImageBitmap?>(null, app.packageName) {
        // Bildgröße begrenzt (Ressourcenbudget), Laden außerhalb des UI-Threads.
        value = withContext(Dispatchers.IO) { runCatching { actions.banner(app.packageName)?.toBitmap(240, 135)?.asImageBitmap() }.getOrNull() }
    }
    FocusSurface({ actions.launch(app.packageName) }, modifier, shape = RoundedCornerShape(7.dp), container = TvColors.Panel, scale = 1.1f) {
        Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
            val img = banner
            if (img != null) Image(img, app.label, contentScale = ContentScale.Crop, modifier = Modifier.fillMaxSize())
            else Text(app.label, fontSize = 9.sp, color = TvColors.Text, maxLines = 2, overflow = TextOverflow.Ellipsis, modifier = Modifier.padding(4.dp))
        }
    }
}
