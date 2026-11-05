package os.trendo.tv.weather

import android.content.Context
import java.net.HttpURLConnection
import java.net.URL

/** Wetter von Open-Meteo (frei, ohne Konto, keine Ortung). Ort wählt der Nutzer selbst. Cache 30 min. */
class WeatherRepo(context: Context) {
    private val prefs = context.getSharedPreferences("trendo_tv_weather", Context.MODE_PRIVATE)

    var place: Place?
        get() {
            val n = prefs.getString("name", null) ?: return null
            return Place(n, prefs.getString("region", "") ?: "", prefs.getFloat("lat", 0f).toDouble(), prefs.getFloat("lon", 0f).toDouble())
        }
        set(p) {
            val e = prefs.edit()
            if (p == null) e.clear() else e.putString("name", p.name).putString("region", p.region)
                .putFloat("lat", p.lat.toFloat()).putFloat("lon", p.lon.toFloat()).remove("cache")
            e.apply()
        }

    private fun cached(): Weather? {
        val p = place ?: return null
        if (!prefs.contains("w_temp")) return null
        return Weather(p.name, prefs.getInt("w_temp", 0), prefs.getInt("w_code", 0), prefs.getBoolean("w_day", true), prefs.getLong("w_at", 0))
    }

    /** Liefert frisches Wetter, sonst den letzten Stand (auch offline), oder null ohne Ort. */
    fun current(now: Long = System.currentTimeMillis()): Weather? {
        val p = place ?: return null
        val old = cached()
        if (WeatherRules.isFresh(old, now)) return old
        return runCatching { WeatherRules.parseForecast(get(WeatherRules.forecastUrl(p)), p.name, now) }
            .onSuccess { w -> prefs.edit().putInt("w_temp", w.tempC).putInt("w_code", w.code).putBoolean("w_day", w.isDay).putLong("w_at", w.fetchedAt).apply() }
            .getOrElse { old }
    }

    fun search(name: String): List<Place> = runCatching { WeatherRules.parsePlaces(get(WeatherRules.geocodeUrl(name))) }.getOrDefault(emptyList())

    private fun get(url: String): String {
        val c = URL(url).openConnection() as HttpURLConnection
        c.connectTimeout = 5000; c.readTimeout = 5000
        c.setRequestProperty("User-Agent", "TrendoOS-TV")
        return try { c.inputStream.bufferedReader().use { it.readText() } } finally { c.disconnect() }
    }
}
