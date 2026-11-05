package os.trendo.tv.livetv

import android.content.Context
import org.json.JSONArray
import org.json.JSONObject
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/** Ein Profil in Trendo Live TV: eigene Favoriten, Verlauf, Filter. Kinderprofil mit Zeitlimit. */
data class Profile(val id: String, val name: String, val kid: Boolean = false, val minutesPerDay: Int = 0)

/** Reine Regeln für Kinderprofile (testbar ohne Android). */
object KidRules {
    /** Ein Kinderprofil sieht immer nur Kinderinhalte, nie Erwachseneninhalte. */
    fun effective(s: LiveSettings, p: Profile?): LiveSettings = if (p?.kid == true) s.copy(kidsMode = true, showAdult = false) else s

    /** Verbleibende Minuten heute oder null = kein Limit. */
    fun remaining(p: Profile?, usedToday: Int): Int? = if (p == null || !p.kid || p.minutesPerDay <= 0) null else (p.minutesPerDay - usedToday).coerceAtLeast(0)

    fun limitReached(p: Profile?, usedToday: Int): Boolean = remaining(p, usedToday) == 0
}

class ProfileStore(context: Context) {
    private val prefs = context.getSharedPreferences("trendo_live_profiles", Context.MODE_PRIVATE)

    fun list(): List<Profile> = runCatching {
        val a = JSONArray(prefs.getString("list", null) ?: return listOf(DEFAULT))
        (0 until a.length()).map { i -> a.getJSONObject(i).let { Profile(it.getString("id"), it.getString("name"), it.optBoolean("kid"), it.optInt("min")) } }
            .ifEmpty { listOf(DEFAULT) }
    }.getOrDefault(listOf(DEFAULT))

    fun save(list: List<Profile>) {
        val a = JSONArray(); list.forEach { a.put(JSONObject().put("id", it.id).put("name", it.name).put("kid", it.kid).put("min", it.minutesPerDay)) }
        prefs.edit().putString("list", a.toString()).apply()
    }

    fun add(name: String, kid: Boolean): Profile {
        val p = Profile("p" + System.currentTimeMillis().toString(36), name.trim().ifBlank { if (kid) "Kinder" else "Profil" }, kid, if (kid) 60 else 0)
        save(list() + p); return p
    }

    fun update(p: Profile) = save(list().map { if (it.id == p.id) p else it })
    fun remove(id: String) { if (id != DEFAULT.id) save(list().filter { it.id != id }); if (currentId == id) currentId = DEFAULT.id }

    var currentId: String
        get() = prefs.getString("current", DEFAULT.id) ?: DEFAULT.id
        set(v) { prefs.edit().putString("current", v).apply() }

    fun current(): Profile = list().firstOrNull { it.id == currentId } ?: DEFAULT

    private fun today() = SimpleDateFormat("yyyyMMdd", Locale.US).format(Date())
    fun usedToday(id: String): Int = prefs.getInt("use_${id}_${today()}", 0)
    fun addMinute(id: String) { val k = "use_${id}_${today()}"; prefs.edit().putInt(k, prefs.getInt(k, 0) + 1).apply() }

    companion object {
        /** Standardprofil – neutral benannt (kein fester Nutzer). */
        val DEFAULT = Profile("standard", "Hauptprofil")
    }
}
