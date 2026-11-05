package os.trendo.tv.live

import android.net.Uri
import android.os.Bundle
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.lifecycleScope
import androidx.tv.material3.Text
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import os.trendo.tv.PlayerActivity
import os.trendo.tv.livetv.LiveRepo
import os.trendo.tv.livetv.LiveStore
import os.trendo.tv.livetv.Source
import os.trendo.tv.livetv.SourceKind
import os.trendo.tv.media.MediaKind
import os.trendo.tv.shared.R
import os.trendo.tv.ui.LiveHost
import os.trendo.tv.ui.LiveTvPage
import os.trendo.tv.ui.ScriptFont
import os.trendo.tv.ui.TvColors
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/** „Trendo Live TV" – eigenständige Live-TV-App im Trendo-Stil. */
class LiveMainActivity : ComponentActivity() {
    private var version by mutableStateOf(0)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val store = LiveStore(this)
        // Übernehmen, wenn noch nichts da ist oder ein Zugang sein Passwort verloren hat.
        val needImport = store.sources().isEmpty() || store.sources().any { it.kind == SourceKind.XTREAM && store.secrets.get(it.id) == null }
        if (needImport) lifecycleScope.launch {
            val n = withContext(Dispatchers.IO) { importFromTrendo(store) }
            if (n > 0) {
                Toast.makeText(this@LiveMainActivity, "$n Quelle(n) aus TRENDO OS übernommen – lade Sender …", Toast.LENGTH_LONG).show()
                withContext(Dispatchers.IO) { LiveRepo(store).refresh() }
                version++
            }
        }
        // „Direkt ins Live-TV": letzten Sender sofort zeigen (Einstellung unter Jugendschutz & Filter).
        if (savedInstanceState == null && store.autoplayLast) lifecycleScope.launch {
            val (list, i) = withContext(Dispatchers.IO) {
                val s = store.settings()
                val vis = os.trendo.tv.livetv.TvRules.visible(store.channels(), s)
                vis to vis.indexOfFirst { it.id == s.lastChannel }
            }
            if (i >= 0) {
                os.trendo.tv.LiveSession.channels = list; os.trendo.tv.LiveSession.allChannels = list
                os.trendo.tv.LiveSession.epg = withContext(Dispatchers.IO) { store.epg() }
                os.trendo.tv.LiveSession.locked = store.settings().lockedGroups
                startActivity(os.trendo.tv.LiveActivity.intent(this@LiveMainActivity, i))
            }
        }
        setContent {
            Box(Modifier.fillMaxSize().background(TvColors.Background)) {
                Box(Modifier.fillMaxSize().border(2.dp, Brush.sweepGradient(listOf(TvColors.Cyan, TvColors.ElectricBlue, TvColors.Violet, TvColors.Magenta, TvColors.Cyan)), RoundedCornerShape(0.dp)))
                Column(Modifier.fillMaxSize().padding(start = 28.dp, end = 28.dp, top = 14.dp, bottom = 10.dp)) {
                    Header()
                    Spacer(Modifier.height(8.dp))
                    // version erzwingt Neuladen nach der Übernahme
                    androidx.compose.runtime.key(version) {
                        LiveTvPage(LiveHost(playVod = { u, t -> startActivity(PlayerActivity.intent(this@LiveMainActivity, u, t, MediaKind.VIDEO)) }), "standard")
                    }
                }
            }
        }
    }

    /** Holt Quellen + Einstellungen aus TRENDO OS (nur möglich, weil beide Apps gleich signiert sind). */
    private fun importFromTrendo(store: LiveStore): Int = runCatching {
        val base = "content://os.trendo.tv.liveexport/"
        val list = ArrayList<Source>()
        contentResolver.query(Uri.parse(base + "sources"), null, null, null, null)?.use { c ->
            while (c.moveToNext()) {
                val s = Source(c.getString(0), c.getString(1), SourceKind.valueOf(c.getString(2)), c.getString(3), c.getString(4).ifBlank { null }, c.getString(5).ifBlank { null })
                // Doppelte (gleiche Art/Adresse/Benutzer oder gleiche Kennung) nur einmal übernehmen.
                if (list.any { (it.kind == s.kind && it.url == s.url && it.user == s.user) || it.id == s.id }) continue
                list += s
                c.getString(6).takeIf { it.isNotBlank() }?.let { store.secrets.put(s.id, it) }
            }
        }
        if (list.isNotEmpty()) store.saveSources(list)
        contentResolver.query(Uri.parse(base + "settings"), null, null, null, null)?.use { c ->
            if (c.moveToFirst()) {
                store.save(store.settingsFromJson(c.getString(0)))
                if (c.getString(1).isNotBlank()) store.importPin(c.getString(1), c.getString(2))
            }
        }
        list.size
    }.getOrDefault(0)
}

@androidx.compose.runtime.Composable
private fun Header() {
    var now by remember { mutableStateOf(Date()) }
    LaunchedEffect(Unit) { while (true) { now = Date(); delay(60_000L - System.currentTimeMillis() % 60_000L) } }
    Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
        Image(painterResource(R.drawable.ic_trendo_mark), null, Modifier.size(40.dp))
        Spacer(Modifier.width(8.dp))
        Column {
            Text("TRENDO", color = TvColors.Text, fontSize = 19.sp, fontWeight = FontWeight.Bold, letterSpacing = 1.5.sp)
            Text("L I V E   T V", color = TvColors.Cyan, fontSize = 8.sp, letterSpacing = 2.sp)
        }
        Spacer(Modifier.width(10.dp))
        Text("by Manuel", color = TvColors.Text, fontSize = 19.sp, fontFamily = ScriptFont)
        Spacer(Modifier.weight(1f))
        Column(horizontalAlignment = Alignment.End) {
            Text(SimpleDateFormat("HH:mm", Locale.GERMANY).format(now), color = TvColors.Text, fontSize = 22.sp)
            Text(SimpleDateFormat("EE d. MMMM", Locale.GERMANY).format(now), color = TvColors.TextSecondary, fontSize = 8.5.sp)
        }
    }
}
