package os.trendo.tv.phone

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class RemotePairingTest {
    @Test fun nurGekoppelteHandysImHeimnetz() {
        val t = RemotePairing.newToken()
        assertEquals(32, t.length); assertNotEquals(t, RemotePairing.newToken())
        assertTrue(RemotePairing.allowed(t, setOf(t), siteLocal = true))
        assertFalse("fremdes Handy", RemotePairing.allowed(RemotePairing.newToken(), setOf(t), siteLocal = true))
        assertFalse("aus dem Internet", RemotePairing.allowed(t, setOf(t), siteLocal = false))
        assertFalse("ohne Token", RemotePairing.allowed(null, setOf(t), siteLocal = true))
        assertFalse("zu kurz", RemotePairing.allowed("abc", setOf("abc"), siteLocal = true))
    }

    @Test fun nurErlaubteTasten() {
        assertTrue("ok" in RemotePairing.keys)
        assertFalse("home" in RemotePairing.keys); assertFalse("power" in RemotePairing.keys)
    }
}
