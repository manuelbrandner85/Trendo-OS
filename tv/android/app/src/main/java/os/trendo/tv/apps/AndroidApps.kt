package os.trendo.tv.apps

import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.provider.Settings
import android.util.Log

/** Adapter zum Android-PackageManager. Liest nur Starteinträge, ändert nichts an fremden Apps. */
class AndroidApps(private val context: Context) {
    private val pm: PackageManager = context.packageManager
    private val own = context.packageName

    fun load(): List<AppEntry> {
        fun query(category: String, tv: Boolean): List<AppEntry> {
            val intent = Intent(Intent.ACTION_MAIN).addCategory(category)
            return pm.queryIntentActivities(intent, 0).map {
                AppEntry(it.activityInfo.packageName, it.loadLabel(pm).toString(), tv)
            }
        }
        return AppCatalog.merge(
            tv = query(Intent.CATEGORY_LEANBACK_LAUNCHER, true),
            phone = query(Intent.CATEGORY_LAUNCHER, false),
            ownPackage = own,
        )
    }

    /** Startet die App über ihren eigenen Original-Starteintrag (TV-Eintrag bevorzugt). */
    fun launch(packageName: String): Boolean {
        if (!AppGuard.isAllowed(AppAction.LAUNCH, packageName, own)) return false
        val intent = pm.getLeanbackLaunchIntentForPackage(packageName)
            ?: pm.getLaunchIntentForPackage(packageName)
            ?: return false
        return start(intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
    }

    fun openSystemInfo(packageName: String): Boolean {
        if (!AppGuard.isAllowed(AppAction.OPEN_SYSTEM_INFO, packageName, own)) return false
        return start(Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, Uri.parse("package:$packageName"))
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
    }

    fun openTvSettings(): Boolean = start(Intent(Settings.ACTION_SETTINGS).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))

    /** Öffnet das Original-TV-Menü. Solange Trendo nicht Standard-Home ist, ist das immer das System-Home. */
    fun openOriginalHome(): Boolean =
        start(Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_HOME).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))

    /**
     * „Alles rückgängig": öffnet den Android-Dialog zum Entfernen von Trendo SELBST.
     * Betrifft ausschließlich das eigene Paket; der Nutzer bestätigt am TV. Danach ist die
     * Original-Oberfläche wieder allein aktiv, alle anderen Apps bleiben unberührt.
     */
    fun uninstallSelf(): Boolean =
        start(Intent(Intent.ACTION_DELETE, Uri.parse("package:$own")).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))

    fun banner(packageName: String) = runCatching {
        pm.getApplicationBanner(packageName) ?: pm.getApplicationIcon(packageName)
    }.getOrNull()

    private fun start(intent: Intent): Boolean = try {
        context.startActivity(intent); true
    } catch (e: ActivityNotFoundException) {
        Log.w("TrendoTV", "Start nicht möglich: ${intent.action}"); false
    } catch (e: SecurityException) {
        Log.w("TrendoTV", "Start verweigert: ${intent.action}"); false
    }
}
