package os.trendo.tv

import android.app.AlarmManager
import android.app.PendingIntent
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.widget.Toast
import os.trendo.tv.livetv.Reminder

/** Erinnerung an eine Sendung: 2 Minuten vor Beginn ein Hinweis auf dem Bildschirm (über jeder App). */
object Reminders {
    private const val LEAD_MS = 2 * 60_000L

    private fun pending(ctx: Context, r: Reminder) = PendingIntent.getBroadcast(
        ctx, (r.channelId + r.start).hashCode(),
        Intent(ctx, ReminderReceiver::class.java).putExtra("t", r.title).putExtra("c", r.channelName),
        PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
    )

    fun schedule(ctx: Context, r: Reminder) {
        val at = r.start - LEAD_MS
        if (at <= System.currentTimeMillis()) return
        ctx.getSystemService(AlarmManager::class.java).setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, at, pending(ctx, r))
    }

    fun cancel(ctx: Context, r: Reminder) = ctx.getSystemService(AlarmManager::class.java).cancel(pending(ctx, r))
}

class ReminderReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val t = intent.getStringExtra("t") ?: return
        val c = intent.getStringExtra("c") ?: ""
        Toast.makeText(context, "Gleich: „$t“ auf $c – in Trendo unter Live TV", Toast.LENGTH_LONG).show()
    }
}
