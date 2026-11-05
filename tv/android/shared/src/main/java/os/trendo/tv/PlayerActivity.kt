package os.trendo.tv

import android.app.Activity
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Bundle
import android.view.KeyEvent
import android.view.WindowManager
import android.widget.Toast
import androidx.media3.common.MediaItem
import androidx.media3.common.PlaybackException
import androidx.media3.common.Player
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.ui.PlayerView
import os.trendo.tv.media.MediaKind
import os.trendo.tv.media.ResumeStore

/**
 * Eigener Player für Filme und Musik (AndroidX Media3). Bedienung komplett per Fernbedienung:
 * OK = Pause/Weiter + Leiste, Links/Rechts = spulen, Zurück = beenden. Position wird gemerkt.
 */
class PlayerActivity : Activity() {
    private var player: ExoPlayer? = null
    private lateinit var resume: ResumeStore
    private lateinit var uri: String
    private lateinit var title: String
    private lateinit var kind: MediaKind

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        resume = ResumeStore(this)
        uri = intent.getStringExtra(EXTRA_URI) ?: return finish()
        title = intent.getStringExtra(EXTRA_TITLE) ?: "Video"
        kind = runCatching { MediaKind.valueOf(intent.getStringExtra(EXTRA_KIND) ?: "VIDEO") }.getOrDefault(MediaKind.VIDEO)

        val view = PlayerView(this).apply {
            useController = true
            controllerShowTimeoutMs = 3500
            setShowSubtitleButton(true)
            keepScreenOn = true
        }
        setContentView(view)
        val p = ExoPlayer.Builder(this)
            .setSeekBackIncrementMs(10_000).setSeekForwardIncrementMs(30_000)
            .build()
        p.setMediaItem(MediaItem.Builder().setUri(Uri.parse(uri)).setMediaId(uri).build())
        val start = resume.position(uri)
        if (start > 0) {
            p.seekTo(start)
            Toast.makeText(this, "Fortsetzen bei ${os.trendo.tv.media.ResumeRules.formatTime(start)}", Toast.LENGTH_SHORT).show()
        }
        p.addListener(object : Player.Listener {
            override fun onPlayerError(error: PlaybackException) {
                Toast.makeText(this@PlayerActivity, "Diese Datei kann nicht abgespielt werden.", Toast.LENGTH_LONG).show()
            }
            override fun onPlaybackStateChanged(state: Int) {
                if (state == Player.STATE_ENDED) { save(); finish() }
            }
        })
        p.playWhenReady = true
        p.prepare()
        view.player = p
        player = p
    }

    override fun dispatchKeyEvent(event: KeyEvent): Boolean {
        val p = player
        if (p != null && event.action == KeyEvent.ACTION_DOWN) {
            when (event.keyCode) {
                KeyEvent.KEYCODE_MEDIA_PLAY_PAUSE -> { p.playWhenReady = !p.playWhenReady; return true }
                KeyEvent.KEYCODE_MEDIA_FAST_FORWARD -> { p.seekForward(); return true }
                KeyEvent.KEYCODE_MEDIA_REWIND -> { p.seekBack(); return true }
            }
        }
        return super.dispatchKeyEvent(event)
    }

    private fun save() {
        val p = player ?: return
        resume.save(uri, title, kind, p.currentPosition, p.duration.coerceAtLeast(0))
    }

    override fun onPause() { super.onPause(); save(); player?.pause() }

    override fun onDestroy() {
        super.onDestroy()
        player?.release(); player = null
    }

    companion object {
        const val EXTRA_URI = "uri"
        const val EXTRA_TITLE = "title"
        const val EXTRA_KIND = "kind"

        fun intent(ctx: Context, uri: String, title: String, kind: MediaKind) =
            Intent(ctx, PlayerActivity::class.java).putExtra(EXTRA_URI, uri).putExtra(EXTRA_TITLE, title).putExtra(EXTRA_KIND, kind.name)
    }
}
