package os.trendo.tv

import android.app.ActivityManager
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.view.KeyEvent
import android.view.WindowManager
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.media3.common.MediaItem
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.ui.PlayerView
import androidx.tv.material3.Text
import os.trendo.tv.ui.TvColors

/**
 * Multi-View: 2 oder 4 Sender gleichzeitig. Pfeile = Feld wählen, nur das markierte Feld hat Ton,
 * Hoch/Runter (bzw. Kanal ±) = im markierten Feld umschalten, OK = Vollbild, Zurück = beenden.
 * Auf Fernsehern mit wenig freiem Speicher automatisch höchstens 2 Felder.
 */
class MultiViewActivity : ComponentActivity() {
    private val players = ArrayList<ExoPlayer>()
    private val indices = mutableStateListOf<Int>()
    private var focus by mutableIntStateOf(0)
    private val channels get() = LiveSession.channels

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        if (channels.isEmpty()) { finish(); return }
        var count = intent.getIntExtra(EXTRA_COUNT, 2).coerceIn(2, 4)
        val mi = ActivityManager.MemoryInfo().also { getSystemService(ActivityManager::class.java).getMemoryInfo(it) }
        if (count > 2 && mi.availMem < 350L * 1024 * 1024) {
            count = 2
            Toast.makeText(this, "Wenig freier Speicher – Multi-View mit 2 Sendern.", Toast.LENGTH_LONG).show()
        }
        val start = intent.getIntExtra(EXTRA_INDEX, 0)
        val favs = LiveSession.favorites.mapNotNull { id -> channels.indexOfFirst { it.id == id }.takeIf { it >= 0 } }.filter { it != start }
        val pick = (listOf(start) + favs + (1 until channels.size).map { (start + it) % channels.size }).distinct().take(count)
        // Kleiner Puffer je Feld – mehrere Streams teilen sich Speicher und Leitung.
        val load = androidx.media3.exoplayer.DefaultLoadControl.Builder().setBufferDurationsMs(4_000, 12_000, 1_500, 2_500).build()
        pick.forEachIndexed { i, idx ->
            indices += idx
            players += ExoPlayer.Builder(this).setLoadControl(load).build().apply {
                volume = if (i == 0) 1f else 0f
                setMediaItem(MediaItem.fromUri(Uri.parse(channels[idx].url))); prepare(); playWhenReady = true
            }
        }
        setContent {
            Box(Modifier.fillMaxSize().background(Color.Black)) {
                val rows = if (players.size <= 2) listOf(players.indices.toList()) else listOf(listOf(0, 1), listOf(2, 3).filter { it < players.size })
                Column(Modifier.fillMaxSize()) {
                    rows.forEach { row ->
                        Row(Modifier.weight(1f)) {
                            row.forEach { i ->
                                Box(Modifier.weight(1f).fillMaxSize().padding(3.dp)
                                    .border(if (i == focus) 3.dp else 1.dp, if (i == focus) TvColors.Cyan else TvColors.Line)) {
                                    AndroidView({ c -> PlayerView(c).apply { useController = false; player = players[i] } }, Modifier.fillMaxSize())
                                    Text((if (i == focus) "🔊 " else "") + (channels.getOrNull(indices.getOrElse(i) { 0 })?.name ?: ""), color = TvColors.Text, fontSize = 11.sp,
                                        modifier = Modifier.align(Alignment.BottomStart).background(Color(0xAA050A14)).padding(horizontal = 8.dp, vertical = 3.dp))
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private fun focusTile(i: Int) {
        if (i !in players.indices) return
        focus = i
        players.forEachIndexed { k, p -> p.volume = if (k == i) 1f else 0f }
    }

    private fun zapFocused(step: Int) {
        val cur = indices.getOrNull(focus) ?: return
        val next = (cur + step + channels.size) % channels.size
        indices[focus] = next
        players[focus].apply { setMediaItem(MediaItem.fromUri(Uri.parse(channels[next].url))); prepare(); playWhenReady = true }
    }

    override fun dispatchKeyEvent(event: KeyEvent): Boolean {
        if (event.action != KeyEvent.ACTION_DOWN) return super.dispatchKeyEvent(event)
        val cols = 2
        when (event.keyCode) {
            KeyEvent.KEYCODE_DPAD_RIGHT -> { focusTile(if (focus % cols == 0 && focus + 1 < players.size) focus + 1 else focus); return true }
            KeyEvent.KEYCODE_DPAD_LEFT -> { focusTile(if (focus % cols == 1) focus - 1 else focus); return true }
            KeyEvent.KEYCODE_DPAD_DOWN -> { if (players.size > 2 && focus < 2 && focus + 2 < players.size) focusTile(focus + 2) else zapFocused(-1); return true }
            KeyEvent.KEYCODE_DPAD_UP -> { if (players.size > 2 && focus >= 2) focusTile(focus - 2) else zapFocused(+1); return true }
            KeyEvent.KEYCODE_CHANNEL_UP -> { zapFocused(+1); return true }
            KeyEvent.KEYCODE_CHANNEL_DOWN -> { zapFocused(-1); return true }
            KeyEvent.KEYCODE_DPAD_CENTER, KeyEvent.KEYCODE_ENTER -> {
                val idx = indices.getOrNull(focus) ?: return true
                startActivity(LiveActivity.intent(this, idx)); finish(); return true
            }
        }
        return super.dispatchKeyEvent(event)
    }

    override fun onStop() { super.onStop(); players.forEach { it.pause() } }
    override fun onStart() { super.onStart(); players.forEach { it.play() } }
    override fun onDestroy() { super.onDestroy(); players.forEach { it.release() }; players.clear() }

    companion object {
        const val EXTRA_COUNT = "count"
        const val EXTRA_INDEX = "index"
        fun intent(ctx: Context, index: Int, count: Int) = Intent(ctx, MultiViewActivity::class.java).putExtra(EXTRA_INDEX, index).putExtra(EXTRA_COUNT, count)
    }
}
