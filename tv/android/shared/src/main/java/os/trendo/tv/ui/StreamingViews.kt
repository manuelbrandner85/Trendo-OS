package os.trendo.tv.ui

import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
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
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.graphics.drawable.toBitmap
import androidx.tv.material3.Text
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import os.trendo.tv.livetv.Broadcaster
import os.trendo.tv.livetv.Mediathek
import os.trendo.tv.livetv.MtItem
import os.trendo.tv.livetv.StreamService
import os.trendo.tv.livetv.StreamServices
import os.trendo.tv.livetv.Tip
import os.trendo.tv.livetv.Tips
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

private fun day(ms: Long) = SimpleDateFormat("d.M.yyyy", Locale.GERMANY).format(Date(ms))

/** Installierte Apps mit Startsymbol (Paket, Name) – nur zum Erkennen der offiziellen Apps. */
@Composable
private fun installedApps(): List<Pair<String, String>> {
    val ctx = LocalContext.current
    return remember {
        val pm = ctx.packageManager
        listOf(Intent.CATEGORY_LEANBACK_LAUNCHER, Intent.CATEGORY_LAUNCHER).flatMap { cat ->
            pm.queryIntentActivities(Intent(Intent.ACTION_MAIN).addCategory(cat), 0).map { it.activityInfo.packageName to it.loadLabel(pm).toString() }
        }.distinctBy { it.first }
    }
}

/** Offizielle App öffnen, sonst den Play Store an der passenden Stelle. */
private fun openService(ctx: android.content.Context, pkg: String?, name: String) {
    val pm = ctx.packageManager
    val i = pkg?.let { pm.getLeanbackLaunchIntentForPackage(it) ?: pm.getLaunchIntentForPackage(it) }
        ?: Intent(Intent.ACTION_VIEW, Uri.parse("market://search?q=" + Uri.encode(name)))
    runCatching { ctx.startActivity(i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)) }
        .onFailure { android.widget.Toast.makeText(ctx, "Play Store nicht verfügbar – „$name“ bitte selbst installieren.", android.widget.Toast.LENGTH_LONG).show() }
}

@Composable
private fun SectionTitle(text: String, sub: String = "") {
    Row(verticalAlignment = Alignment.Bottom, modifier = Modifier.padding(top = 4.dp)) {
        Text(text, color = TvColors.Text, fontSize = 15.sp, fontWeight = FontWeight.Bold)
        if (sub.isNotEmpty()) { Spacer(Modifier.width(10.dp)); Text(sub, color = TvColors.TextSecondary, fontSize = 10.sp) }
    }
}

/** Reiter „Mediatheken & Streaming": öffentlich-rechtliche Mediatheken direkt, Dienste als Kacheln. */
@Composable
fun StreamingView(playVod: (String, String) -> Unit) {
    var open by remember { mutableStateOf<Broadcaster?>(null) }
    val o = open
    if (o != null) { BroadcasterView(o, playVod) { open = null }; return }
    val ctx = LocalContext.current
    val apps = installedApps()
    val first = remember { FocusRequester() }
    LaunchedEffect(Unit) { runCatching { first.requestFocus() } }
    LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        item { SectionTitle("Mediatheken", "öffentlich-rechtlich · kostenlos · legal – läuft direkt in Trendo") }
        item {
            LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp), contentPadding = PaddingValues(4.dp)) {
                items(Mediathek.main) { b -> BroadcasterTile(b, Modifier.then(if (b == Mediathek.main.first()) Modifier.focusRequester(first) else Modifier)) { open = b } }
            }
        }
        item { SectionTitle("Die Dritten") }
        item { LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp), contentPadding = PaddingValues(4.dp)) { items(Mediathek.regional) { b -> BroadcasterTile(b, Modifier, small = true) { open = b } } } }
        for ((label, word) in listOf("Spielfilme" to "spielfilm", "Dokus" to "dokumentation", "Serien" to "serie", "Krimis" to "krimi")) {
            item(key = "mt_$label") { MtRow("$label in den Mediatheken", null, word, if (word == "spielfilm") 4800 else 1500, playVod) }
        }
        item { SectionTitle("Kostenlos mit Werbung", "offizielle Apps") }
        item { LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp), contentPadding = PaddingValues(4.dp)) { items(StreamServices.freeWithAds) { s -> ServiceTile(s, apps) { pkg -> openService(ctx, pkg, s.name) } } } }
        item { SectionTitle("Bezahlte Dienste", "öffnet die offizielle App – Anmeldung dort") }
        item { LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp), contentPadding = PaddingValues(4.dp)) { items(StreamServices.paid) { s -> ServiceTile(s, apps) { pkg -> openService(ctx, pkg, s.name) } } } }
        item { Text("Mediathek-Daten: MediathekViewWeb (frei). Videos kommen direkt von den Sendern; manche sind nur in Deutschland abrufbar.", color = TvColors.TextSecondary, fontSize = 8.5.sp) }
    }
}

@Composable
private fun BroadcasterTile(b: Broadcaster, modifier: Modifier, small: Boolean = false, onClick: () -> Unit) {
    FocusSurface(onClick, modifier.width(if (small) 110.dp else 150.dp).height(if (small) 56.dp else 78.dp), scale = 1.08f, container = Color(b.color)) {
        Box(Modifier.fillMaxSize().background(Brush.linearGradient(listOf(Color(b.color), Color(b.color).copy(alpha = 0.55f), TvColors.Ink))), contentAlignment = Alignment.Center) {
            Text(b.name, color = Color.White, fontSize = if (small) 15.sp else 22.sp, fontWeight = FontWeight.Bold)
        }
    }
}

@Composable
private fun ServiceTile(s: StreamService, apps: List<Pair<String, String>>, pkgOverride: String? = null, open: (String?) -> Unit) {
    val ctx = LocalContext.current
    val pkg = remember(apps, pkgOverride) { pkgOverride ?: StreamServices.match(s.keys, apps) }
    val banner by produceState<ImageBitmap?>(null, pkg) {
        value = pkg?.let { p -> withContext(Dispatchers.IO) { runCatching { (ctx.packageManager.getApplicationBanner(p) ?: ctx.packageManager.getApplicationIcon(p)).toBitmap(240, 135).asImageBitmap() }.getOrNull() } }
    }
    Column {
        FocusSurface({ open(pkg) }, Modifier.width(150.dp).height(84.dp), scale = 1.08f, container = TvColors.Panel) {
            Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                val b = banner
                if (b != null) Image(b, s.name, contentScale = ContentScale.Crop, modifier = Modifier.fillMaxSize())
                else Box(Modifier.fillMaxSize().background(Brush.linearGradient(listOf(TvColors.Panel, TvColors.Violet.copy(alpha = 0.35f)))), contentAlignment = Alignment.Center) {
                    Text(s.name, color = TvColors.Text, fontSize = 15.sp, fontWeight = FontWeight.Bold)
                }
            }
        }
        Text(if (pkg != null) "App öffnen · ${s.note}" else "Im Play Store holen · ${s.note}", color = TvColors.TextSecondary, fontSize = 8.sp, maxLines = 1,
            modifier = Modifier.width(150.dp).padding(top = 3.dp), overflow = TextOverflow.Ellipsis)
    }
}

@Composable
private fun BroadcasterView(b: Broadcaster, playVod: (String, String) -> Unit, back: () -> Unit) {
    val ctx = LocalContext.current
    val apps = installedApps()
    val pkg = StreamServices.mediathekApps[b.name]?.let { StreamServices.match(it, apps) }
    LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        item {
            Row(verticalAlignment = Alignment.CenterVertically) {
                PillButton("‹ Zurück", back)
                Spacer(Modifier.width(12.dp))
                Text("${b.name} Mediathek", color = TvColors.Text, fontSize = 20.sp, fontWeight = FontWeight.Bold)
                Spacer(Modifier.width(12.dp))
                PillButton(if (pkg != null) "Offizielle App öffnen" else "Offizielle App im Play Store", { openService(ctx, pkg, "${b.name} Mediathek") })
            }
        }
        for ((label, word) in Mediathek.categories) item(key = "${b.key}_$label") { MtRow(label, b.key, word, if (word == "film") 4200 else 900, playVod) }
    }
}

/** Reiter „YouTube & Videos": offizielle Video-Apps als Kacheln. */
@Composable
fun VideosView() {
    val ctx = LocalContext.current
    val apps = installedApps()
    // „YouTube" darf nicht „YouTube Kids" treffen: genauer Name zuerst.
    fun pkgFor(s: StreamService) = apps.firstOrNull { it.second.equals(s.name, true) }?.first ?: StreamServices.match(s.keys, apps.filter { a -> s.name != "YouTube" || !a.second.contains("Kids", true) })
    val first = remember { FocusRequester() }
    LaunchedEffect(Unit) { runCatching { first.requestFocus() } }
    Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
        SectionTitle("YouTube & Videoplattformen", "öffnet die offizielle App – Anmeldung und Abos dort")
        LazyRow(horizontalArrangement = Arrangement.spacedBy(10.dp), contentPadding = PaddingValues(4.dp)) {
            items(StreamServices.videos) { s ->
                Box(if (s == StreamServices.videos.first()) Modifier.focusRequester(first) else Modifier) {
                    ServiceTile(s, apps, pkgOverride = pkgFor(s)) { pkg -> openService(ctx, pkg, s.name) }
                }
            }
        }
        Text("Hinweis: YouTube erlaubt nicht, Videos in fremden Playern abzuspielen – deshalb startet Trendo die offizielle App.", color = TvColors.TextSecondary, fontSize = 9.sp)
    }
}

/** Eine Reihe Mediathek-Beiträge (lädt beim ersten Anzeigen). */
@Composable
private fun MtRow(title: String, channel: String?, word: String, minSec: Int, playVod: (String, String) -> Unit) {
    val items by produceState<List<MtItem>?>(null, channel, word) { value = withContext(Dispatchers.IO) { Mediathek.load(channel, word, minSec, 30) } }
    Column {
        SectionTitle(title)
        val list = items
        when {
            list == null -> Text("Lade …", color = TvColors.TextSecondary, fontSize = 10.sp, modifier = Modifier.padding(6.dp))
            list.isEmpty() -> Text("Nichts gefunden (oder offline).", color = TvColors.TextSecondary, fontSize = 10.sp, modifier = Modifier.padding(6.dp))
            else -> LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp), contentPadding = PaddingValues(vertical = 4.dp, horizontal = 4.dp)) {
                items(list, key = { it.url }) { m -> MtCard(m) { playVod(m.url, m.title) } }
            }
        }
    }
}

@Composable
private fun MtCard(m: MtItem, onClick: () -> Unit) {
    val color = (Mediathek.main + Mediathek.regional).firstOrNull { it.key.equals(m.channel, true) }?.color ?: 0xFF317DFF
    FocusSurface(onClick, Modifier.width(200.dp).height(100.dp), scale = 1.07f, container = TvColors.Panel) {
        Column(Modifier.fillMaxSize().background(Brush.verticalGradient(listOf(Color(color).copy(alpha = 0.35f), TvColors.Panel))).padding(8.dp)) {
            Text(m.channel, color = Color.White, fontSize = 8.5.sp, fontWeight = FontWeight.Bold, modifier = Modifier.background(Color(color), RoundedCornerShape(4.dp)).padding(horizontal = 5.dp, vertical = 1.dp))
            Spacer(Modifier.height(4.dp))
            Text(m.title, color = TvColors.Text, fontSize = 11.sp, fontWeight = FontWeight.SemiBold, maxLines = 2, overflow = TextOverflow.Ellipsis)
            Text(m.topic, color = TvColors.TextSecondary, fontSize = 8.5.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
            Spacer(Modifier.weight(1f))
            Text("${Mediathek.minutes(m.durationSec)} · ${day(m.timestamp)}", color = TvColors.Cyan, fontSize = 8.sp)
        }
    }
}

/** Reiter „Heute": Tipps aus dem Programmführer (Sport, Filme, 20:15, Serien, Nachrichten, Kinder). */
@Composable
fun TipsView(rows: List<Tips.Row>, filter: String, open: (Tip) -> Unit) {
    val f = filter.trim().lowercase().removeSuffix("s")
    val shown = if (f.isEmpty()) rows else rows.map { r -> r.copy(tips = r.tips.filter { t -> (t.programme.title + " " + t.programme.category).lowercase().contains(f) || r.title.lowercase().contains(f) }) }.filter { it.tips.isNotEmpty() }
    if (shown.isEmpty()) {
        Box(Modifier.fillMaxWidth().background(TvColors.GlassStrong, RoundedCornerShape(10.dp)).padding(14.dp)) {
            Text(if (rows.isEmpty()) "Noch keine Tipps – dafür braucht es den Programmführer (Quellen → Alles aktualisieren)." else "Zu „$filter“ läuft heute nichts.", color = TvColors.Text, fontSize = 12.sp)
        }
        return
    }
    val now = System.currentTimeMillis()
    val first = remember { FocusRequester() }
    LaunchedEffect(Unit) { runCatching { first.requestFocus() } }
    LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        if (filter.isNotBlank()) item { Text("Gefiltert: „$filter“", color = TvColors.Cyan, fontSize = 10.sp) }
        items(shown, key = { it.title }) { r ->
            Column {
                SectionTitle(r.title)
                LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp), contentPadding = PaddingValues(vertical = 4.dp, horizontal = 4.dp)) {
                    items(r.tips, key = { it.channel.id + it.programme.start }) { t ->
                        val p = t.programme
                        FocusSurface({ open(t) }, Modifier.width(220.dp).height(96.dp).then(if (r == shown.first() && t == r.tips.first()) Modifier.focusRequester(first) else Modifier),
                            scale = 1.06f, container = TvColors.Panel) {
                            Row(Modifier.fillMaxSize().padding(8.dp)) {
                                Box(Modifier.width(52.dp).height(32.dp).background(Color(0x33FFFFFF), RoundedCornerShape(5.dp)), contentAlignment = Alignment.Center) {
                                    NetImage(t.channel.logo, Modifier.fillMaxSize().padding(2.dp), 120, 70, crop = false)
                                }
                                Spacer(Modifier.width(8.dp))
                                Column(Modifier.weight(1f)) {
                                    Text(p.title, color = TvColors.Text, fontSize = 11.sp, fontWeight = FontWeight.SemiBold, maxLines = 2, overflow = TextOverflow.Ellipsis)
                                    Text(t.channel.name, color = TvColors.TextSecondary, fontSize = 8.5.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                                    Spacer(Modifier.weight(1f))
                                    val hm = SimpleDateFormat("HH:mm", Locale.GERMANY)
                                    Text(if (p.isOn(now)) "läuft · bis ${hm.format(Date(p.stop))}" else "${hm.format(Date(p.start))} Uhr · ⏰ Erinnerung", color = if (p.isOn(now)) TvColors.Cyan else TvColors.Violet, fontSize = 8.5.sp)
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
