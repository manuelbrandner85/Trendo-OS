package os.trendo.tv

import android.app.AlarmManager
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.Environment
import android.os.IBinder
import android.util.Log
import org.json.JSONArray
import org.json.JSONObject
import os.trendo.tv.livetv.Channel
import os.trendo.tv.livetv.Programme
import java.io.File
import java.net.HttpURLConnection
import java.net.URL
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/** Eine Aufnahme (geplant, läuft, fertig, Fehler). Datei liegt auf dem USB-Stick. */
data class Rec(val id: String, val channelName: String, val title: String, val url: String, val start: Long, val stop: Long,
               val state: String = "geplant", val file: String? = null, val bytes: Long = 0)

/** Aufnahmen und Aufnahme-Regeln – gespeichert im privaten Speicher, Dateien auf USB. */
object Recordings {
    private const val PREFS = "trendo_live_rec"

    /** Ordner auf einem Wechseldatenträger (USB-Stick) – ohne Zusatzrechte (app-eigener Ordner). */
    fun usbDir(ctx: Context): File? = ctx.getExternalFilesDirs(Environment.DIRECTORY_MOVIES).filterNotNull()
        .firstOrNull { runCatching { Environment.isExternalStorageRemovable(it) }.getOrDefault(false) }
        ?.also { it.mkdirs() }

    fun list(ctx: Context): List<Rec> = runCatching {
        val a = JSONArray(ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getString("list", "[]"))
        (0 until a.length()).map { i -> a.getJSONObject(i).let { Rec(it.getString("id"), it.getString("ch"), it.getString("t"), it.getString("u"),
            it.getLong("s"), it.getLong("e"), it.optString("st", "geplant"), it.optString("f").ifBlank { null }, it.optLong("b")) } }
    }.getOrDefault(emptyList())

    @Synchronized fun save(ctx: Context, list: List<Rec>) {
        val a = JSONArray()
        list.forEach { r -> a.put(JSONObject().put("id", r.id).put("ch", r.channelName).put("t", r.title).put("u", r.url).put("s", r.start).put("e", r.stop)
            .put("st", r.state).put("f", r.file ?: "").put("b", r.bytes)) }
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().putString("list", a.toString()).apply()
    }

    @Synchronized fun update(ctx: Context, id: String, f: (Rec) -> Rec) = save(ctx, list(ctx).map { if (it.id == id) f(it) else it })

    fun rules(ctx: Context): List<String> = ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getString("rules", "")!!.split('\n').filter { it.isNotBlank() }
    fun setRules(ctx: Context, r: List<String>) = ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().putString("rules", r.distinct().joinToString("\n")).apply()

    /** Sendung zur Aufnahme vormerken (1 min Vorlauf, 3 min Nachlauf). */
    fun schedule(ctx: Context, c: Channel, p: Programme): Rec? {
        if (list(ctx).any { it.url == c.url && it.start == p.start - 60_000 }) return null
        val r = Rec("r" + System.currentTimeMillis().toString(36) + (p.start % 1000), c.name, p.title, c.url, p.start - 60_000, p.stop + 180_000)
        save(ctx, list(ctx) + r); arm(ctx, r); return r
    }

    /** Sofort aufnehmen bis [stop]. */
    fun now(ctx: Context, c: Channel, title: String, stop: Long): Rec {
        val r = Rec("r" + System.currentTimeMillis().toString(36), c.name, title, c.url, System.currentTimeMillis(), stop)
        save(ctx, list(ctx) + r); startService(ctx, r.id); return r
    }

    fun cancel(ctx: Context, id: String) {
        ctx.getSystemService(AlarmManager::class.java).cancel(pending(ctx, id))
        update(ctx, id) { if (it.state == "läuft") it.copy(state = "abgebrochen") else it.copy(state = "gelöscht") }
        save(ctx, list(ctx).filter { it.state != "gelöscht" })
        RecordingService.stopFlags += id
    }

    fun delete(ctx: Context, r: Rec) { r.file?.let { File(it).delete() }; save(ctx, list(ctx).filter { it.id != r.id }) }

    /** Regeln anwenden: passende Sendungen der nächsten 24 h einplanen. */
    fun applyRules(ctx: Context, channels: List<Channel>, epg: Map<String, List<Programme>>, now: Long): Int {
        val rules = rules(ctx).map { it.lowercase() }
        if (rules.isEmpty()) return 0
        var n = 0
        for (c in channels) for (p in c.epgId?.let { epg[it] } ?: continue) {
            if (p.start > now && p.start < now + 86_400_000L && rules.any { p.title.lowercase().contains(it) } && schedule(ctx, c, p) != null) n++
        }
        return n
    }

    private fun pending(ctx: Context, id: String) = PendingIntent.getBroadcast(ctx, id.hashCode(),
        Intent(ctx, RecordingAlarm::class.java).putExtra("id", id), PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)

    private fun arm(ctx: Context, r: Rec) {
        val am = ctx.getSystemService(AlarmManager::class.java)
        if (Build.VERSION.SDK_INT >= 31 && !am.canScheduleExactAlarms()) am.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, r.start, pending(ctx, r.id))
        else am.setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, r.start, pending(ctx, r.id))
    }

    internal fun startService(ctx: Context, id: String) {
        val i = Intent(ctx, RecordingService::class.java).putExtra("id", id)
        if (Build.VERSION.SDK_INT >= 26) ctx.startForegroundService(i) else ctx.startService(i)
    }
}

class RecordingAlarm : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) { intent.getStringExtra("id")?.let { Recordings.startService(context, it) } }
}

/** Nimmt den Stream auf den USB-Stick auf (Vordergrund-Dienst mit Hinweis, Neuverbindung bei Abbruch). */
class RecordingService : Service() {
    companion object { val stopFlags = java.util.concurrent.ConcurrentHashMap.newKeySet<String>() }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        val id = intent?.getStringExtra("id") ?: return START_NOT_STICKY
        val nm = getSystemService(NotificationManager::class.java)
        if (Build.VERSION.SDK_INT >= 26) nm.createNotificationChannel(NotificationChannel("rec", "Aufnahmen", NotificationManager.IMPORTANCE_LOW))
        val rec = Recordings.list(this).firstOrNull { it.id == id } ?: return START_NOT_STICKY
        val n = (if (Build.VERSION.SDK_INT >= 26) Notification.Builder(this, "rec") else Notification.Builder(this))
            .setContentTitle("⏺ Aufnahme läuft").setContentText("${rec.title} · ${rec.channelName}").setSmallIcon(android.R.drawable.presence_video_online).build()
        if (Build.VERSION.SDK_INT >= 29) startForeground(id.hashCode(), n, android.content.pm.ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC) else startForeground(id.hashCode(), n)
        Thread { record(rec); stopForeground(STOP_FOREGROUND_REMOVE); stopSelf(startId) }.start()
        return START_NOT_STICKY
    }

    private fun record(r: Rec) {
        val dir = Recordings.usbDir(this)
        if (dir == null) { Recordings.update(this, r.id) { it.copy(state = "Fehler: kein USB-Stick") }; return }
        val stamp = SimpleDateFormat("yyyyMMdd-HHmm", Locale.GERMANY).format(Date(r.start))
        val safe = (r.title + "_" + r.channelName).replace(Regex("""[^\p{L}\p{N} _.-]"""), "").take(80).trim()
        val file = File(dir, "${safe}_$stamp.ts")
        Recordings.update(this, r.id) { it.copy(state = "läuft", file = file.absolutePath) }
        var total = 0L; var fails = 0
        file.outputStream().use { out ->
            while (System.currentTimeMillis() < r.stop && r.id !in stopFlags && fails < 20) {
                try {
                    val c = URL(r.url).openConnection() as HttpURLConnection
                    c.connectTimeout = 8000; c.readTimeout = 15000; c.setRequestProperty("User-Agent", "TrendoOS-TV")
                    c.inputStream.use { inp ->
                        val buf = ByteArray(64 * 1024)
                        while (System.currentTimeMillis() < r.stop && r.id !in stopFlags) {
                            val k = inp.read(buf); if (k < 0) break
                            out.write(buf, 0, k); total += k
                            if (total % (8L shl 20) < k) Recordings.update(this, r.id) { it.copy(bytes = total) }
                        }
                    }
                    c.disconnect()
                } catch (e: Exception) {
                    fails++; Log.w("TrendoRec", "Verbindung unterbrochen ($fails)"); Thread.sleep(2000)
                }
            }
        }
        val state = when { r.id in stopFlags -> "abgebrochen"; total == 0L -> "Fehler: keine Daten"; else -> "fertig" }
        stopFlags.remove(r.id)
        Recordings.update(this, r.id) { it.copy(state = state, bytes = total) }
    }
}
