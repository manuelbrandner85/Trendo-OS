package os.trendo.tv.apps

import android.app.ActivityManager
import android.app.role.RoleManager
import android.content.ActivityNotFoundException
import android.content.ComponentName
import android.os.Build
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
                val info = it.activityInfo.applicationInfo
                AppEntry(it.activityInfo.packageName, it.loadLabel(pm).toString(), tv, kindOf(info), info.flags and ApplicationInfo.FLAG_SYSTEM != 0)
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
    fun openSettings(action: String = Settings.ACTION_SETTINGS): Boolean {
        // Manche Hersteller ersetzen Android-Einträge durch leere Platzhalter oder lassen sie weg.
        val intent = Intent(action)
        val target = pm.resolveActivity(intent, PackageManager.MATCH_DEFAULT_ONLY)?.activityInfo?.packageName
        if (target != null && !target.contains("frameworkpackagestubs") && start(intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))) return true
        if (action == Settings.ACTION_DREAM_SETTINGS && openScreensaverSettings()) return true
        if (action == Settings.ACTION_ACCESSIBILITY_SETTINGS && openAccessibilitySettings()) return true
        return action != Settings.ACTION_SETTINGS && start(Intent(Settings.ACTION_SETTINGS).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
    }

    /** Bildschirmschoner-Auswahl; bei Android TV liegt sie in den TV-Einstellungen (Daydream). */
    fun openScreensaverSettings(): Boolean = listOf(
        ComponentName("com.android.tv.settings", "com.android.tv.settings.device.display.daydream.DaydreamActivity"),
    ).any { c -> runCatching { pm.getActivityInfo(c, 0) }.isSuccess && start(Intent().setComponent(c).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)) }

    /** Ist der TRENDO-OS-Bildschirmschoner ausgewählt? */
    fun isOwnScreensaver(): Boolean = runCatching {
        (Settings.Secure.getString(context.contentResolver, "screensaver_components") ?: "").contains(own)
    }.getOrDefault(false)

    fun openTvSettings(): Boolean = openSettings()

    /**
     * Öffnet die Original-Startseite des Herstellers – ausdrücklich die fremde Home-App,
     * damit sie auch dann erreichbar bleibt, wenn Trendo die Standard-Startseite ist.
     */
    fun openOriginalHome(): Boolean {
        val home = Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_HOME)
        val other = pm.queryIntentActivities(home, 0).map { it.activityInfo }
            .firstOrNull { it.packageName != own && it.packageName != "android" }
            ?: return start(home.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
        return start(home.setComponent(ComponentName(other.packageName, other.name)).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
    }

    // ---- Startseiten-Modus (nur eigenes Paket, Bestätigung durch den Nutzer am TV) ----

    private val homeAlias get() = ComponentName(own, "$own.HomeAlias")

    /** Ist Trendo gerade die Standard-Startseite? */
    fun isDefaultHome(): Boolean = runCatching {
        pm.resolveActivity(Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_HOME), PackageManager.MATCH_DEFAULT_ONLY)
            ?.activityInfo?.packageName == own
    }.getOrDefault(false)

    /** Schaltet den eigenen Startseiten-Eintrag ein oder aus. Betrifft ausschließlich Trendo. */
    fun setHomeEntry(enabled: Boolean) {
        pm.setComponentEnabledSetting(
            homeAlias,
            if (enabled) PackageManager.COMPONENT_ENABLED_STATE_ENABLED else PackageManager.COMPONENT_ENABLED_STATE_DISABLED,
            PackageManager.DONT_KILL_APP,
        )
    }

    /**
     * Offizieller Android-Weg: Dialog „Standard-Startseite" (RoleManager, ab Android 10).
     * Gibt den Intent zurück, den die Activity starten muss, oder null, wenn das Gerät ihn nicht anbietet.
     */
    fun homeRoleRequest(): Intent? {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) return null
        val rm = context.getSystemService(RoleManager::class.java) ?: return null
        if (!rm.isRoleAvailable(RoleManager.ROLE_HOME) || rm.isRoleHeld(RoleManager.ROLE_HOME)) return null
        return rm.createRequestRoleIntent(RoleManager.ROLE_HOME)
    }

    /** Paketnamen der Original-Startseite(n); ohne Trendo und ohne die Notfall-Startseite der Einstellungen. */
    fun originalHomePackages(): Set<String> =
        pm.queryIntentActivities(Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_HOME), 0)
            .filter { it.priority >= 0 && it.activityInfo.packageName != own && it.activityInfo.packageName != "android" }
            .map { it.activityInfo.packageName }.toSet()

    /** Ist der Startseiten-Wächter in den Bedienungshilfen eingeschaltet? */
    fun isHomeGuardEnabled(): Boolean = runCatching {
        val list = Settings.Secure.getString(context.contentResolver, Settings.Secure.ENABLED_ACCESSIBILITY_SERVICES) ?: ""
        list.split(':').any { it.equals("$own/$own.HomeGuardService", true) || it.equals("$own/.HomeGuardService", true) }
    }.getOrDefault(false)

    /**
     * Bedienungshilfen öffnen. Manche Hersteller ersetzen den Android-Eintrag durch einen leeren Platzhalter
     * (frameworkpackagestubs) und bieten eine eigene Seite an – die wird zuerst versucht.
     */
    fun openAccessibilitySettings(): Boolean {
        val candidates = listOf("android.settings.ACCESSIBILITY_TV_OEM_LINK", Settings.ACTION_ACCESSIBILITY_SETTINGS)
        for (action in candidates) {
            val intent = Intent(action)
            val target = pm.resolveActivity(intent, PackageManager.MATCH_DEFAULT_ONLY)?.activityInfo?.packageName ?: continue
            if (target.contains("frameworkpackagestubs")) continue
            if (start(intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))) return true
        }
        return false
    }

    /** Ausweg: Android-Seite zur Wahl der Standard-Startseite (falls vorhanden). */
    fun openHomeSettings(): Boolean =
        start(Intent(Settings.ACTION_HOME_SETTINGS).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)) ||
            start(Intent(Settings.ACTION_MANAGE_DEFAULT_APPS_SETTINGS).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))

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
