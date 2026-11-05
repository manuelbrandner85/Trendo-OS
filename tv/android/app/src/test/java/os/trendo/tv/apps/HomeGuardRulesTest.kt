package os.trendo.tv.apps

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class HomeGuardRulesTest {
    private val homes = setOf("com.google.android.tvlauncher")

    @Test fun nurOriginalStartseiteLoestAus() {
        assertTrue(HomeGuardRules.shouldBringTrendo("com.google.android.tvlauncher", homes, false, ArrayDeque(), 0))
        assertFalse(HomeGuardRules.shouldBringTrendo("ar.tvplayer.tv", homes, false, ArrayDeque(), 0))
        assertFalse(HomeGuardRules.shouldBringTrendo("com.android.tv.settings", homes, false, ArrayDeque(), 0))
    }

    @Test fun bewussteWahlDerOriginalStartseiteWirdRespektiert() {
        assertFalse(HomeGuardRules.shouldBringTrendo("com.google.android.tvlauncher", homes, true, ArrayDeque(), 0))
    }

    @Test fun schleifenschutz() {
        val recent = ArrayDeque(listOf(1_000L, 2_000L, 3_000L))
        assertFalse(HomeGuardRules.shouldBringTrendo("com.google.android.tvlauncher", homes, false, recent, 4_000))
        assertTrue(HomeGuardRules.shouldBringTrendo("com.google.android.tvlauncher", homes, false, recent, 14_000))
    }
}
