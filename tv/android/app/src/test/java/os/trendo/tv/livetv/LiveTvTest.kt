package os.trendo.tv.livetv

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class LiveTvTest {
    private val m3u = """
        #EXTM3U x-tvg-url="http://epg.example/guide.xml"
        #EXTINF:-1 tvg-id="zdf.de" tvg-logo="http://l/zdf.png" group-title="DE | Vollprogramm" tvg-chno="2",ZDF HD
        http://s/1.ts
        #EXTINF:-1 tvg-id="zdf.de" group-title="DE | Vollprogramm",ZDF FHD
        http://s/2.ts
        #EXTINF:-1 group-title="TR | Ulusal",TRT 1
        http://s/3.ts
        #EXTINF:-1 group-title="XXX Adult",Nacht
        http://s/4.ts
        #EXTINF:-1 group-title="Kinder",KiKA HD
        http://s/5.ts
    """.trimIndent()

    @Test fun m3uLesen() {
        val r = M3u.parse(m3u, "a")
        assertEquals("http://epg.example/guide.xml", r.epgUrl)
        assertEquals(5, r.channels.size)
        val z = r.channels[0]
        assertEquals("ZDF HD", z.name); assertEquals("zdf.de", z.epgId); assertEquals("DE | Vollprogramm", z.group); assertEquals(2, z.number)
        assertEquals("http://l/zdf.png", z.logo); assertEquals("http://s/1.ts", z.url)
    }

    @Test fun filterWieWindows() {
        val ch = M3u.parse(m3u, "a").channels
        assertEquals("de", TvRules.countryOf("DE | Vollprogramm")); assertEquals("tr", TvRules.countryOf("TR | Ulusal"))
        assertTrue(TvRules.isAdult("XXX Adult")); assertFalse(TvRules.isAdult("Sport"))
        val deOnly = TvRules.visible(ch, LiveSettings(onlyGerman = true, mergeDuplicates = false))
        assertEquals(listOf("ZDF HD", "ZDF FHD", "KiKA HD"), deOnly.map { it.name })
        val merged = TvRules.visible(ch, LiveSettings(onlyGerman = true, mergeDuplicates = true))
        assertEquals(listOf("ZDF FHD", "KiKA HD"), merged.map { it.name })
        val kids = TvRules.visible(ch, LiveSettings(kidsMode = true))
        assertEquals(listOf("KiKA HD"), kids.map { it.name })
        assertTrue(TvRules.visible(ch, LiveSettings(showAdult = true, mergeDuplicates = false)).any { it.name == "Nacht" })
        assertTrue(TvRules.visible(ch, LiveSettings(hiddenGroups = setOf("Kinder"))).none { it.group == "Kinder" })
    }

    @Test fun xtreamAdressen() {
        assertEquals("http://host:8080/get.php?username=a+b&password=p%26w&type=m3u_plus&output=ts", Xtream.playlistUrl("host:8080/", "a b", "p&w"))
        val t = Xtream.fromPlaylistUrl("http://host:8080/get.php?username=u1&password=p%40ss&type=m3u_plus")!!
        assertEquals("http://host:8080", t.first); assertEquals("u1", t.second); assertEquals("p@ss", t.third)
        assertNull(Xtream.fromPlaylistUrl("http://host/liste.m3u"))
        val live = Xtream.parseLive("""[{"stream_id":7,"name":"Das Erste HD","category_id":"1","epg_channel_id":"daserste.de","num":1}]""",
            """[{"category_id":"1","category_name":"DE | ARD"}]""", "h", "u", "p", "x")
        assertEquals("http://h/live/u/p/7.ts", live.single().url); assertEquals("DE | ARD", live.single().group)
        val eps = Xtream.parseEpisodes("""{"episodes":{"1":[{"id":"55","episode_num":2,"title":"Folge 2","container_extension":"mkv","season":1},{"id":"54","episode_num":1,"title":"Folge 1","season":1}]}}""",
            "x:s9", "h", "u", "p")
        assertEquals(listOf(1, 2), eps.map { it.episode }); assertEquals("http://h/series/u/p/55.mkv", eps[1].url)
    }

    @Test fun xmltvMitZeitfenster() {
        assertEquals(Xmltv.parseTime("20261105180000 +0000") - 3_600_000, Xmltv.parseTime("20261105180000 +0100"))
        val xml = """<?xml version="1.0"?><tv>
            <programme start="20261105180000 +0100" stop="20261105190000 +0100" channel="zdf.de"><title lang="de">heute</title><desc>Nachrichten</desc></programme>
            <programme start="20261105190000 +0100" stop="20261105200000 +0100" channel="zdf.de"><title>Krimi</title></programme>
            <programme start="20261105190000 +0100" stop="20261105200000 +0100" channel="anders"><title>Weg</title></programme>
            <programme start="20261201190000 +0100" stop="20261201200000 +0100" channel="zdf.de"><title>Zu spät</title></programme>
            </tv>"""
        val from = Xmltv.parseTime("20261105000000 +0000"); val to = Xmltv.parseTime("20261107000000 +0000")
        val p = Xmltv.parse(xml.byteInputStream(), setOf("zdf.de"), from, to)
        assertEquals(listOf("heute", "Krimi"), p.map { it.title }); assertEquals("Nachrichten", p[0].description)
        val (now, next) = TvRules.nowNext(p, Xmltv.parseTime("20261105183000 +0100"))
        assertEquals("heute", now?.title); assertEquals("Krimi", next?.title)
    }

    @Test fun pinWirdNichtImKlartextGespeichert() {
        val h = TvRules.pinHash("1234", "salz")
        assertEquals(64, h.length); assertFalse(h.contains("1234")); assertEquals(h, TvRules.pinHash("1234", "salz"))
    }
}
