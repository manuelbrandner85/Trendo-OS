package os.trendo.tv.apps

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ProtectedAppsTest {
    @Test fun wichtigeAppsGeschuetzt() {
        listOf(
            AppEntry("ar.tvplayer.tv", "TiviMate", true), AppEntry("com.android.vending", "Google Play Store", true),
            AppEntry("com.google.android.tv", "Live TV", true), AppEntry("com.mediatek.tvsetup", "LiveTVSetupWizard", true),
            AppEntry("com.x.mtktvapi_agent", "mtktvapi_agent", true),
        ).forEach { assertTrue(it.label, ProtectedApps.isProtected(it)) }
    }

    @Test fun normaleAppsNichtGeschuetzt() {
        listOf(
            AppEntry("com.disney.disneyplus", "Disney+", true), AppEntry("org.xbmc.kodi", "Kodi", true),
            AppEntry("com.homwee.aipont", "AI PONT", true), AppEntry("com.esaba.downloader", "Downloader", true),
        ).forEach { assertFalse(it.label, ProtectedApps.isProtected(it)) }
    }
}
