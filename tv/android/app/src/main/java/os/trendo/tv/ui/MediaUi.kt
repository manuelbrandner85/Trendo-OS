package os.trendo.tv.ui

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.net.Uri
import android.os.Build
import android.util.Size
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.focusable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Image
import androidx.compose.material.icons.outlined.Movie
import androidx.compose.material.icons.outlined.MusicNote
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.input.key.KeyEventType
import androidx.compose.ui.input.key.key
import androidx.compose.ui.input.key.onKeyEvent
import androidx.compose.ui.input.key.type
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.tv.material3.Icon
import androidx.tv.material3.Text
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import os.trendo.tv.media.MediaFile
import os.trendo.tv.media.MediaKind

/** Vorschaubild einer eigenen Mediendatei; klein geladen (Speicherbudget), sonst ein Symbol. */
@Composable
fun MediaThumb(f: MediaFile) {
    val ctx = LocalContext.current
    val bmp by produceState<ImageBitmap?>(null, f.uri) {
        value = withContext(Dispatchers.IO) { loadScaled(ctx, f.uri, 320, 180)?.asImageBitmap() }
    }
    val b = bmp
    if (b != null) Image(b, null, contentScale = ContentScale.Crop, modifier = Modifier.fillMaxSize())
    else Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Icon(when (f.kind) { MediaKind.VIDEO -> Icons.Outlined.Movie; MediaKind.AUDIO -> Icons.Outlined.MusicNote; MediaKind.IMAGE -> Icons.Outlined.Image },
            null, tint = TvColors.Cyan.copy(alpha = 0.7f), modifier = Modifier.size(28.dp))
    }
}

fun loadScaled(ctx: android.content.Context, uri: String, w: Int, h: Int): Bitmap? = runCatching {
    val u = Uri.parse(uri)
    if (Build.VERSION.SDK_INT >= 29) ctx.contentResolver.loadThumbnail(u, Size(w, h), null)
    else {
        val o = BitmapFactory.Options().apply { inJustDecodeBounds = true }
        ctx.contentResolver.openInputStream(u)?.use { BitmapFactory.decodeStream(it, null, o) }
        var s = 1; while (o.outWidth / (s * 2) >= w && o.outHeight / (s * 2) >= h) s *= 2
        ctx.contentResolver.openInputStream(u)?.use { BitmapFactory.decodeStream(it, null, BitmapFactory.Options().apply { inSampleSize = s }) }
    }
}.getOrNull()

/** Vollbild-Fotoanzeige: Links/Rechts blättern, Zurück schließt. */
@Composable
fun PhotoViewer(images: List<MediaFile>, index: Int, onIndex: (Int) -> Unit, onClose: () -> Unit) {
    val ctx = LocalContext.current
    val f = images.getOrNull(index) ?: return onClose()
    val bmp by produceState<ImageBitmap?>(null, f.uri) {
        value = withContext(Dispatchers.IO) { loadScaled(ctx, f.uri, 1920, 1080)?.asImageBitmap() }
    }
    val focus = remember { FocusRequester() }
    LaunchedEffect(Unit) { runCatching { focus.requestFocus() } }
    Box(
        Modifier.fillMaxSize().background(Color.Black).focusRequester(focus).focusable()
            .onKeyEvent { e ->
                if (e.type != KeyEventType.KeyDown) return@onKeyEvent false
                when (e.key) {
                    Key.DirectionRight -> { onIndex((index + 1) % images.size); true }
                    Key.DirectionLeft -> { onIndex((index - 1 + images.size) % images.size); true }
                    Key.Back, Key.Escape -> { onClose(); true }
                    else -> false
                }
            },
        contentAlignment = Alignment.Center,
    ) {
        bmp?.let { Image(it, f.title, contentScale = ContentScale.Fit, modifier = Modifier.fillMaxSize()) }
        Column(Modifier.align(Alignment.BottomStart).padding(20.dp)) {
            Text("${index + 1} / ${images.size} · ${f.title}", color = TvColors.Text.copy(alpha = 0.8f), fontSize = 11.sp)
        }
    }
}

/** Nach Ablauf des Einschlaf-Timers: dunkler Bildschirm; jede Taste weckt. */
@Composable
fun GoodNight(onWake: () -> Unit) {
    val focus = remember { FocusRequester() }
    LaunchedEffect(Unit) { runCatching { focus.requestFocus() } }
    Box(
        Modifier.fillMaxSize().background(Color.Black).focusRequester(focus).focusable()
            .onKeyEvent { e -> if (e.type == KeyEventType.KeyDown) { onWake(); true } else false },
        contentAlignment = Alignment.Center,
    ) {
        Text("Gute Nacht. Wiedergabe angehalten – beliebige Taste zum Aufwecken.", color = Color(0xFF3A4A66), fontSize = 12.sp)
    }
}
