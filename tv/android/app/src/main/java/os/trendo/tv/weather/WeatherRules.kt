package os.trendo.tv.weather

import org.json.JSONObject

data class Place(val name: String, val region: String, val lat: Double, val lon: Double)
data class Weather(val place: String, val tempC: Int, val code: Int, val isDay: Boolean, val fetchedAt: Long) {
    val text get() = WeatherRules.text(code)
}

/** Open-Meteo (frei, ohne Konto). Reine Auswertung – testbar ohne Netz. */
object WeatherRules {
    const val MAX_AGE_MS = 30 * 60 * 1000L

    fun forecastUrl(p: Place) =
        "https://api.open-meteo.com/v1/forecast?latitude=${p.lat}&longitude=${p.lon}&current=temperature_2m,weather_code,is_day&timezone=auto"

    fun geocodeUrl(name: String) =
        "https://geocoding-api.open-meteo.com/v1/search?count=5&language=de&format=json&name=" + java.net.URLEncoder.encode(name.trim(), "UTF-8")

    fun parseForecast(json: String, place: String, now: Long): Weather {
        val c = JSONObject(json).getJSONObject("current")
        return Weather(place, Math.round(c.getDouble("temperature_2m")).toInt(), c.getInt("weather_code"), c.optInt("is_day", 1) == 1, now)
    }

    fun parsePlaces(json: String): List<Place> {
        val arr = JSONObject(json).optJSONArray("results") ?: return emptyList()
        return (0 until arr.length()).map { i ->
            val o = arr.getJSONObject(i)
            Place(o.getString("name"), listOf(o.optString("admin1"), o.optString("country")).filter { it.isNotBlank() }.joinToString(", "),
                o.getDouble("latitude"), o.getDouble("longitude"))
        }
    }

    fun isFresh(w: Weather?, now: Long) = w != null && now - w.fetchedAt < MAX_AGE_MS

    /** WMO-Wettercodes auf Deutsch. */
    fun text(code: Int): String = when (code) {
        0 -> "Klarer Himmel"
        1 -> "Überwiegend klar"
        2 -> "Teilweise bewölkt"
        3 -> "Bedeckt"
        45, 48 -> "Nebel"
        51, 53, 55 -> "Nieselregen"
        56, 57 -> "Gefrierender Niesel"
        61, 63, 65 -> "Regen"
        66, 67 -> "Gefrierender Regen"
        71, 73, 75, 77 -> "Schnee"
        80, 81, 82 -> "Regenschauer"
        85, 86 -> "Schneeschauer"
        95 -> "Gewitter"
        96, 99 -> "Gewitter mit Hagel"
        else -> "Wetter"
    }

    enum class Sky { SUN, MOON, PARTLY, CLOUD, FOG, RAIN, SNOW, STORM }

    fun sky(code: Int, isDay: Boolean): Sky = when (code) {
        0, 1 -> if (isDay) Sky.SUN else Sky.MOON
        2 -> Sky.PARTLY
        3 -> Sky.CLOUD
        45, 48 -> Sky.FOG
        in 51..67, in 80..82 -> Sky.RAIN
        in 71..77, 85, 86 -> Sky.SNOW
        in 95..99 -> Sky.STORM
        else -> Sky.CLOUD
    }
}
