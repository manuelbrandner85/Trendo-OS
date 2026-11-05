package os.trendo.tv.media

/** Eine eigene Mediendatei (USB, interner Speicher) – nie Daten fremder Apps. */
data class MediaFile(
    val id: Long,
    val uri: String,
    val title: String,
    val kind: MediaKind,
    val durationMs: Long,
    val sizeBytes: Long,
    val addedSec: Long,
    val folder: String,
)

enum class MediaKind { VIDEO, AUDIO, IMAGE }

/** Regeln für „Weitersehen": wann eine Position gemerkt, fortgesetzt oder vergessen wird. */
object ResumeRules {
    /** Unter 30 s oder in den letzten 5 % gilt nicht als angefangen bzw. als fertig. */
    fun shouldRemember(positionMs: Long, durationMs: Long): Boolean {
        if (durationMs <= 0) return positionMs > 30_000
        return positionMs > 30_000 && positionMs < durationMs * 95 / 100
    }

    fun progress(positionMs: Long, durationMs: Long): Float =
        if (durationMs <= 0) 0f else (positionMs.toFloat() / durationMs).coerceIn(0f, 1f)

    fun formatTime(ms: Long): String {
        val s = (ms / 1000).coerceAtLeast(0)
        val h = s / 3600; val m = (s % 3600) / 60; val sec = s % 60
        return if (h > 0) "%d:%02d:%02d".format(h, m, sec) else "%d:%02d".format(m, sec)
    }

    fun formatSize(bytes: Long): String = when {
        bytes >= 1L shl 30 -> String.format(java.util.Locale.GERMANY, "%.1f GB", bytes / (1L shl 30).toDouble())
        bytes >= 1L shl 20 -> String.format(java.util.Locale.GERMANY, "%.0f MB", bytes / (1L shl 20).toDouble())
        else -> "${bytes / 1024} KB"
    }
}
