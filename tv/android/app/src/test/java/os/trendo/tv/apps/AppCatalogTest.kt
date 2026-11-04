package os.trendo.tv.apps

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class AppCatalogTest {
    private val own = "os.trendo.tv"

    @Test fun eigeneAppErscheintNicht() {
        val r = AppCatalog.merge(listOf(AppEntry(own, "TRENDO OS", true)), emptyList(), own)
        assertTrue(r.isEmpty())
    }

    @Test fun tvEintragGewinntUndKeineDoppelten() {
        val r = AppCatalog.merge(
            tv = listOf(AppEntry("a.tv", "Beta", true)),
            phone = listOf(AppEntry("a.tv", "Beta", false), AppEntry("b.phone", "alpha", false)),
            ownPackage = own,
        )
        assertEquals(listOf("b.phone", "a.tv"), r.map { it.packageName })
        assertTrue(r.first { it.packageName == "a.tv" }.isTvApp)
    }

    @Test fun favoritenVorschlagNachAngezeigtemNamen() {
        val apps = listOf(AppEntry("x.1", "TiviMate", true), AppEntry("x.2", "Google Play Store", true), AppEntry("x.3", "Andere", true))
        assertEquals(listOf("x.1", "x.2"), AppCatalog.suggestedFavorites(apps))
    }

    @Test fun nichtSichtbareFavoritenWerdenUebersprungenNichtGeloescht() {
        val saved = listOf("weg.app", "x.1")
        val r = AppCatalog.favorites(listOf(AppEntry("x.1", "TiviMate", true)), saved)
        assertEquals(listOf("x.1"), r.map { it.packageName })
        assertEquals(2, saved.size)
    }
}

class AppGuardTest {
    private val own = "os.trendo.tv"

    @Test fun nurStartenUndInfoseiteErlaubt() {
        for (a in AppAction.entries) {
            val expected = a == AppAction.LAUNCH || a == AppAction.OPEN_SYSTEM_INFO
            assertEquals("Aktion $a", expected, AppGuard.isAllowed(a, "fremd.app", own))
        }
    }

    @Test fun eigeneAppUndLeerePaketIdAbgelehnt() {
        assertFalse(AppGuard.isAllowed(AppAction.LAUNCH, own, own))
        assertFalse(AppGuard.isAllowed(AppAction.LAUNCH, " ", own))
    }

    @Test fun gefaehrlicheBefehleErkannt() {
        listOf(
            "pm uninstall ar.tvplayer.tv", "PM  CLEAR com.android.vending", "pm disable-user --user 0 x",
            "cmd package uninstall x", "pm suspend x", "pm hide x", "echo a && pm uninstall x", "am force-stop x",
        ).forEach { assertTrue(it, AppGuard.isForbiddenCommand(it)) }
        listOf("pm list packages -3", "dumpsys package x", "getprop ro.build.version.sdk")
            .forEach { assertFalse(it, AppGuard.isForbiddenCommand(it)) }
    }
}
