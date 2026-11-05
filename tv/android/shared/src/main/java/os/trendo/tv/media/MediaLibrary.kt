package os.trendo.tv.media

import android.Manifest
import android.content.ContentUris
import android.content.Context
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.provider.MediaStore
import androidx.core.content.ContextCompat

/**
 * Eigene Medien über den Android-Medienindex (interner Speicher und USB-Sticks).
 * Liest nur Dateien, die Android allen Medien-Apps zeigt – keine privaten Daten anderer Apps.
 */
class MediaLibrary(private val context: Context) {

    val permission: String =
        if (Build.VERSION.SDK_INT >= 33) Manifest.permission.READ_MEDIA_VIDEO else Manifest.permission.READ_EXTERNAL_STORAGE

    fun hasPermission(): Boolean {
        val perms = if (Build.VERSION.SDK_INT >= 33)
            listOf(Manifest.permission.READ_MEDIA_VIDEO, Manifest.permission.READ_MEDIA_AUDIO, Manifest.permission.READ_MEDIA_IMAGES)
        else listOf(Manifest.permission.READ_EXTERNAL_STORAGE)
        return perms.all { ContextCompat.checkSelfPermission(context, it) == PackageManager.PERMISSION_GRANTED }
    }

    fun permissions(): Array<String> = if (Build.VERSION.SDK_INT >= 33)
        arrayOf(Manifest.permission.READ_MEDIA_VIDEO, Manifest.permission.READ_MEDIA_AUDIO, Manifest.permission.READ_MEDIA_IMAGES)
    else arrayOf(Manifest.permission.READ_EXTERNAL_STORAGE)

    fun load(kind: MediaKind, limit: Int = 300): List<MediaFile> {
        if (!hasPermission()) return emptyList()
        val volumes = if (Build.VERSION.SDK_INT >= 29) MediaStore.getExternalVolumeNames(context).toList() else listOf("external")
        val out = ArrayList<MediaFile>()
        for (v in volumes) {
            val base: Uri = when (kind) {
                MediaKind.VIDEO -> MediaStore.Video.Media.getContentUri(v)
                MediaKind.AUDIO -> MediaStore.Audio.Media.getContentUri(v)
                MediaKind.IMAGE -> MediaStore.Images.Media.getContentUri(v)
            }
            val cols = mutableListOf(
                MediaStore.MediaColumns._ID, MediaStore.MediaColumns.DISPLAY_NAME, MediaStore.MediaColumns.SIZE,
                MediaStore.MediaColumns.DATE_ADDED,
            )
            if (kind != MediaKind.IMAGE) cols += MediaStore.MediaColumns.DURATION
            if (Build.VERSION.SDK_INT >= 29) cols += MediaStore.MediaColumns.RELATIVE_PATH
            runCatching {
                context.contentResolver.query(base, cols.toTypedArray(), null, null, "${MediaStore.MediaColumns.DATE_ADDED} DESC")?.use { c ->
                    val id = c.getColumnIndexOrThrow(MediaStore.MediaColumns._ID)
                    val name = c.getColumnIndexOrThrow(MediaStore.MediaColumns.DISPLAY_NAME)
                    val size = c.getColumnIndexOrThrow(MediaStore.MediaColumns.SIZE)
                    val added = c.getColumnIndexOrThrow(MediaStore.MediaColumns.DATE_ADDED)
                    val dur = c.getColumnIndex(MediaStore.MediaColumns.DURATION)
                    val rel = c.getColumnIndex(MediaStore.MediaColumns.RELATIVE_PATH)
                    while (c.moveToNext() && out.size < limit) {
                        val rowId = c.getLong(id)
                        out += MediaFile(
                            id = rowId,
                            uri = ContentUris.withAppendedId(base, rowId).toString(),
                            title = (c.getString(name) ?: "Datei").substringBeforeLast('.'),
                            kind = kind,
                            durationMs = if (dur >= 0) c.getLong(dur) else 0,
                            sizeBytes = c.getLong(size),
                            addedSec = c.getLong(added),
                            folder = if (rel >= 0) (c.getString(rel) ?: "").trimEnd('/').substringAfterLast('/') else "",
                        )
                    }
                }
            }
        }
        return out.sortedByDescending { it.addedSec }
    }
}

/** Merkt Wiedergabepositionen eigener Medien („Weitersehen"). Nur im privaten Trendo-Speicher. */
class ResumeStore(context: Context) {
    private val prefs = context.getSharedPreferences("trendo_tv_resume", Context.MODE_PRIVATE)

    fun position(uri: String): Long = prefs.getLong("p:$uri", 0)

    fun save(uri: String, title: String, kind: MediaKind, positionMs: Long, durationMs: Long) {
        val e = prefs.edit()
        if (ResumeRules.shouldRemember(positionMs, durationMs)) {
            e.putLong("p:$uri", positionMs).putLong("d:$uri", durationMs).putString("t:$uri", title)
                .putString("k:$uri", kind.name).putLong("w:$uri", System.currentTimeMillis())
        } else {
            e.remove("p:$uri").remove("d:$uri").remove("t:$uri").remove("k:$uri").remove("w:$uri")
        }
        e.apply()
    }

    data class Entry(val uri: String, val title: String, val kind: MediaKind, val positionMs: Long, val durationMs: Long, val when_: Long)

    /** Zuletzt angefangene Medien, neueste zuerst. */
    fun recent(max: Int = 12): List<Entry> = prefs.all.keys.filter { it.startsWith("p:") }.map { it.removePrefix("p:") }.mapNotNull { u ->
        val kind = runCatching { MediaKind.valueOf(prefs.getString("k:$u", "VIDEO")!!) }.getOrDefault(MediaKind.VIDEO)
        Entry(u, prefs.getString("t:$u", "Video") ?: "Video", kind, prefs.getLong("p:$u", 0), prefs.getLong("d:$u", 0), prefs.getLong("w:$u", 0))
    }.sortedByDescending { it.when_ }.take(max)
}
