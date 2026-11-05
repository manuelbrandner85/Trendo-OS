package os.trendo.tv

import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.view.KeyEvent
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.focusable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
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
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.input.key.KeyEventType
import androidx.compose.ui.input.key.key
import androidx.compose.ui.input.key.onKeyEvent
import androidx.compose.ui.input.key.type
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.media3.common.MediaItem
import androidx.media3.common.PlaybackException
import androidx.media3.common.Player
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.ui.PlayerView
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.launch
import androidx.tv.material3.Text
import kotlinx.coroutines.delay
import os.trendo.tv.livetv.Channel
import os.trendo.tv.livetv.LiveStore
import os.trendo.tv.livetv.Programme
import os.trendo.tv.livetv.TvRules
import os.trendo.tv.ui.FocusSurface
import os.trendo.tv.ui.TvColors
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/** Übergabe der (gefilterten) Senderliste an den Player, ohne große Intents. */
object LiveSession {
    var channels: List<Channel> = emptyList()
    var epg: Map<String, List<Programme>> = emptyMap()
    var locked: Set<String> = emptySet()
    /** Alle Sender inkl. zusammengeführter Doppelter – für den Ruckelschutz (Ersatz-Stream). */
    var allChannels: List<Channel> = emptyList()
    var favorites: Set<String> = emptySet()
    var unlocked = false
    var profile = "standard"
}

/**
 * Trendo Live TV – Vollbild-Player. Hoch/Runter = Sender, OK = Senderliste, Ziffern = Sendernummer,
 * Zurück = Liste schließen bzw. beenden. Spielt nur die eigenen Quellen des Nutzers.
 */
class LiveActivity : ComponentActivity() {
    private var player: ExoPlayer? = null
    private var pip: ExoPlayer? = null
    private var index by mutableIntStateOf(0)
    private var banner by mutableIntStateOf(0)
    private var showList by mutableStateOf(false)
    private var digits by mutableStateOf("")
    private var error by mutableStateOf<String?>(null)
    private var askPin by mutableStateOf<Int?>(null)
    private var pipIndex by mutableStateOf<Int?>(null)
    private var showMenu by mutableStateOf(false)
    private var quality by mutableStateOf("")
    private var resize by mutableIntStateOf(androidx.media3.ui.AspectRatioFrameLayout.RESIZE_MODE_FIT)
    private var retries = 0
    /** Zeitversatz: Inhaltszeit (Wanduhr), ab der die aktuelle Archiv-Wiedergabe läuft; null = live. */
    private var shiftStart by mutableStateOf<Long?>(null)
    private var pausedAt: Long? = null
    private val tried = HashSet<String>()
    private lateinit var store: LiveStore

    private val channels get() = LiveSession.channels

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        store = LiveStore(this, LiveSession.profile)
        if (channels.isEmpty()) { finish(); return }
        index = intent.getIntExtra(EXTRA_INDEX, 0).coerceIn(0, channels.lastIndex)
        // Ruckelschutz: größerer Puffer für schwankendes WLAN.
        val load = androidx.media3.exoplayer.DefaultLoadControl.Builder().setBufferDurationsMs(15_000, 60_000, 2_500, 5_000).build()
        val p = ExoPlayer.Builder(this).setLoadControl(load).build()
        p.addListener(object : Player.Listener {
            override fun onPlayerError(e: PlaybackException) = recover(e)
            override fun onPlaybackStateChanged(state: Int) { if (state == Player.STATE_READY) { error = null; retries = 0 } }
        })
        // Kinderprofil: Zeitlimit pro Tag (jede volle Minute Wiedergabe zählt).
        lifecycleScope.launch {
            val ps = os.trendo.tv.livetv.ProfileStore(this@LiveActivity)
            while (true) {
                delay(60_000)
                if (player?.isPlaying == true) ps.addMinute(LiveSession.profile)
                val prof = ps.list().firstOrNull { it.id == LiveSession.profile }
                if (os.trendo.tv.livetv.KidRules.limitReached(prof, ps.usedToday(LiveSession.profile))) {
                    player?.pause(); error = "Die Fernsehzeit für heute ist aufgebraucht. Bis morgen!"
                    delay(6000); finish(); break
                }
            }
        }
        // Verbindungsqualität aus dem Puffer-Vorrat (alle 2 s, nur Anzeige).
        lifecycleScope.launch {
            while (true) {
                delay(2000)
                val pl = player ?: continue
                val ahead = (pl.bufferedPosition - pl.currentPosition).coerceAtLeast(0)
                quality = when {
                    pl.playbackState == Player.STATE_BUFFERING -> "lädt …"
                    ahead > 8000 -> "Verbindung gut"
                    ahead > 3000 -> "Verbindung mittel"
                    else -> "Verbindung schwach"
                }
            }
        }
        player = p
        tune(index, ask = true)
        // Aus dem Programmführer „Nachholen": direkt ab Sendungsbeginn aus dem Archiv.
        intent.getLongExtra(EXTRA_START, 0L).takeIf { it > 0 }?.let { shiftTo(it) }

        setContent {
            Box(Modifier.fillMaxSize().background(Color.Black)) {
                AndroidView({ ctx -> PlayerView(ctx).apply { useController = false; this.player = p } }, Modifier.fillMaxSize(), update = { it.resizeMode = resize })
                pipIndex?.let { pi -> PipView(pi) }
                BannerOverlay()
                error?.let { Text(it, color = TvColors.Text, fontSize = 14.sp, modifier = Modifier.align(Alignment.Center).background(Color(0xCC050A14), RoundedCornerShape(8.dp)).padding(14.dp)) }
                if (digits.isNotEmpty()) Text(digits, color = TvColors.Text, fontSize = 40.sp, fontWeight = FontWeight.Bold,
                    modifier = Modifier.align(Alignment.TopEnd).padding(30.dp).background(Color(0xCC050A14), RoundedCornerShape(8.dp)).padding(horizontal = 18.dp, vertical = 6.dp))
                if (showList) ChannelList()
                if (showMenu) OptionsMenu()
                askPin?.let { target -> PinDialog(onOk = { pin ->
                    if (store.checkPin(pin)) { LiveSession.unlocked = true; askPin = null; tune(target, ask = false) } else error = "PIN falsch."
                }, onCancel = { askPin = null }) }
            }
        }
    }

    private fun isLocked(c: Channel) = c.group in LiveSession.locked && !LiveSession.unlocked

    private fun tune(i: Int, ask: Boolean) {
        val c = channels.getOrNull(i) ?: return
        if (ask && isLocked(c)) { askPin = i; return }
        index = i; banner++; error = null; retries = 0; tried.clear(); shiftStart = null; pausedAt = null
        play(c.url)
        store.save(store.settings().copy(lastChannel = c.id))
        store.addHistory(c.id)
    }

    private fun play(url: String) {
        tried += url
        player?.apply { setMediaItem(MediaItem.fromUri(Uri.parse(url))); prepare(); playWhenReady = true }
    }

    /** Aktuelle Inhaltszeit (bei Zeitversatz in der Vergangenheit). */
    private fun contentNow(): Long = shiftStart?.let { it + (player?.currentPosition ?: 0) } ?: System.currentTimeMillis()

    /** Zu einem Zeitpunkt springen: aus dem Archiv, oder zurück zu Live, wenn er fast jetzt ist. */
    private fun shiftTo(t: Long) {
        val c = channels.getOrNull(index) ?: return
        val now = System.currentTimeMillis()
        if (t >= now - 15_000) { goLive(); return }
        if (!TvRules.canCatchup(c, t, now)) { error = "Dieser Sender hat kein Archiv – zurückspulen geht hier nicht."; return }
        val url = TvRules.catchupUrl(c, t, ((now - t) / 60_000L).toInt() + 120) ?: return
        shiftStart = t; error = null; banner++
        play(url)
    }

    private fun goLive() {
        val c = channels.getOrNull(index) ?: return
        shiftStart = null; pausedAt = null; banner++
        play(c.url)
    }

    /** Ruckelschutz: bis zu 4 Neuversuche mit wachsender Pause, danach ein gleicher Sender in anderer Qualität. */
    private fun recover(e: PlaybackException) {
        val c = channels.getOrNull(index) ?: return
        if (retries < 4) {
            retries++
            error = "Verbindung unterbrochen – neuer Versuch $retries/4 …"
            window.decorView.postDelayed({ player?.apply { prepare(); playWhenReady = true } }, 1500L shl (retries - 1))
            return
        }
        val base = TvRules.baseName(c.name)
        val alt = LiveSession.allChannels.firstOrNull { it.url !in tried && TvRules.baseName(it.name) == base }
        if (alt != null) { retries = 0; error = "Wechsle auf Ersatz-Stream (${alt.name}) …"; play(alt.url); return }
        error = "Sender nicht erreichbar (${e.errorCodeName.removePrefix("ERROR_CODE_").lowercase()}). ▲▼ anderer Sender."
    }

    /** Nächster/vorheriger Sender; gesperrte Gruppen werden beim Durchschalten übersprungen. */
    private fun zap(step: Int) {
        if (channels.isEmpty()) return
        var i = index
        repeat(channels.size) {
            i = (i + step + channels.size) % channels.size
            if (!isLocked(channels[i])) { tune(i, ask = false); return }
        }
    }

    override fun dispatchKeyEvent(event: KeyEvent): Boolean {
        if (showList || showMenu || askPin != null) return super.dispatchKeyEvent(event)
        if (event.action != KeyEvent.ACTION_DOWN) return super.dispatchKeyEvent(event)
        when (event.keyCode) {
            KeyEvent.KEYCODE_MEDIA_PLAY_PAUSE, KeyEvent.KEYCODE_MEDIA_PAUSE, KeyEvent.KEYCODE_MEDIA_PLAY -> {
                val p = player ?: return true
                if (p.isPlaying && event.keyCode != KeyEvent.KEYCODE_MEDIA_PLAY) {
                    pausedAt = contentNow(); p.pause(); banner++
                    val c = channels.getOrNull(index)
                    error = if (c != null && c.catchup != null && c.archiveDays > 0) "Angehalten – ▶ setzt genau hier fort." else "Angehalten – dieser Sender hat kein Archiv, ▶ geht live weiter."
                } else {
                    val at = pausedAt
                    error = null
                    if (at != null && shiftStart == null) shiftTo(at) else p.play()
                    pausedAt = null
                }
                return true
            }
            KeyEvent.KEYCODE_MEDIA_REWIND -> { shiftTo(contentNow() - 60_000); return true }
            KeyEvent.KEYCODE_MEDIA_FAST_FORWARD -> { shiftTo(contentNow() + 60_000); return true }
            KeyEvent.KEYCODE_DPAD_UP, KeyEvent.KEYCODE_CHANNEL_UP -> { zap(+1); return true }
            KeyEvent.KEYCODE_DPAD_DOWN, KeyEvent.KEYCODE_CHANNEL_DOWN -> { zap(-1); return true }
            KeyEvent.KEYCODE_DPAD_CENTER, KeyEvent.KEYCODE_ENTER -> { showList = true; return true }
            KeyEvent.KEYCODE_INFO -> { banner++; return true }
            KeyEvent.KEYCODE_DPAD_RIGHT, KeyEvent.KEYCODE_MENU -> { showMenu = true; return true }
            KeyEvent.KEYCODE_PROG_RED, KeyEvent.KEYCODE_DPAD_LEFT -> { togglePip(); return true }
            KeyEvent.KEYCODE_LAST_CHANNEL -> { zap(-1); return true }
        }
        if (event.keyCode in KeyEvent.KEYCODE_0..KeyEvent.KEYCODE_9) {
            digits = (digits + (event.keyCode - KeyEvent.KEYCODE_0)).takeLast(4)
            val d = digits
            window.decorView.postDelayed({
                if (digits == d) {
                    val n = d.toInt(); digits = ""
                    val i = channels.indexOfFirst { it.number == n }.takeIf { it >= 0 } ?: (n - 1)
                    if (i in channels.indices) tune(i, ask = true) else error = "Sender $n gibt es nicht."
                }
            }, 1500)
            return true
        }
        return super.dispatchKeyEvent(event)
    }

    /** Bild-in-Bild: zweiter Sender klein (Links-Taste). Auf schwachen Fernsehern kann das ruckeln – dann wieder aus. */
    private fun togglePip() {
        if (pipIndex != null) { pip?.release(); pip = null; pipIndex = null; return }
        val i = (index + 1) % channels.size
        val p = ExoPlayer.Builder(this).build().apply { volume = 0f; setMediaItem(MediaItem.fromUri(Uri.parse(channels[i].url))); prepare(); playWhenReady = true }
        pip = p; pipIndex = i
    }

    @Composable
    private fun PipView(pi: Int) {
        Box(Modifier.fillMaxSize().padding(24.dp), contentAlignment = Alignment.TopEnd) {
            Column(Modifier.width(320.dp).border(2.dp, TvColors.FocusRing, RoundedCornerShape(6.dp)).background(Color.Black)) {
                AndroidView({ ctx -> PlayerView(ctx).apply { useController = false; player = pip } }, Modifier.fillMaxWidth().height(180.dp))
                Text(channels.getOrNull(pi)?.name ?: "", color = TvColors.Text, fontSize = 10.sp, modifier = Modifier.padding(4.dp))
            }
        }
    }

    @Composable
    private fun BannerOverlay() {
        var visible by remember { mutableStateOf(true) }
        LaunchedEffect(banner) { visible = true; delay(4500); visible = false }
        if (!visible || showList) return
        val c = channels.getOrNull(index) ?: return
        val now = System.currentTimeMillis()
        val (cur, next) = TvRules.nowNext(c.epgId?.let { LiveSession.epg[it] }, now)
        Box(Modifier.fillMaxSize(), contentAlignment = Alignment.BottomCenter) {
            Column(Modifier.fillMaxWidth().background(androidx.compose.ui.graphics.Brush.verticalGradient(listOf(Color.Transparent, Color(0xE6050A14)))).padding(start = 48.dp, end = 48.dp, bottom = 28.dp, top = 40.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text("${c.number}", color = TvColors.Cyan, fontSize = 26.sp, fontWeight = FontWeight.Bold)
                    Spacer(Modifier.width(14.dp))
                    Text(c.name, color = TvColors.Text, fontSize = 24.sp, fontWeight = FontWeight.Bold)
                    Spacer(Modifier.width(14.dp))
                    Text(c.group, color = TvColors.TextSecondary, fontSize = 12.sp)
                    Spacer(Modifier.weight(1f))
                    Text(SimpleDateFormat("HH:mm", Locale.GERMANY).format(Date(now)), color = TvColors.Text, fontSize = 20.sp)
                }
                Spacer(Modifier.height(6.dp))
                if (cur != null) {
                    Text("${t(cur.start)}–${t(cur.stop)}  ${cur.title}", color = TvColors.Text, fontSize = 15.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                    Box(Modifier.padding(vertical = 5.dp).width(420.dp).height(3.dp).background(TvColors.Line)) {
                        Box(Modifier.fillMaxWidth(cur.progress(now)).fillMaxHeight().background(TvColors.Prism))
                    }
                } else Text("Kein Programmführer für diesen Sender", color = TvColors.TextSecondary, fontSize = 12.sp)
                next?.let { Text("Danach ${t(it.start)}  ${it.title}", color = TvColors.TextSecondary, fontSize = 12.sp, maxLines = 1) }
                shiftStart?.let {
                    val behind = ((System.currentTimeMillis() - contentNow()) / 60_000L).coerceAtLeast(0)
                    Text("⏪ Zeitversetzt – ${behind} Min. hinter Live (◀◀ ▶▶ spulen, Optionen → Zurück zu Live)", color = TvColors.Violet, fontSize = 10.sp)
                }
                if (quality.isNotEmpty()) Text(quality, color = if (quality.contains("schwach")) TvColors.Magenta else TvColors.Cyan, fontSize = 9.sp)
                Text("▲▼ Sender · OK Liste · ▶ Optionen · ◀ Bild-in-Bild · Ziffern Sendernummer", color = TvColors.TextSecondary.copy(alpha = 0.7f), fontSize = 9.sp)
            }
        }
    }

    @Composable
    private fun ChannelList() {
        val state = rememberLazyListState(initialFirstVisibleItemIndex = (index - 4).coerceAtLeast(0))
        val focus = remember { FocusRequester() }
        LaunchedEffect(Unit) { runCatching { focus.requestFocus() } }
        val now = System.currentTimeMillis()
        Row(Modifier.fillMaxSize().onKeyEvent { e ->
            if (e.type == KeyEventType.KeyDown && (e.key == Key.Back || e.key == Key.Escape)) { showList = false; true } else false
        }) {
            Column(Modifier.width(520.dp).fillMaxHeight().background(Color(0xF2050A14)).padding(16.dp)) {
                Text("Senderliste · ${channels.size}", color = TvColors.Text, fontSize = 16.sp, fontWeight = FontWeight.Bold)
                Spacer(Modifier.height(8.dp))
                LazyColumn(state = state, verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    itemsIndexed(channels, key = { _, c -> c.id }) { i, c ->
                        val (cur, _) = TvRules.nowNext(c.epgId?.let { LiveSession.epg[it] }, now)
                        FocusSurface({ showList = false; tune(i, ask = true) }, Modifier.fillMaxWidth().height(44.dp).then(if (i == index) Modifier.focusRequester(focus) else Modifier),
                            container = if (i == index) TvColors.Panel else Color.Transparent, scale = 1.02f, idleBorder = false) {
                            Row(Modifier.fillMaxSize().padding(horizontal = 10.dp), verticalAlignment = Alignment.CenterVertically) {
                                Text("${c.number}", color = TvColors.Cyan, fontSize = 12.sp, modifier = Modifier.width(44.dp))
                                Column(Modifier.weight(1f)) {
                                    Text((if (c.group in LiveSession.locked && !LiveSession.unlocked) "🔒 " else "") + c.name, color = TvColors.Text, fontSize = 13.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                                    Text(cur?.title ?: c.group, color = TvColors.TextSecondary, fontSize = 9.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                                }
                            }
                        }
                    }
                }
            }
            Box(Modifier.weight(1f).fillMaxHeight().focusable())
        }
    }

    /** Optionen (Rechts-Taste): Ton, Untertitel, Bildformat, Stream-Info, Favorit. */
    @Composable
    private fun OptionsMenu() {
        val p = player ?: return
        var page by remember { mutableStateOf("main") }
        val focus = remember { FocusRequester() }
        LaunchedEffect(page) { runCatching { focus.requestFocus() } }
        val c = channels.getOrNull(index)
        fun tracks(type: Int): List<Pair<String, () -> Unit>> = p.currentTracks.groups.filter { it.type == type }.flatMap { g ->
            (0 until g.length).map { i ->
                val f = g.getTrackFormat(i)
                val lang = f.language?.let { java.util.Locale(it).getDisplayLanguage(Locale.GERMANY) }
                val name = listOfNotNull(f.label, lang, f.codecs).joinToString(" · ").ifBlank { "Spur " + (i + 1) }
                (if (g.isTrackSelected(i)) "● $name" else "○ $name") to {
                    p.trackSelectionParameters = p.trackSelectionParameters.buildUpon().setTrackTypeDisabled(type, false)
                        .setOverrideForType(androidx.media3.common.TrackSelectionOverride(g.mediaTrackGroup, i)).build()
                    showMenu = false
                }
            }
        }
        val audioT = androidx.media3.common.C.TRACK_TYPE_AUDIO
        val textT = androidx.media3.common.C.TRACK_TYPE_TEXT
        val items: List<Pair<String, () -> Unit>> = when (page) {
            "audio" -> tracks(audioT).ifEmpty { listOf("Keine weiteren Tonspuren" to { page = "main" }) }
            "text" -> listOf("Untertitel aus" to {
                p.trackSelectionParameters = p.trackSelectionParameters.buildUpon().setTrackTypeDisabled(textT, true).build(); showMenu = false
            }) + tracks(textT)
            "info" -> {
                val v = p.videoFormat; val a = p.audioFormat
                val bild = v?.let { "" + it.width + "×" + it.height + (if (it.frameRate > 0) " · " + it.frameRate.toInt() + " fps" else "") + " · " + (it.codecs ?: it.sampleMimeType ?: "?") } ?: "–"
                val ton = a?.let { (it.codecs ?: it.sampleMimeType ?: "?") + " · " + it.channelCount + " Kanäle" } ?: "–"
                val rate = v?.bitrate?.takeIf { it > 0 }?.let { "" + (it / 1000) + " kbit/s" } ?: "unbekannt"
                val buf = (p.bufferedPosition - p.currentPosition).coerceAtLeast(0) / 1000
                listOf("Bild: $bild", "Ton: $ton", "Bitrate: $rate", "Puffer: $buf s · $quality").map { it to { page = "main" } }
            }
            else -> timeItems() + listOf(
                "Tonspur" to { page = "audio" },
                "Untertitel" to { page = "text" },
                ("Bildformat: " + when (resize) { androidx.media3.ui.AspectRatioFrameLayout.RESIZE_MODE_ZOOM -> "Zoom"; androidx.media3.ui.AspectRatioFrameLayout.RESIZE_MODE_FILL -> "Strecken"; else -> "Anpassen" }) to {
                    resize = when (resize) {
                        androidx.media3.ui.AspectRatioFrameLayout.RESIZE_MODE_FIT -> androidx.media3.ui.AspectRatioFrameLayout.RESIZE_MODE_ZOOM
                        androidx.media3.ui.AspectRatioFrameLayout.RESIZE_MODE_ZOOM -> androidx.media3.ui.AspectRatioFrameLayout.RESIZE_MODE_FILL
                        else -> androidx.media3.ui.AspectRatioFrameLayout.RESIZE_MODE_FIT
                    }
                },
                "Stream-Infos" to { page = "info" },
                "▦ Multi-View: 2 Sender" to { showMenu = false; startActivity(MultiViewActivity.intent(this@LiveActivity, index, 2)); finish() },
                "▦ Multi-View: 4 Sender" to { showMenu = false; startActivity(MultiViewActivity.intent(this@LiveActivity, index, 4)); finish() },
                (if (c != null && c.id in store.settings().favorites) "★ Aus Favoriten entfernen" else "☆ Zu Favoriten") to {
                    c?.let { ch -> val s = store.settings(); store.save(s.copy(favorites = if (ch.id in s.favorites) s.favorites - ch.id else s.favorites + ch.id)) }
                    showMenu = false
                },
                "Schließen" to { showMenu = false },
            )
        }
        Box(Modifier.fillMaxSize().onKeyEvent { e ->
            if (e.type == KeyEventType.KeyDown && (e.key == Key.Back || e.key == Key.Escape)) { if (page == "main") showMenu = false else page = "main"; true } else false
        }, contentAlignment = Alignment.CenterEnd) {
            Column(Modifier.width(380.dp).fillMaxHeight().background(Color(0xF2050A14)).padding(18.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                Text(when (page) { "audio" -> "Tonspur"; "text" -> "Untertitel"; "info" -> "Stream-Infos"; else -> "Optionen" }, color = TvColors.Text, fontSize = 16.sp, fontWeight = FontWeight.Bold)
                items.forEachIndexed { i, (label, act) ->
                    FocusSurface(act, Modifier.fillMaxWidth().height(40.dp).then(if (i == 0) Modifier.focusRequester(focus) else Modifier), scale = 1.02f) {
                        Box(Modifier.fillMaxSize().padding(horizontal = 12.dp), contentAlignment = Alignment.CenterStart) {
                            Text(label, color = TvColors.Text, fontSize = 12.sp, maxLines = 1, overflow = TextOverflow.Ellipsis)
                        }
                    }
                }
            }
        }
    }

    /** Menüpunkte für Zeitversatz/Catch-up (nur wenn der Sender ein Archiv hat). */
    private fun timeItems(): List<Pair<String, () -> Unit>> {
        val c = channels.getOrNull(index) ?: return emptyList()
        val now = System.currentTimeMillis()
        val out = ArrayList<Pair<String, () -> Unit>>()
        val (cur, _) = TvRules.nowNext(c.epgId?.let { LiveSession.epg[it] }, contentNow())
        if (cur != null && TvRules.canCatchup(c, cur.start, now)) out += "⏮ Sendung von Anfang an" to { showMenu = false; shiftTo(cur.start) }
        if (c.catchup != null && c.archiveDays > 0) out += "⏪ 5 Minuten zurück" to { showMenu = false; shiftTo(contentNow() - 5 * 60_000) }
        if (shiftStart != null) out += "⏩ Zurück zu Live" to { showMenu = false; goLive() }
        return out
    }

    @Composable
    private fun PinDialog(onOk: (String) -> Unit, onCancel: () -> Unit) {
        var pin by remember { mutableStateOf("") }
        val focus = remember { FocusRequester() }
        LaunchedEffect(Unit) { runCatching { focus.requestFocus() } }
        Box(Modifier.fillMaxSize().background(Color(0xCC000000)), contentAlignment = Alignment.Center) {
            Column(Modifier.width(360.dp).background(TvColors.GlassStrong, RoundedCornerShape(12.dp)).border(1.dp, TvColors.FocusRing, RoundedCornerShape(12.dp)).padding(20.dp)
                .focusRequester(focus).focusable().onKeyEvent { e ->
                    if (e.type != KeyEventType.KeyDown) return@onKeyEvent false
                    val n = e.nativeKeyEvent.keyCode - KeyEvent.KEYCODE_0
                    when {
                        n in 0..9 -> { pin = (pin + n).take(8); if (pin.length == 4) onOk(pin); true }
                        e.key == Key.Back || e.key == Key.Escape -> { onCancel(); true }
                        e.key == Key.DirectionCenter || e.key == Key.Enter -> { onOk(pin); true }
                        else -> false
                    }
                }, horizontalAlignment = Alignment.CenterHorizontally) {
                Text("Gesperrte Gruppe", color = TvColors.Text, fontSize = 16.sp, fontWeight = FontWeight.Bold)
                Spacer(Modifier.height(8.dp))
                Text("PIN mit den Zifferntasten eingeben", color = TvColors.TextSecondary, fontSize = 11.sp)
                Spacer(Modifier.height(12.dp))
                Text("●".repeat(pin.length).ifEmpty { "– – – –" }, color = TvColors.Cyan, fontSize = 26.sp)
            }
        }
    }

    private fun t(ms: Long) = SimpleDateFormat("HH:mm", Locale.GERMANY).format(Date(ms))

    override fun onStop() { super.onStop(); player?.pause(); pip?.pause() }
    override fun onStart() { super.onStart(); player?.play(); pip?.play() }
    override fun onDestroy() { super.onDestroy(); player?.release(); pip?.release(); player = null; pip = null }

    companion object {
        const val EXTRA_INDEX = "index"
        const val EXTRA_START = "start"
        fun intent(ctx: Context, index: Int, catchupStart: Long = 0L) =
            Intent(ctx, LiveActivity::class.java).putExtra(EXTRA_INDEX, index).putExtra(EXTRA_START, catchupStart)
    }
}
