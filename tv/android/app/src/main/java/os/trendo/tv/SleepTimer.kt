package os.trendo.tv

import android.app.AlarmManager
import android.app.PendingIntent
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.media.AudioManager
import android.os.Build
import android.os.SystemClock
import android.view.KeyEvent

/**
 * Einschlaf-Timer: Nach Ablauf wird die laufende Wiedergabe angehalten (Medien-Pause-Taste an
 * jede App, auch TiviMate) und der Bildschirmschoner gestartet. Den Fernseher ausschalten darf eine
 * App nicht – dafür hat der TV seinen eigenen Ausschalt-Timer.
 */
object SleepTimer {
    private const val PREFS = "trendo_tv_timer"

    fun start(ctx: Context, minutes: Int) {
        val am = ctx.getSystemService(AlarmManager::class.java)
        val at = SystemClock.elapsedRealtime() + minutes * 60_000L
        if (Build.VERSION.SDK_INT >= 31 && !am.canScheduleExactAlarms())
            am.setAndAllowWhileIdle(AlarmManager.ELAPSED_REALTIME_WAKEUP, at, pending(ctx))
        else am.setExactAndAllowWhileIdle(AlarmManager.ELAPSED_REALTIME_WAKEUP, at, pending(ctx))
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit()
            .putLong("end_wall", System.currentTimeMillis() + minutes * 60_000L).apply()
    }

    fun cancel(ctx: Context) {
        ctx.getSystemService(AlarmManager::class.java).cancel(pending(ctx))
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().remove("end_wall").apply()
    }

    /** Verbleibende Minuten oder null, wenn kein Timer läuft. */
    fun remainingMinutes(ctx: Context): Int? {
        val end = ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getLong("end_wall", 0)
        val left = end - System.currentTimeMillis()
        return if (end == 0L || left <= 0) null else ((left + 59_999) / 60_000).toInt()
    }

    private fun pending(ctx: Context) = PendingIntent.getBroadcast(
        ctx, 7, Intent(ctx, SleepTimerReceiver::class.java), PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
    )

    internal fun fire(ctx: Context) {
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().remove("end_wall").apply()
        val audio = ctx.getSystemService(AudioManager::class.java)
        audio.dispatchMediaKeyEvent(KeyEvent(KeyEvent.ACTION_DOWN, KeyEvent.KEYCODE_MEDIA_PAUSE))
        audio.dispatchMediaKeyEvent(KeyEvent(KeyEvent.ACTION_UP, KeyEvent.KEYCODE_MEDIA_PAUSE))
        // Trendo zeigen; von dort startet der Bildschirmschoner.
        ctx.startActivity(Intent(ctx, MainActivity::class.java).putExtra(MainActivity.EXTRA_TIMER_DONE, true)
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_REORDER_TO_FRONT))
    }
}

class SleepTimerReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent?) = SleepTimer.fire(context)
}
