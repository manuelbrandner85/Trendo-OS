package os.trendo.tv.livetv

import android.util.JsonReader
import android.util.JsonToken
import org.json.JSONArray
import java.io.InputStream
import java.io.InputStreamReader
import java.net.URLEncoder

/**
 * Xtream-Filme und -Serien gestreamt lesen: große Listen (zehntausende Titel) passen so auch auf
 * Fernseher mit wenig Arbeitsspeicher, weil nie die ganze Antwort auf einmal im Speicher liegt.
 */
object XtreamStream {
    private fun e(s: String) = URLEncoder.encode(s, "UTF-8")

    private fun categories(json: String): Map<String, String> = runCatching {
        val a = JSONArray(json); (0 until a.length()).associate { a.getJSONObject(it).let { o -> o.optString("category_id") to o.optString("category_name") } }
    }.getOrDefault(emptyMap())

    /** Liest ein Array von Objekten; je Objekt nur die genannten Felder als Text. */
    private fun each(input: InputStream, fields: Set<String>, block: (Map<String, String>) -> Unit) {
        JsonReader(InputStreamReader(input, Charsets.UTF_8)).use { r ->
            if (r.peek() != JsonToken.BEGIN_ARRAY) return
            r.beginArray()
            while (r.hasNext()) {
                if (r.peek() != JsonToken.BEGIN_OBJECT) { r.skipValue(); continue }
                val m = HashMap<String, String>(12)
                r.beginObject()
                while (r.hasNext()) {
                    val name = r.nextName()
                    if (name in fields && r.peek() in setOf(JsonToken.STRING, JsonToken.NUMBER, JsonToken.BOOLEAN)) {
                        m[name] = when (r.peek()) { JsonToken.BOOLEAN -> r.nextBoolean().toString(); else -> r.nextString() }
                    } else r.skipValue()
                }
                r.endObject()
                block(m)
            }
            r.endArray()
        }
    }

    fun vod(input: InputStream, catsJson: String, server: String, user: String, pass: String, sourceId: String): List<Vod> {
        val cats = categories(catsJson); val s = Xtream.normalize(server); val u = e(user); val p = e(pass)
        val out = ArrayList<Vod>()
        each(input, setOf("stream_id", "name", "category_id", "container_extension", "stream_icon", "plot", "year", "releasedate", "added", "rating")) { v ->
            val id = v["stream_id"].orEmpty(); if (id.isEmpty()) return@each
            val ext = v["container_extension"].orEmpty().ifEmpty { "mp4" }
            out += Vod("$sourceId:v$id", v["name"].orEmpty(), cats[v["category_id"]] ?: "Filme", "$s/movie/$u/$p/$id.$ext",
                v["stream_icon"]?.ifBlank { null }, v["plot"]?.ifBlank { null }?.take(400),
                (v["year"]?.ifBlank { null } ?: (v["releasedate"]?.ifBlank { null } ?: v["added"])?.split('-')?.firstOrNull())?.take(4), v["rating"]?.ifBlank { null }, sourceId)
        }
        return out
    }

    fun series(input: InputStream, catsJson: String, sourceId: String): List<Series> {
        val cats = categories(catsJson)
        val out = ArrayList<Series>()
        each(input, setOf("series_id", "name", "category_id", "cover", "plot", "releaseDate", "release_date", "rating")) { v ->
            val id = v["series_id"].orEmpty(); if (id.isEmpty()) return@each
            out += Series("$sourceId:s$id", v["name"].orEmpty(), cats[v["category_id"]] ?: "Serien", v["cover"]?.ifBlank { null }, v["plot"]?.ifBlank { null }?.take(400),
                (v["releaseDate"] ?: v["release_date"])?.ifBlank { null }?.split('-')?.firstOrNull(), v["rating"]?.ifBlank { null }, sourceId)
        }
        return out
    }
}
