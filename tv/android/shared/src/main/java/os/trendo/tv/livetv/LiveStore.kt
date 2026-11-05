package os.trendo.tv.livetv

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/** Passwörter verschlüsselt mit einem Schlüssel aus dem Android-Keystore. Nie im Klartext, nie in Protokollen. */
class SecretBox(context: Context) {
    private val prefs = context.getSharedPreferences("trendo_tv_secrets", Context.MODE_PRIVATE)
    private val alias = "trendo_tv_live"

    private fun key(): SecretKey {
        val ks = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (ks.getKey(alias, null) as? SecretKey)?.let { return it }
        val gen = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore")
        gen.init(KeyGenParameterSpec.Builder(alias, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
            .setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).build())
        return gen.generateKey()
    }

    fun put(id: String, secret: String) {
        val c = Cipher.getInstance("AES/GCM/NoPadding").apply { init(Cipher.ENCRYPT_MODE, key()) }
        val enc = c.doFinal(secret.toByteArray())
        prefs.edit().putString(id, Base64.encodeToString(c.iv, Base64.NO_WRAP) + ":" + Base64.encodeToString(enc, Base64.NO_WRAP)).apply()
    }

    fun get(id: String): String? = runCatching {
        val (iv, data) = (prefs.getString(id, null) ?: return null).split(':', limit = 2)
        val c = Cipher.getInstance("AES/GCM/NoPadding").apply { init(Cipher.DECRYPT_MODE, key(), GCMParameterSpec(128, Base64.decode(iv, Base64.NO_WRAP))) }
        String(c.doFinal(Base64.decode(data, Base64.NO_WRAP)))
    }.getOrNull()

    fun remove(id: String) { prefs.edit().remove(id).apply() }
}

/** Speichert Quellen, Senderlisten, Programmführer, Filme/Serien und Einstellungen im privaten Trendo-Speicher. */
class LiveStore(private val context: Context, private val profile: String = "standard") {
    private val dir = File(context.filesDir, "livetv").apply { mkdirs() }
    private val prefs = context.getSharedPreferences("trendo_tv_live_$profile", Context.MODE_PRIVATE)
    private val shared = context.getSharedPreferences("trendo_tv_live_shared", Context.MODE_PRIVATE)
    val secrets = SecretBox(context)

    // ---- Quellen (für alle Profile gleich) ----
    /** Doppelte Quellen (gleiche Art, Adresse, Benutzer) zusammenfassen – die erste bleibt. */
    fun dedupeSources(): Int {
        val all = sources()
        val keep = all.distinctBy { Triple(it.kind, it.url, it.user) }.distinctBy { it.id }
        if (keep.size == all.size) return 0
        saveSources(keep)
        // Dateien/Passwort nur für Kennungen löschen, die NICHT mehr gebraucht werden.
        val keepIds = keep.map { it.id }.toSet()
        all.map { it.id }.filter { it !in keepIds }.distinct().forEach { id ->
            secrets.remove(id); listOf("ch_$id.json", "vod_$id.json", "ser_$id.json", "ch_$id.tsv", "vod_$id.tsv", "ser_$id.tsv").forEach { File(dir, it).delete() }
        }
        return all.size - keep.size
    }

    fun sources(): List<Source> = runCatching {
        val a = JSONArray(shared.getString("sources", "[]"))
        (0 until a.length()).map { i -> a.getJSONObject(i).let {
            Source(it.getString("id"), it.getString("name"), SourceKind.valueOf(it.getString("kind")), it.getString("url"),
                it.optString("user").ifBlank { null }, it.optString("epg").ifBlank { null })
        } }
    }.getOrDefault(emptyList())

    fun saveSources(list: List<Source>) {
        val a = JSONArray()
        list.forEach { s -> a.put(JSONObject().put("id", s.id).put("name", s.name).put("kind", s.kind.name).put("url", s.url).put("user", s.user ?: "").put("epg", s.epgUrl ?: "")) }
        shared.edit().putString("sources", a.toString()).apply()
    }

    fun removeSource(id: String) {
        saveSources(sources().filter { it.id != id }); secrets.remove(id)
        listOf("ch_$id.json", "vod_$id.json", "ser_$id.json", "ch_$id.tsv", "vod_$id.tsv", "ser_$id.tsv").forEach { File(dir, it).delete() }
    }

    // ---- Sender (schnelles Zeilenformat, Tab-getrennt; alte JSON-Dateien werden einmalig umgewandelt) ----
    private fun t(v: String?) = (v ?: "").replace('\t', ' ').replace('\n', ' ').replace('\r', ' ')

    private fun writeTsv(name: String, rows: Sequence<String>) {
        val tmp = File(dir, "$name.tmp")
        tmp.bufferedWriter(Charsets.UTF_8, 1 shl 16).use { w -> rows.forEach { w.write(it); w.write("\n") } }
        tmp.renameTo(File(dir, name)).also { if (!it) { File(dir, name).delete(); tmp.renameTo(File(dir, name)) } }
    }

    private fun <T> readTsv(name: String, min: Int, f: (List<String>) -> T?): List<T>? {
        val file = File(dir, name); if (!file.exists()) return null
        val out = ArrayList<T>()
        file.bufferedReader(Charsets.UTF_8, 1 shl 16).useLines { lines -> lines.forEach { l -> val p = l.split('\t'); if (p.size >= min) f(p)?.let { out += it } } }
        return out
    }

    fun saveChannels(sourceId: String, list: List<Channel>) {
        writeTsv("ch_$sourceId.tsv", list.asSequence().map { c -> listOf(c.id, c.name, c.url, c.group, c.logo, c.epgId, c.number.toString(), c.archiveDays.toString(), c.catchup).joinToString("\t") { t(it) } })
        File(dir, "ch_$sourceId.json").delete()
    }

    fun channels(): List<Channel> = sources().flatMap { s ->
        readTsv("ch_${s.id}.tsv", 9) { f -> Channel(f[0], f[1], f[2], f[3], f[4].ifBlank { null }, f[5].ifBlank { null }, f[6].toIntOrNull() ?: 0, s.id, f[7].toIntOrNull() ?: 0, f[8].ifBlank { null }) }
            ?: runCatching {
                val a = JSONArray(File(dir, "ch_${s.id}.json").readText())
                (0 until a.length()).map { i -> a.getJSONObject(i).let {
                    Channel(it.getString("i"), it.getString("n"), it.getString("u"), it.getString("g"), it.optString("l").ifBlank { null },
                        it.optString("e").ifBlank { null }, it.optInt("c"), s.id, it.optInt("a"), it.optString("cu").ifBlank { null })
                } }.also { saveChannels(s.id, it) }
            }.getOrDefault(emptyList())
    }

    // ---- Programmführer (kompakt: Tab-getrennt) ----
    fun saveEpg(list: List<Programme>) {
        File(dir, "epg.tsv").bufferedWriter().use { w ->
            list.forEach { p -> w.write("${p.channel}\t${p.start}\t${p.stop}\t${clean(p.title)}\t${clean(p.category)}\t${clean(p.description)}\n") }
        }
        shared.edit().putLong("epg_at", System.currentTimeMillis()).apply()
    }

    private fun clean(s: String) = s.replace('\t', ' ').replace('\n', ' ')

    fun epg(): Map<String, List<Programme>> = runCatching {
        val out = HashMap<String, MutableList<Programme>>()
        File(dir, "epg.tsv").forEachLine { line ->
            val f = line.split('\t'); if (f.size < 6) return@forEachLine
            out.getOrPut(f[0]) { ArrayList() } += Programme(f[0], f[1].toLong(), f[2].toLong(), f[3], f[5], f[4])
        }
        out.mapValues { (_, v) -> v.sortedBy { it.start } }
    }.getOrDefault(emptyMap())

    val epgAt get() = shared.getLong("epg_at", 0)

    // ---- Filme & Serien (schnelles Zeilenformat) ----
    fun saveVod(sourceId: String, list: List<Vod>) {
        writeTsv("vod_$sourceId.tsv", list.asSequence().map { v -> listOf(v.id, v.name, v.category, v.url, v.cover, v.plot, v.year, v.rating).joinToString("\t") { t(it) } })
        File(dir, "vod_$sourceId.json").delete()
    }

    fun vod(): List<Vod> = sources().flatMap { s ->
        readTsv("vod_${s.id}.tsv", 8) { f -> Vod(f[0], f[1], f[2], f[3], f[4].ifBlank { null }, f[5].ifBlank { null }, f[6].ifBlank { null }, f[7].ifBlank { null }, s.id) }
            ?: runCatching {
                val a = JSONArray(File(dir, "vod_${s.id}.json").readText())
                (0 until a.length()).map { i -> a.getJSONObject(i).let { Vod(it.getString("i"), it.getString("n"), it.getString("c"), it.getString("u"),
                    it.optString("cv").ifBlank { null }, it.optString("p").ifBlank { null }, it.optString("y").ifBlank { null }, it.optString("r").ifBlank { null }, s.id) } }
                    .also { saveVod(s.id, it) }
            }.getOrDefault(emptyList())
    }

    fun saveSeries(sourceId: String, list: List<Series>) {
        writeTsv("ser_$sourceId.tsv", list.asSequence().map { v -> listOf(v.id, v.name, v.category, v.cover, v.plot, v.year, v.rating).joinToString("\t") { t(it) } })
        File(dir, "ser_$sourceId.json").delete()
    }

    fun series(): List<Series> = sources().flatMap { s ->
        readTsv("ser_${s.id}.tsv", 7) { f -> Series(f[0], f[1], f[2], f[3].ifBlank { null }, f[4].ifBlank { null }, f[5].ifBlank { null }, f[6].ifBlank { null }, s.id) }
            ?: runCatching {
                val a = JSONArray(File(dir, "ser_${s.id}.json").readText())
                (0 until a.length()).map { i -> a.getJSONObject(i).let { Series(it.getString("i"), it.getString("n"), it.getString("c"),
                    it.optString("cv").ifBlank { null }, it.optString("p").ifBlank { null }, it.optString("y").ifBlank { null }, it.optString("r").ifBlank { null }, s.id) } }
                    .also { saveSeries(s.id, it) }
            }.getOrDefault(emptyList())
    }

    // ---- Einstellungen je Profil ----
    fun settings() = LiveSettings(
        favorites = prefs.getStringSet("fav", emptySet())!!.toSet(),
        hiddenGroups = prefs.getStringSet("hidden", emptySet())!!.toSet(),
        lockedGroups = prefs.getStringSet("locked", emptySet())!!.toSet(),
        onlyGerman = prefs.getBoolean("de", false), showAdult = prefs.getBoolean("adult", false),
        kidsMode = prefs.getBoolean("kids", false), mergeDuplicates = prefs.getBoolean("merge", true),
        lastChannel = prefs.getString("last", null),
        hiddenChannels = prefs.getStringSet("hiddenCh", emptySet())!!.toSet(),
        sortMode = prefs.getString("sort", "default") ?: "default",
    )

    fun save(s: LiveSettings) {
        prefs.edit().putStringSet("fav", s.favorites).putStringSet("hidden", s.hiddenGroups).putStringSet("locked", s.lockedGroups)
            .putBoolean("de", s.onlyGerman).putBoolean("adult", s.showAdult).putBoolean("kids", s.kidsMode).putBoolean("merge", s.mergeDuplicates)
            .putString("last", s.lastChannel).putStringSet("hiddenCh", s.hiddenChannels).putString("sort", s.sortMode).apply()
    }

    // ---- Verlauf (zuletzt gesehene Sender) ----
    fun history(): List<String> = prefs.getString("hist", "")!!.split('\n').filter { it.isNotBlank() }
    fun addHistory(id: String) { prefs.edit().putString("hist", (listOf(id) + history().filter { it != id }).take(20).joinToString("\n")).apply() }

    // ---- Sprachfilter je Reiter (null = alle Sprachen) ----
    fun language(tab: String): String? = prefs.getString("lang_$tab", null)?.ifBlank { null }
    fun setLanguage(tab: String, code: String?) { prefs.edit().putString("lang_$tab", code ?: "").apply() }

    // ---- Automatik ----
    var autoplayLast: Boolean
        get() = shared.getBoolean("autoplay", false)
        set(v) { shared.edit().putBoolean("autoplay", v).apply() }
    val sourcesAt get() = shared.getLong("src_at", 0)
    fun markSourcesLoaded() { shared.edit().putLong("src_at", System.currentTimeMillis()).apply() }

    // ---- PIN (nur als Prüfsumme) ----
    fun hasPin() = shared.contains("pin")
    fun setPin(pin: String) {
        val salt = java.util.UUID.randomUUID().toString()
        shared.edit().putString("pin_salt", salt).putString("pin", TvRules.pinHash(pin, salt)).apply()
    }
    /** Für die geschützte Übergabe an „Trendo Live TV": PIN nur als Prüfsumme + Salz. */
    fun pinRaw(): Pair<String, String>? = shared.getString("pin", null)?.let { it to (shared.getString("pin_salt", "") ?: "") }
    fun importPin(hash: String, salt: String) { shared.edit().putString("pin", hash).putString("pin_salt", salt).apply() }

    fun settingsJson(s: LiveSettings = settings()): String = JSONObject()
        .put("fav", JSONArray(s.favorites.toList())).put("hidden", JSONArray(s.hiddenGroups.toList())).put("locked", JSONArray(s.lockedGroups.toList()))
        .put("de", s.onlyGerman).put("adult", s.showAdult).put("kids", s.kidsMode).put("merge", s.mergeDuplicates).put("last", s.lastChannel ?: "").toString()

    fun settingsFromJson(json: String): LiveSettings = runCatching {
        val o = JSONObject(json)
        fun set(k: String) = o.optJSONArray(k)?.let { a -> (0 until a.length()).map { a.getString(it) }.toSet() } ?: emptySet()
        LiveSettings(set("fav"), set("hidden"), set("locked"), o.optBoolean("de"), o.optBoolean("adult"), o.optBoolean("kids"), o.optBoolean("merge", true),
            o.optString("last").ifBlank { null })
    }.getOrDefault(LiveSettings())

    fun checkPin(pin: String) = shared.getString("pin", null)?.let { it == TvRules.pinHash(pin, shared.getString("pin_salt", "")!!) } ?: true

    // ---- Erinnerungen ----
    fun reminders(): List<Reminder> = runCatching {
        val a = JSONArray(prefs.getString("rem", "[]"))
        (0 until a.length()).map { i -> a.getJSONObject(i).let { Reminder(it.getString("c"), it.getString("n"), it.getString("t"), it.getLong("s")) } }
    }.getOrDefault(emptyList())

    fun saveReminders(list: List<Reminder>) {
        val a = JSONArray(); list.forEach { r -> a.put(JSONObject().put("c", r.channelId).put("n", r.channelName).put("t", r.title).put("s", r.start)) }
        prefs.edit().putString("rem", a.toString()).apply()
    }
}
