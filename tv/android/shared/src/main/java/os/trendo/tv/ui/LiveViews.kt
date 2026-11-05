package os.trendo.tv.ui

import android.net.Uri
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
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
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.media3.common.MediaItem
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.ui.PlayerView
import androidx.tv.material3.Text
import kotlinx.coroutines.delay
import os.trendo.tv.livetv.Channel
import os.trendo.tv.livetv.Programme
import os.trendo.tv.livetv.TvRules
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

private fun hhmm(ms: Long) = SimpleDateFormat("HH:mm", Locale.GERMANY).format(Date(ms))

/**
 * Gemeinsamer Zugriff auf die Vorschau: Viele Anbieter erlauben nur EINE Verbindung gleichzeitig –
 * vor dem Vollbild (oder Multi-View/Aufnahme) muss die Vorschau ihre Verbindung freigeben.
 */
object PreviewHub {
    @Volatile var player: ExoPlayer? = null
    fun release() { player?.let { it.stop(); it.clearMediaItems() } }
}

/** Kleine Live-Vorschau des markierten Senders (startet erst, wenn die Markierung kurz ruht). */
@Composable
fun PreviewPlayer(url: String?, modifier: Modifier) {
    val ctx = LocalContext.current
    val player = remember { ExoPlayer.Builder(ctx).build().apply { volume = 0.6f } }
    val owner = androidx.compose.ui.platform.LocalLifecycleOwner.current
    var visible by remember { mutableStateOf(true) }
    DisposableEffect(owner) {
        PreviewHub.player = player
        // Nicht sichtbar (Vollbild, andere App) → Verbindung sofort freigeben.
        val obs = androidx.lifecycle.LifecycleEventObserver { _, e ->
            when (e) {
                androidx.lifecycle.Lifecycle.Event.ON_PAUSE -> { visible = false; PreviewHub.release() }
                androidx.lifecycle.Lifecycle.Event.ON_RESUME -> visible = true
                else -> Unit
            }
        }
        owner.lifecycle.addObserver(obs)
        onDispose { owner.lifecycle.removeObserver(obs); if (PreviewHub.player === player) PreviewHub.player = null; player.release() }
    }
    LaunchedEffect(url, visible) {
        player.stop()
        if (url == null || !visible) return@LaunchedEffect
        delay(900)
        player.setMediaItem(MediaItem.fromUri(Uri.parse(url))); player.prepare(); player.playWhenReady = true
    }
    Box(modifier.background(Color.Black, RoundedCornerShape(10.dp)).border(1.dp, TvColors.Line, RoundedCornerShape(10.dp))) {
        AndroidView({ c -> PlayerView(c).apply { useController = false; this.player = player } }, Modifier.fillMaxSize().padding(1.dp))
    }
}

/** Live TV im Drei-Spalten-Aufbau: Gruppen · Sender · Vorschau mit Sendungsinfos. */
@Composable
fun LiveChannelsView(
    channels: List<Channel>, groups: List<String>, epg: Map<String, List<Programme>>, favorites: Set<String>, locked: Set<String>,
    groupFilter: (String) -> List<Channel>, play: (Channel) -> Unit, onFav: (Channel) -> Unit, autofocus: Boolean,
) {
    var group by remember { mutableStateOf(groups.getOrElse(2) { "Alle" }) }
    var focused by remember { mutableStateOf<Channel?>(null) }
    val list = groupFilter(group)
    val now = System.currentTimeMillis()
    val first = remember { FocusRequester() }
    LaunchedEffect(autofocus, list.size) { if (autofocus && list.isNotEmpty()) runCatching { first.requestFocus() } }

    Row(Modifier.fillMaxSize()) {
        // Gruppen
        Column(Modifier.width(180.dp).fillMaxHeight().background(TvColors.GlassStrong, RoundedCornerShape(12.dp)).padding(8.dp)) {
            Text("Gruppen", color = TvColors.TextSecondary, fontSize = 10.sp, modifier = Modifier.padding(start = 6.dp, bottom = 4.dp))
            LazyColumn(verticalArrangement = Arrangement.spacedBy(2.dp)) {
                items(groups) { g ->
                    FocusSurface({ group = g }, Modifier.fillMaxWidth().height(30.dp), container = if (g == group) TvColors.Panel else Color.Transparent, scale = 1.03f, idleBorder = false) {
                        Box(Modifier.fillMaxSize().padding(horizontal = 8.dp), contentAlignment = Alignment.CenterStart) {
                            Text((if (g in locked) "🔒 " else "") + g, fontSize = 10.5.sp, color = if (g == group) TvColors.Cyan else TvColors.Text, maxLines = 1, overflow = TextOverflow.Ellipsis)
                        }
                    }
                }
            }
        }
        Spacer(Modifier.width(10.dp))
        // Sender
        Column(Modifier.weight(1f).fillMaxHeight()) {
            Text("$group · ${list.size} Sender", color = TvColors.TextSecondary, fontSize = 10.sp, modifier = Modifier.padding(start = 4.dp, bottom = 4.dp))
            if (list.isEmpty()) { Text("Keine Sender in dieser Gruppe.", color = TvColors.Text, fontSize = 12.sp); return@Column }
            LazyColumn(verticalArrangement = Arrangement.spacedBy(3.dp), contentPadding = PaddingValues(end = 4.dp)) {
                items(list, key = { it.id }) { c ->
                    val (cur, _) = TvRules.nowNext(c.epgId?.let { epg[it] }, now)
                    FocusSurface({ play(c) }, Modifier.fillMaxWidth().height(50.dp).onFocusChanged { if (it.isFocused) focused = c }
                        .then(if (c == list.first()) Modifier.focusRequester(first) else Modifier), scale = 1.02f, container = TvColors.Glass) {
                        Row(Modifier.fillMaxSize().padding(horizontal = 10.dp), verticalAlignment = Alignment.CenterVertically) {
                            Text("${c.number}", color = TvColors.Cyan, fontSize = 11.sp, modifier = Modifier.width(40.dp))
                            Box(Modifier.size(width = 58.dp, height = 34.dp).background(Color(0x33FFFFFF), RoundedCornerShape(6.dp)), contentAlignment = Alignment.Center) {
                                NetImage(c.logo, Modifier.fillMaxSize().padding(3.dp), 140, 80, crop = false)
                            }
                            Spacer(Modifier.width(10.dp))
                            Column(Modifier.weight(1f)) {
                                Text((if (c.id in favorites) "★ " else "") + (if (c.group in locked) "🔒 " else "") + c.name,
                                    color = TvColors.Text, fontSize = 12.5.sp, fontWeight = FontWeight.Medium, maxLines = 1, overflow = TextOverflow.Ellipsis)
                                Text(cur?.let { "${hhmm(it.start)}  ${it.title}" } ?: "Kein Programm", color = TvColors.TextSecondary, fontSize = 9.5.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                                cur?.let { p -> Box(Modifier.padding(top = 3.dp).fillMaxWidth(0.7f).height(2.dp).background(TvColors.Line)) { Box(Modifier.fillMaxWidth(p.progress(now)).fillMaxHeight().background(TvColors.Prism)) } }
                            }
                        }
                    }
                }
            }
        }
        Spacer(Modifier.width(12.dp))
        // Vorschau + Infos
        val f = focused ?: list.firstOrNull()
        Column(Modifier.width(360.dp).fillMaxHeight()) {
            PreviewPlayer(f?.url, Modifier.fillMaxWidth().aspectRatio(16f / 9f))
            Spacer(Modifier.height(10.dp))
            if (f != null) {
                val progs = f.epgId?.let { epg[it] }
                val (cur, next) = TvRules.nowNext(progs, now)
                Text(f.name, color = TvColors.Text, fontSize = 16.sp, fontWeight = FontWeight.Bold, maxLines = 1, overflow = TextOverflow.Ellipsis)
                Text(f.group, color = TvColors.TextSecondary, fontSize = 10.sp, maxLines = 1)
                Spacer(Modifier.height(8.dp))
                if (cur != null) {
                    Text("JETZT  ${hhmm(cur.start)}–${hhmm(cur.stop)}", color = TvColors.Cyan, fontSize = 9.sp, fontWeight = FontWeight.Bold, letterSpacing = 1.sp)
                    Text(cur.title, color = TvColors.Text, fontSize = 13.sp, fontWeight = FontWeight.SemiBold, maxLines = 2)
                    Box(Modifier.padding(vertical = 4.dp).fillMaxWidth().height(3.dp).background(TvColors.Line)) { Box(Modifier.fillMaxWidth(cur.progress(now)).fillMaxHeight().background(TvColors.Prism)) }
                    if (cur.description.isNotBlank()) Text(cur.description, color = TvColors.TextSecondary, fontSize = 10.sp, maxLines = 4, overflow = TextOverflow.Ellipsis)
                    next?.let {
                        Spacer(Modifier.height(8.dp))
                        Text("DANACH  ${hhmm(it.start)}", color = TvColors.Violet, fontSize = 9.sp, fontWeight = FontWeight.Bold, letterSpacing = 1.sp)
                        Text(it.title, color = TvColors.Text, fontSize = 11.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                    }
                } else Text("Kein Programmführer für diesen Sender.", color = TvColors.TextSecondary, fontSize = 10.sp)
                Spacer(Modifier.weight(1f))
                Row {
                    PillButton("Vollbild", { play(f) })
                    Spacer(Modifier.width(8.dp))
                    PillButton(if (f.id in favorites) "★ Favorit" else "☆ Favorit", { onFav(f) })
                }
            }
        }
    }
}

/** Ein Eintrag für die Film-/Serien-Ansicht. */
data class PosterItem(val id: String, val title: String, val category: String, val cover: String?, val year: String?, val rating: String?, val plot: String?, val open: () -> Unit)

/** Filme/Serien: großes Titelbild des markierten Titels, darunter Reihen je Kategorie. */
@Composable
fun PosterRowsView(items: List<PosterItem>, emptyText: String, autofocus: Boolean) {
    if (items.isEmpty()) {
        Box(Modifier.fillMaxWidth().background(TvColors.GlassStrong, RoundedCornerShape(10.dp)).padding(14.dp)) { Text(emptyText, color = TvColors.Text, fontSize = 12.sp) }
        return
    }
    var q by remember { mutableStateOf("") }
    // Anbieter (Netflix, Disney+, Prime …) aus den Kategorienamen des eigenen Anbieters.
    val providers = remember(items) { os.trendo.tv.livetv.Providers.present(items.map { it.category }.distinct()) }
    var provider by remember { mutableStateOf<String?>(null) }
    val shown = remember(items, q, provider) {
        items.filter { (q.isBlank() || it.title.contains(q.trim(), true)) && (provider == null || os.trendo.tv.livetv.Providers.of(it.category) == provider) }
    }
    val rows = remember(shown) {
        val order = os.trendo.tv.livetv.Providers.list.map { it.first }
        shown.groupBy { it.category }.entries
            .sortedBy { e -> os.trendo.tv.livetv.Providers.of(e.key)?.let { order.indexOf(it) } ?: order.size }
            .take(40).map { it.key to it.value.take(40) }
    }
    var focused by remember { mutableStateOf(items.first()) }
    val first = remember { FocusRequester() }
    LaunchedEffect(autofocus) { if (autofocus) runCatching { first.requestFocus() } }
    Column(Modifier.fillMaxSize()) {
        // Hero
        Box(Modifier.fillMaxWidth().height(170.dp).background(TvColors.GlassStrong, RoundedCornerShape(14.dp))) {
            NetImage(focused.cover, Modifier.fillMaxSize(), 480, 300)
            Box(Modifier.fillMaxSize().background(Brush.horizontalGradient(0f to TvColors.Ink, 0.55f to TvColors.Ink.copy(alpha = 0.75f), 1f to Color.Transparent)))
            Row(Modifier.fillMaxSize().padding(16.dp)) {
                Box(Modifier.width(100.dp).fillMaxHeight().background(TvColors.Panel, RoundedCornerShape(8.dp))) { NetImage(focused.cover, Modifier.fillMaxSize(), 200, 300) }
                Spacer(Modifier.width(16.dp))
                Column(Modifier.weight(1f)) {
                    Text(focused.title, color = TvColors.Text, fontSize = 22.sp, fontWeight = FontWeight.Bold, maxLines = 1, overflow = TextOverflow.Ellipsis)
                    Text(listOfNotNull(focused.year, focused.rating?.let { "★ $it" }, focused.category).joinToString("  ·  "), color = TvColors.Cyan, fontSize = 10.sp)
                    Spacer(Modifier.height(6.dp))
                    Text(focused.plot ?: "", color = TvColors.TextSecondary, fontSize = 10.5.sp, maxLines = 4, overflow = TextOverflow.Ellipsis)
                }
                Field(q, { q = it }, "Titel suchen …", width = 220)
            }
        }
        if (providers.isNotEmpty()) {
            Spacer(Modifier.height(6.dp))
            LazyRow(horizontalArrangement = Arrangement.spacedBy(6.dp), contentPadding = PaddingValues(2.dp)) {
                item { PillButton(if (provider == null) "● Alle Anbieter" else "Alle Anbieter", { provider = null }) }
                items(providers) { p -> PillButton(if (provider == p) "● $p" else p, { provider = if (provider == p) null else p }) }
            }
        }
        Spacer(Modifier.height(8.dp))
        LazyColumn(verticalArrangement = Arrangement.spacedBy(10.dp)) {
            items(rows, key = { it.first }) { (cat, list) ->
                Column {
                    Text("$cat", color = TvColors.Text, fontSize = 12.sp, fontWeight = FontWeight.SemiBold, maxLines = 1)
                    LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp), contentPadding = PaddingValues(vertical = 6.dp, horizontal = 2.dp)) {
                        items(list, key = { it.id }) { it2 ->
                            FocusSurface(it2.open, Modifier.width(96.dp).height(142.dp).onFocusChanged { s -> if (s.isFocused) focused = it2 }
                                .then(if (rows.first().first == cat && it2 == list.first()) Modifier.focusRequester(first) else Modifier),
                                container = TvColors.Panel, scale = 1.1f) {
                                Box(Modifier.fillMaxSize()) {
                                    NetImage(it2.cover, Modifier.fillMaxSize(), 200, 300)
                                    if (it2.cover.isNullOrBlank()) Text(it2.title, color = TvColors.Text, fontSize = 9.sp, maxLines = 4, modifier = Modifier.padding(6.dp))
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

/** Programmführer: Zeitleiste mit „Jetzt"-Linie, Senderlogos links. */
@Composable
fun GuideView(rows: List<Channel>, epg: Map<String, List<Programme>>, reminders: Set<Pair<String, Long>>, play: (Channel) -> Unit,
              catchup: (Channel, Long) -> Unit = { _, _ -> }, remind: (Channel, Programme) -> Unit) {
    val now = System.currentTimeMillis()
    // Mit Archiv (Catch-up) zeigt der Programmführer auch die letzten 2 Stunden zum Nachholen.
    val anyArchive = rows.any { it.archiveDays > 0 && it.catchup != null }
    val from = now - now % (30 * 60_000L) - if (anyArchive) 2 * 3_600_000L else 0L
    val to = now - now % (30 * 60_000L) + 3 * 3_600_000L
    val pxPerMin = 3.4f
    val first = remember { FocusRequester() }
    LaunchedEffect(Unit) { runCatching { first.requestFocus() } }
    Box(Modifier.fillMaxSize()) {
        Column {
            Row(Modifier.padding(bottom = 4.dp)) {
                Spacer(Modifier.width(150.dp))
                for (h in 0 until ((to - from) / (30 * 60_000L)).toInt()) Text(hhmm(from + h * 30 * 60_000L), color = TvColors.TextSecondary, fontSize = 9.sp, modifier = Modifier.width((30 * pxPerMin).dp))
            }
            LazyColumn(verticalArrangement = Arrangement.spacedBy(3.dp)) {
                items(rows, key = { it.id }) { c ->
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        FocusSurface({ play(c) }, Modifier.width(146.dp).height(40.dp), scale = 1.03f) {
                            Row(Modifier.fillMaxSize().padding(horizontal = 6.dp), verticalAlignment = Alignment.CenterVertically) {
                                Box(Modifier.size(width = 40.dp, height = 24.dp), contentAlignment = Alignment.Center) { NetImage(c.logo, Modifier.fillMaxSize(), 100, 60, crop = false) }
                                Spacer(Modifier.width(6.dp))
                                Text(c.name, fontSize = 9.5.sp, color = TvColors.Text, maxLines = 1, overflow = TextOverflow.Ellipsis)
                            }
                        }
                        Spacer(Modifier.width(4.dp))
                        val progs = epg[c.epgId].orEmpty().filter { it.stop > from && it.start < to }
                        LazyRow(horizontalArrangement = Arrangement.spacedBy(2.dp)) {
                            items(progs, key = { it.start }) { p ->
                                val mins = ((minOf(p.stop, to) - maxOf(p.start, from)) / 60_000L).coerceAtLeast(8)
                                val on = p.isOn(now)
                                val rem = (p.title to p.start) in reminders
                                val past = p.stop <= now
                                val canCu = past && TvRules.canCatchup(c, p.start, now)
                                FocusSurface({ if (on) play(c) else if (canCu) catchup(c, p.start) else if (!past) remind(c, p) }, Modifier.width((mins * pxPerMin).dp).height(40.dp)
                                    .then(if (c == rows.first() && p == progs.first()) Modifier.focusRequester(first) else Modifier),
                                    container = if (on) TvColors.Panel else TvColors.Glass, scale = 1.03f, shape = RoundedCornerShape(6.dp)) {
                                    Column(Modifier.fillMaxSize().padding(horizontal = 6.dp), verticalArrangement = Arrangement.Center) {
                                        Text((if (rem) "⏰ " else "") + (if (canCu) "⏮ " else "") + p.title, fontSize = 9.5.sp, color = if (on) TvColors.Cyan else if (past) TvColors.TextSecondary else TvColors.Text, maxLines = 1, overflow = TextOverflow.Ellipsis)
                                        Text("${hhmm(p.start)}–${hhmm(p.stop)}" + (if (p.category.isNotBlank()) " · ${p.category}" else ""), fontSize = 7.5.sp, color = TvColors.TextSecondary, maxLines = 1)
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        // „Jetzt"-Linie
        val x = 150f + 4f + (now - from) / 60_000f * pxPerMin
        Box(Modifier.offset(x = x.dp).width(2.dp).fillMaxHeight().background(TvColors.Magenta.copy(alpha = 0.8f)))
    }
}
