package os.trendo.tv.ai

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import os.trendo.tv.apps.AppEntry
import os.trendo.tv.media.ResumeRules
import os.trendo.tv.weather.WeatherRules

class CommandParserTest {
    private val apps = listOf(
        AppEntry("ar.tvplayer.tv", "TiviMate", true), AppEntry("com.android.vending", "Google Play Store", true),
        AppEntry("org.xbmc.kodi", "Kodi", true), AppEntry("com.google.android.youtube.tv", "YouTube", true),
    )

    @Test fun appOeffnen() {
        assertEquals("ar.tvplayer.tv", (CommandParser.parse("Öffne TiviMate", apps) as Command.OpenApp).app.packageName)
        assertEquals("org.xbmc.kodi", (CommandParser.parse("starte kodi", apps) as Command.OpenApp).app.packageName)
        assertEquals("com.google.android.youtube.tv", (CommandParser.parse("YouTube", apps) as Command.OpenApp).app.packageName)
        assertEquals("com.android.vending", (CommandParser.parse("öffne den play store", apps) as Command.OpenApp).app.packageName)
    }

    @Test fun timer() {
        assertEquals(Command.Timer(30), CommandParser.parse("Timer 30 Minuten", apps))
        assertEquals(Command.Timer(60), CommandParser.parse("Einschlaf-Timer eine Stunde", apps))
        assertEquals(Command.Timer(45), CommandParser.parse("timer fünfundvierzig minuten", apps))
        assertEquals(Command.TimerOff, CommandParser.parse("Timer aus", apps))
    }

    @Test fun eingangLautstaerkeMedien() {
        assertEquals(Command.Input(2), CommandParser.parse("HDMI 2", apps))
        assertEquals(Command.Input(null), CommandParser.parse("Eingang wechseln", apps))
        assertEquals(Command.Volume(true), CommandParser.parse("lauter", apps))
        assertEquals(Command.Volume(false), CommandParser.parse("Bitte leiser", apps))
        assertEquals(Command.Mute, CommandParser.parse("stumm", apps))
        assertEquals(Command.Media(MediaAction.PLAY_PAUSE), CommandParser.parse("Pause", apps))
    }

    @Test fun sucheSeitenWetterUnbekannt() {
        assertEquals(Command.Search("krimi"), CommandParser.parse("suche krimi", apps))
        assertEquals(Command.Page("SETTINGS"), CommandParser.parse("öffne einstellungen", apps))
        assertEquals(Command.Page("MUSIC"), CommandParser.parse("zeige musik", apps))
        assertEquals(Command.Weather, CommandParser.parse("Wie ist das Wetter?", apps))
        assertTrue(CommandParser.parse("blablub", apps) is Command.Unknown)
        assertTrue(CommandParser.parse("   ", apps) is Command.Unknown)
    }
}

class MediaAndWeatherRulesTest {
    @Test fun fortsetzen() {
        assertTrue(!ResumeRules.shouldRemember(10_000, 600_000))
        assertTrue(ResumeRules.shouldRemember(120_000, 600_000))
        assertTrue(!ResumeRules.shouldRemember(590_000, 600_000))
        assertEquals("1:02:03", ResumeRules.formatTime(3_723_000))
        assertEquals("4:05", ResumeRules.formatTime(245_000))
    }

    @Test fun wetterAuswerten() {
        val w = WeatherRules.parseForecast("""{"current":{"temperature_2m":23.6,"weather_code":0,"is_day":1}}""", "Rom", 0)
        assertEquals(24, w.tempC); assertEquals("Klarer Himmel", w.text)
        val p = WeatherRules.parsePlaces("""{"results":[{"name":"Wien","latitude":48.2,"longitude":16.37,"admin1":"Wien","country":"Österreich"}]}""")
        assertEquals("Wien", p.single().name); assertEquals("Wien, Österreich", p.single().region)
        assertTrue(WeatherRules.parsePlaces("""{"generationtime_ms":0.1}""").isEmpty())
        assertEquals(WeatherRules.Sky.MOON, WeatherRules.sky(0, false))
        assertEquals(WeatherRules.Sky.RAIN, WeatherRules.sky(63, true))
        assertTrue(WeatherRules.isFresh(w, 10 * 60_000) && !WeatherRules.isFresh(w, 31 * 60_000))
    }
}
