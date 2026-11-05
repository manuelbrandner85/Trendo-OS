package os.trendo.tv.livetv

import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class MediathekTest {
    @Test fun anfrageWieMediathekViewWeb() {
        val q = JSONObject(Mediathek.queryJson("ZDF", "Film", 3600, 20))
        assertEquals(2, q.getJSONArray("queries").length())
        assertEquals("zdf", q.getJSONArray("queries").getJSONObject(0).getString("query"))
        assertEquals("film", q.getJSONArray("queries").getJSONObject(1).getString("query"))
        assertEquals(3600, q.getInt("duration_min")); assertEquals(20, q.getInt("size")); assertEquals(false, q.getBoolean("future"))
        assertEquals(0, JSONObject(Mediathek.queryJson(null, "", 0, 5)).getJSONArray("queries").length())
    }

    @Test fun antwortLesen() {
        val json = """{"result":{"results":[
            {"channel":"ZDF","topic":"Spielfilm-Highlights","title":"Fabian","description":"Ein Film","timestamp":1793750400,"duration":6600,"url_video":"https://v/1.mp4","url_video_hd":"https://v/1hd.mp4","url_website":"https://zdf.de/x"},
            {"channel":"ZDF","topic":"Spielfilm-Highlights","title":"Fabian","duration":6600,"url_video":"https://v/1b.mp4"},
            {"channel":"ARD","topic":"Doku","title":"Ohne Video","duration":100,"url_video":""}]}}"""
        val r = Mediathek.parse(json)
        assertEquals(1, r.size); assertEquals("Fabian", r[0].title); assertEquals(1793750400000L, r[0].timestamp); assertEquals("https://v/1hd.mp4", r[0].urlHd)
        assertTrue(Mediathek.parse("kaputt").isEmpty())
        assertEquals("1 Std. 50 Min.", Mediathek.minutes(6600))
    }

    @Test fun appsAmNamenErkennen() {
        val installed = listOf("com.netflix.ninja" to "Netflix", "x.joyn" to "Joyn", "de.ard" to "ARD Mediathek")
        assertEquals("com.netflix.ninja", StreamServices.match(listOf("netflix"), installed))
        assertEquals("de.ard", StreamServices.match(StreamServices.mediathekApps["ARD"]!!, installed))
        assertNull(StreamServices.match(listOf("pluto"), installed))
    }
}

class ProvidersTest {
    @Test fun anbieterAusKategorie() {
        assertEquals("Netflix", Providers.of("DE • NETFLIX SERIEN"))
        assertEquals("Disney+", Providers.of("DE | DISNEY+ FILME"))
        assertEquals("Prime Video", Providers.of("DE • AMAZON PRIME FILME"))
        assertEquals("Sky", Providers.of("DE: SKY CINEMA"))
        assertEquals("Apple TV+", Providers.of("DE • APPLE TV+"))
        assertNull(Providers.of("DE • ACTION"))
        assertEquals(listOf("Netflix", "Sky"), Providers.present(listOf("DE: SKY SPORT", "DE • NETFLIX FILME", "DE • DRAMA")))
    }
}
