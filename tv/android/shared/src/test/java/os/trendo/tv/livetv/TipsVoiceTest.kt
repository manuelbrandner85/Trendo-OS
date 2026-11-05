package os.trendo.tv.livetv

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.TimeZone

class TipsVoiceTest {
    private val now = 1_800_000_000_000L
    private val zdf = Channel("a:x1", "DE: ZDF HD", "u1", "DE | Voll", epgId = "zdf")
    private val sky = Channel("a:x2", "Sky Sport Bundesliga", "u2", "DE | Sport", epgId = "sky", archiveDays = 3, catchup = "http://h/timeshift/u/p/{dur}/{start}/2.ts")
    private val kika = Channel("a:x3", "KiKA", "u3", "Kinder", epgId = "kika")
    private val epg = mapOf(
        "zdf" to listOf(Programme("zdf", now - 600_000, now + 3_000_000, "Tatort: Der Fall", category = "Krimi Spielfilm"), Programme("zdf", now + 3_000_000, now + 6_000_000, "heute journal", category = "Nachrichten")),
        "sky" to listOf(Programme("sky", now - 1_000_000, now + 4_000_000, "Bundesliga: Bayern – Dortmund", category = "Sport")),
        "kika" to listOf(Programme("kika", now - 100_000, now + 900_000, "Die Sendung mit der Maus", category = "Kinder")),
    )

    @Test fun reihenAusDemProgramm() {
        val rows = Tips.rows(listOf(zdf, sky, kika), epg, setOf("a:x2"), now)
        val byTitle = rows.associate { it.title to it.tips.map { t -> t.programme.title } }
        assertEquals(listOf("Bundesliga: Bayern – Dortmund"), byTitle["Jetzt live: Sport"])
        assertTrue(byTitle["Filme – jetzt & gleich"]!!.contains("Tatort: Der Fall"))
        assertTrue(byTitle["Kinder"]!!.contains("Die Sendung mit der Maus"))
        val kids = Tips.rows(listOf(zdf, sky, kika), epg, emptySet(), now, kidsMode = true)
        assertEquals(listOf("Kinder – jetzt"), kids.map { it.title })
    }

    @Test fun sprachbefehle() {
        val ch = listOf(zdf, sky, kika)
        assertEquals(zdf, (LiveVoice.parse("Schalte auf ZDF", ch, epg, now) as LiveCommand.Play).channel)
        assertEquals(kika, (LiveVoice.parse("kika", ch, epg, now) as LiveCommand.Play).channel)
        assertEquals(sky, (LiveVoice.parse("zeig sky sport", ch, epg, now) as LiveCommand.Play).channel)
        assertEquals(LiveCommand.Tab("TIPS", "krimi"), LiveVoice.parse("Was läuft heute Abend für Krimis?", ch, epg, now))
        assertEquals("heute journal", (LiveVoice.parse("Erinnere mich an heute journal", ch, epg, now) as LiveCommand.Remind).tip.programme.title)
        assertEquals(LiveCommand.Tab("GUIDE"), LiveVoice.parse("Programmführer", ch, epg, now))
        assertTrue(LiveVoice.parse("blabla xyz", ch, epg, now) is LiveCommand.Unknown)
    }

    @Test fun catchupAdresse() {
        assertTrue(TvRules.canCatchup(sky, now - 3_600_000, now))
        assertTrue(!TvRules.canCatchup(sky, now - 4 * 86_400_000L, now))
        assertTrue(!TvRules.canCatchup(zdf, now - 3_600_000, now))
        val url = TvRules.catchupUrl(sky, 1_800_000_000_000L, 90, TimeZone.getTimeZone("UTC"))
        assertEquals("http://h/timeshift/u/p/90/2027-01-15:08-00/2.ts", url)
        assertNull(TvRules.catchupUrl(zdf, now, 10))
    }
}
