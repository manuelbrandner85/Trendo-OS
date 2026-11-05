package os.trendo.tv.export

import android.content.ContentProvider
import android.content.ContentValues
import android.database.Cursor
import android.database.MatrixCursor
import android.net.Uri
import os.trendo.tv.livetv.LiveStore

/**
 * Übergibt die Live-TV-Quellen einmalig an die App „Trendo Live TV".
 * Nur lesbar mit der Berechtigung LIVE_EXPORT (protectionLevel=signature): also ausschließlich
 * für Apps, die mit demselben Trendo-Schlüssel signiert sind. Keine andere App kommt an die Daten.
 */
class LiveExportProvider : ContentProvider() {
    override fun onCreate() = true

    override fun query(uri: Uri, projection: Array<out String>?, selection: String?, args: Array<out String>?, sort: String?): Cursor? {
        val ctx = context ?: return null
        val store = LiveStore(ctx)
        return when (uri.lastPathSegment) {
            "sources" -> MatrixCursor(arrayOf("id", "name", "kind", "url", "user", "epg", "secret")).apply {
                store.sources().forEach { s -> addRow(arrayOf(s.id, s.name, s.kind.name, s.url, s.user ?: "", s.epgUrl ?: "", store.secrets.get(s.id) ?: "")) }
            }
            "settings" -> MatrixCursor(arrayOf("settings", "pin", "salt")).apply {
                val pin = store.pinRaw()
                addRow(arrayOf(store.settingsJson(), pin?.first ?: "", pin?.second ?: ""))
            }
            else -> null
        }
    }

    override fun getType(uri: Uri): String? = null
    override fun insert(uri: Uri, values: ContentValues?): Uri? = null
    override fun delete(uri: Uri, selection: String?, args: Array<out String>?) = 0
    override fun update(uri: Uri, values: ContentValues?, selection: String?, args: Array<out String>?) = 0
}
