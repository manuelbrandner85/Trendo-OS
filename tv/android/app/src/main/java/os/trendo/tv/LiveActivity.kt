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
    private lateinit var store: LiveStore

    private val channels get() = LiveSession.channels

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        store = LiveStore(this, LiveSession.profile)
        if (channels.isEmpty()) { finish(); return }
        index = intent.getIntExtra(EXTRA_INDEX, 0).coerceIn(0, channels.lastIndex)
        val p = ExoPlayer.Builder(this).build()
        p.addListener(object : Player.Listener {
            override fun onPlayerError(e: PlaybackException) { error = "Sender nicht erreichbar (${e.errorCodeName.removePrefix("ERROR_CODE_").lowercase()})." }
            override fun onPlaybackStateChanged(state: Int) { if (state == Player.STATE_READY) error = null }
        })
        player = p
        tune(index, ask = true)

        setContent {
            Box(Modifier.fillMaxSize().background(Color.Black)) {
                AndroidView({ ctx -> PlayerView(ctx).apply { useController = false; this.player = p } }, Modifier.fillMaxSize())
                pipIndex?.let { pi -> PipView(pi) }
                BannerOverlay()
                error?.let { Text(it, color = TvColors.Text, fontSize = 14.sp, modifier = Modifier.align(Alignment.Center).background(Color(0xCC050A14), RoundedCornerShape(8.dp)).padding(14.dp)) }
                if (digits.isNotEmpty()) Text(digits, color = TvColors.Text, fontSize = 40.sp, fontWeight = FontWeight.Bold,
                    modifier = Modifier.align(Alignment.TopEnd).padding(30.dp).background(Color(0xCC050A14), RoundedCornerShape(8.dp)).padding(horizontal = 18.dp, vertical = 6.dp))
                if (showList) ChannelList()
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
        index = i; banner++; error = null
        player?.apply { setMediaItem(MediaItem.fromUri(Uri.parse(c.url))); prepare(); playWhenReady = true }
        store.save(store.settings().copy(lastChannel = c.id))
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
        if (showList || askPin != null) return super.dispatchKeyEvent(event)
        if (event.action != KeyEvent.ACTION_DOWN) return super.dispatchKeyEvent(event)
        when (event.keyCode) {
            KeyEvent.KEYCODE_DPAD_UP, KeyEvent.KEYCODE_CHANNEL_UP -> { zap(+1); return true }
            KeyEvent.KEYCODE_DPAD_DOWN, KeyEvent.KEYCODE_CHANNEL_DOWN -> { zap(-1); return true }
            KeyEvent.KEYCODE_DPAD_CENTER, KeyEvent.KEYCODE_ENTER -> { showList = true; return true }
            KeyEvent.KEYCODE_INFO, KeyEvent.KEYCODE_DPAD_RIGHT -> { banner++; return true }
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
                Text("▲▼ Sender · OK Liste · ◀ Bild-in-Bild · Ziffern Sendernummer", color = TvColors.TextSecondary.copy(alpha = 0.7f), fontSize = 9.sp)
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
        fun intent(ctx: Context, index: Int) = Intent(ctx, LiveActivity::class.java).putExtra(EXTRA_INDEX, index)
    }
}
