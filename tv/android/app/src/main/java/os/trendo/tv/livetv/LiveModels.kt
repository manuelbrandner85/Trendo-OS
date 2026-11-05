package os.trendo.tv.livetv

/** Datenmodell wie in Trendo OS für Windows (`Trendo.Core.Tv`), für Android neu geschrieben. */
data class Channel(
    val id: String,
    val name: String,
    val url: String,
    val group: String,
    val logo: String? = null,
    val epgId: String? = null,
    val number: Int = 0,
    val sourceId: String = "",
)

data class Programme(val channel: String, val start: Long, val stop: Long, val title: String, val description: String = "", val category: String = "") {
    fun isOn(now: Long) = now in start until stop
    fun progress(now: Long): Float = if (stop <= start) 0f else ((now - start).toFloat() / (stop - start)).coerceIn(0f, 1f)
}

enum class SourceKind { M3U, XTREAM }

/** Eine Senderquelle. Zugangsdaten (Passwort) liegen getrennt und verschlüsselt im [SecretBox]. */
data class Source(val id: String, val name: String, val kind: SourceKind, val url: String, val user: String? = null, val epgUrl: String? = null)

data class Vod(val id: String, val name: String, val category: String, val url: String, val cover: String? = null, val plot: String? = null,
               val year: String? = null, val rating: String? = null, val sourceId: String = "")

data class Series(val id: String, val name: String, val category: String, val cover: String? = null, val plot: String? = null,
                  val year: String? = null, val rating: String? = null, val sourceId: String = "")

data class Episode(val seriesId: String, val season: Int, val episode: Int, val title: String, val url: String, val plot: String? = null, val seconds: Int = 0)

data class Reminder(val channelId: String, val channelName: String, val title: String, val start: Long)

data class Recording(val id: String, val channelId: String, val channelName: String, val title: String, val start: Long, val stop: Long,
                     val state: String = "geplant", val file: String? = null)

/** Einstellungen von Trendo Live TV (je Profil). */
data class LiveSettings(
    val favorites: Set<String> = emptySet(),
    val hiddenGroups: Set<String> = emptySet(),
    val lockedGroups: Set<String> = emptySet(),
    val onlyGerman: Boolean = false,
    val showAdult: Boolean = false,
    val kidsMode: Boolean = false,
    val mergeDuplicates: Boolean = true,
    val lastChannel: String? = null,
)
