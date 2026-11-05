package os.trendo.tv.apps

import android.app.ActivityManager
import android.content.ActivityNotFoundException
import android.content.Context
import android.content.Intent
import android.content.pm.ApplicationInfo
import android.content.pm.PackageManager
import android.media.tv.TvContract
import android.media.tv.TvInputInfo
import android.media.tv.TvInputManager
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.net.Uri
import android.os.Environment
import android.os.StatFs
import android.provider.Settings
import android.util.Log

/** Ein Bildeingang des Fernsehers (z. B. HDMI 1), wie Android ihn meldet. */
data class TvInput(val id: String, val label: String)

/** Echte Messwerte; null = vom Gerät nicht verfügbar. */
data class DeviceStats(val ramTotalMb: Long?, val ramFreeMb: Long?, val lowMemory: Boolean?, val storageFreeMb: Long?, val storageTotalMb: Long?)

/** Adapter zum Android-PackageManager. Liest nur Starteinträge, ändert nichts an fremden Apps. */
class AndroidApps(private val context: Context) {
    private val pm: PackageManager = context.packageManager
    private val own = context.packageName

    fun load(): List<AppEntry> {
        fun query(category: String, tv: Boolean): List<AppEntry> {
            val intent = Intent(Intent.ACTION_MAIN).addCategory(category)
            return pm.queryIntentActivities(intent, 0).map {
                AppEntry(it.activityInfo.packageName, it.loadLabel(pm).toString(), tv, kindOf(it.activityInfo.applicationInfo))
            }
        }
        return AppCatalog.merge(
            tv = query(Intent.CATEGORY_LEANBACK_LAUNCHER, true),
            phone = query(Intent.CATEGORY_LAUNCHER, false),
            ownPackage = own,
        )
    }

    private fun kindOf(info: ApplicationInfo): AppKind = when (info.category) {
        ApplicationInfo.CATEGORY_GAME -> AppKind.GAME
        ApplicationInfo.CATEGORY_VIDEO -> AppKind.VIDEO
        ApplicationInfo.CATEGORY_AUDIO -> AppKind.AUDIO
        ApplicationInfo.CATEGORY_IMAGE -> AppKind.IMAGE
        else -> if (info.flags and ApplicationInfo.FLAG_IS_GAME != 0) AppKind.GAME else AppKind.OTHER
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

    /** Öffnet eine Android-Einstellungsseite; fällt auf die allgemeinen Einstellungen zurück. */
    fun openSettings(action: String = Settings.ACTION_SETTINGS): Boolean =
        start(Intent(action).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)) ||
            (action != Settings.ACTION_SETTINGS && start(Intent(Settings.ACTION_SETTINGS).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)))

    fun openTvSettings(): Boolean = openSettings()

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

    /** HDMI- und andere Durchschleif-Eingänge; leer, wenn das Gerät keine meldet. */
    fun hdmiInputs(): List<TvInput> = runCatching {
        val tim = context.getSystemService(Context.TV_INPUT_SERVICE) as? TvInputManager ?: return emptyList()
        tim.tvInputList.filter { it.isPassthroughInput }.map {
            val label = it.loadLabel(context).toString().ifBlank { if (it.type == TvInputInfo.TYPE_HDMI) "HDMI" else "Eingang" }
            TvInput(it.id, label)
        }.sortedWith(compareBy(String.CASE_INSENSITIVE_ORDER) { it.label })
    }.getOrDefault(emptyList())

    fun openInput(input: TvInput): Boolean =
        start(Intent(Intent.ACTION_VIEW, TvContract.buildChannelUriForPassthroughInput(input.id)).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))

    fun stats(): DeviceStats {
        val mi = ActivityManager.MemoryInfo()
        val am = context.getSystemService(Context.ACTIVITY_SERVICE) as? ActivityManager
        val ok = am != null && runCatching { am.getMemoryInfo(mi) }.isSuccess
        val fs = runCatching { StatFs(Environment.getDataDirectory().path) }.getOrNull()
        return DeviceStats(
            ramTotalMb = if (ok) mi.totalMem / 1_048_576 else null,
            ramFreeMb = if (ok) mi.availMem / 1_048_576 else null,
            lowMemory = if (ok) mi.lowMemory else null,
            storageFreeMb = fs?.availableBytes?.div(1_048_576),
            storageTotalMb = fs?.totalBytes?.div(1_048_576),
        )
    }

    fun online(): Boolean = runCatching {
        val cm = context.getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager
        cm.getNetworkCapabilities(cm.activeNetwork)?.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET) == true
    }.getOrDefault(false)

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
