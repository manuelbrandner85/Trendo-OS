package os.trendo.tv.ui

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.util.LruCache
import androidx.compose.foundation.Image
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.produceState
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.sync.Semaphore
import kotlinx.coroutines.sync.withPermit
import kotlinx.coroutines.withContext
import java.net.HttpURLConnection
import java.net.URL

/** Sparsamer Bildlader für Senderlogos und Cover: verkleinert, höchstens 12 MB im Speicher, 3 Downloads gleichzeitig. */
object NetImages {
    private val cache = object : LruCache<String, Bitmap>(12 * 1024 * 1024) {
        override fun sizeOf(key: String, value: Bitmap) = value.byteCount
    }
    private val gate = Semaphore(3)

    suspend fun load(url: String, maxW: Int, maxH: Int): Bitmap? {
        cache.get("$url@$maxW")?.let { return it }
        return gate.withPermit {
            withContext(Dispatchers.IO) {
                runCatching {
                    val bytes = (URL(url).openConnection() as HttpURLConnection).run {
                        connectTimeout = 6000; readTimeout = 8000; setRequestProperty("User-Agent", "TrendoOS-TV")
                        try { inputStream.use { it.readBytes() } } finally { disconnect() }
                    }
                    val o = BitmapFactory.Options().apply { inJustDecodeBounds = true }
                    BitmapFactory.decodeByteArray(bytes, 0, bytes.size, o)
                    var s = 1; while (o.outWidth / (s * 2) >= maxW && o.outHeight / (s * 2) >= maxH) s *= 2
                    BitmapFactory.decodeByteArray(bytes, 0, bytes.size, BitmapFactory.Options().apply { inSampleSize = s; inPreferredConfig = Bitmap.Config.RGB_565 })
                }.getOrNull()?.also { cache.put("$url@$maxW", it) }
            }
        }
    }
}

@Composable
fun NetImage(url: String?, modifier: Modifier, maxW: Int = 240, maxH: Int = 360, crop: Boolean = true) {
    if (url.isNullOrBlank()) return
    val img by produceState<ImageBitmap?>(null, url) { value = NetImages.load(url, maxW, maxH)?.asImageBitmap() }
    img?.let { Image(it, null, contentScale = if (crop) ContentScale.Crop else ContentScale.Fit, modifier = modifier) }
}
