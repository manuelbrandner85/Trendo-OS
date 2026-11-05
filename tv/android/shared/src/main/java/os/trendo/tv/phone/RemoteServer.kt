package os.trendo.tv.phone

import android.content.Context
import android.net.ConnectivityManager
import org.json.JSONArray
import org.json.JSONObject
import java.io.BufferedReader
import java.io.InputStreamReader
import java.net.Inet4Address
import java.net.ServerSocket
import java.net.Socket
import java.net.URLDecoder
import java.security.SecureRandom

/** Was das Handy am Fernseher auslösen darf – nur Aktionen innerhalb von Trendo Live TV. */
interface RemoteHost {
    fun channels(query: String): List<Triple<String, String, String>>  // id, Name, „jetzt läuft"
    fun play(id: String): Boolean
    fun key(name: String): Boolean        // up, down, left, right, ok, back, chup, chdown, playpause
    fun volume(up: Boolean?)              // null = stumm umschalten
}

/** Reine Regeln für die Kopplung (testbar ohne Netz). */
object RemotePairing {
    private val rnd = SecureRandom()
    fun newToken(len: Int = 32): String = (1..len).map { "abcdefghjkmnpqrstuvwxyz23456789"[rnd.nextInt(31)] }.joinToString("")

    /** Nur gekoppelte Geräte (Token in der Liste) und nur aus dem eigenen Heimnetz. */
    fun allowed(token: String?, paired: Set<String>, siteLocal: Boolean): Boolean = siteLocal && token != null && token.length >= 24 && token in paired

    /** Nur diese Tasten gibt es – alles andere wird abgewiesen. */
    val keys = setOf("up", "down", "left", "right", "ok", "back", "chup", "chdown", "playpause")
}

/**
 * Handy als zweiter Bildschirm: kleiner Webserver im Heimnetz, solange Trendo Live TV offen ist.
 * Erst nach Kopplung per QR-Code (Einmal-Code, 5 min gültig) werden Befehle angenommen.
 */
class RemoteServer(private val context: Context, private val host: RemoteHost) {
    private val prefs = context.getSharedPreferences("trendo_live_remote", Context.MODE_PRIVATE)
    private var server: ServerSocket? = null
    @Volatile private var pairCode: String? = null
    @Volatile private var pairUntil = 0L
    var baseUrl: String? = null; private set

    val paired: Set<String> get() = prefs.getStringSet("paired", emptySet())!!.toSet()
    fun forgetAll() { prefs.edit().remove("paired").apply() }

    /** Startet den Server (Port bleibt gleich, damit gekoppelte Handys ihn wiederfinden). */
    fun start() {
        if (server != null) return
        val ip = localIp() ?: return
        val port = prefs.getInt("port", 0)
        val s = runCatching { ServerSocket(port) }.getOrElse { ServerSocket(0) }
        prefs.edit().putInt("port", s.localPort).apply()
        server = s; baseUrl = "http://$ip:${s.localPort}"
        Thread {
            while (!s.isClosed) {
                val c = runCatching { s.accept() }.getOrNull() ?: continue
                Thread { runCatching { handle(c) }; runCatching { c.close() } }.apply { isDaemon = true }.start()
            }
        }.apply { isDaemon = true }.start()
    }

    fun stop() { runCatching { server?.close() }; server = null }

    /** Neuer Kopplungs-Link für den QR-Code (5 Minuten gültig). */
    fun pairingUrl(): String? {
        start()
        val code = RemotePairing.newToken(20); pairCode = code; pairUntil = System.currentTimeMillis() + 5 * 60_000L
        return baseUrl?.let { "$it/pair/$code" }
    }

    private fun handle(c: Socket) {
        val remote = c.inetAddress
        val local = remote.isSiteLocalAddress || remote.isLoopbackAddress || remote.isLinkLocalAddress
        c.soTimeout = 8000
        val input = BufferedReader(InputStreamReader(c.getInputStream(), Charsets.UTF_8))
        val parts = (input.readLine() ?: return).split(' ')
        if (parts.size < 2) return
        val method = parts[0]; val path = parts[1]
        var length = 0
        while (true) { val h = input.readLine() ?: break; if (h.isEmpty()) break; if (h.startsWith("Content-Length:", true)) length = h.substringAfter(':').trim().toIntOrNull() ?: 0 }
        val body = if (method == "POST") CharArray(length.coerceAtMost(4096)).let { b -> var n = 0; while (n < b.size) { val r = input.read(b, n, b.size - n); if (r < 0) break; n += r }; String(b, 0, n) } else ""

        if (!local) { respond(c, 403, "text/plain", "Nur im Heimnetz."); return }
        // Kopplung: /pair/<code> → Token anlegen, Seite mit Token ausliefern
        if (path.startsWith("/pair/")) {
            val code = path.removePrefix("/pair/")
            if (code == pairCode && System.currentTimeMillis() < pairUntil) {
                val t = RemotePairing.newToken()
                prefs.edit().putStringSet("paired", paired + t).apply()
                pairCode = null
                respond(c, 302, "text/plain", "", "Location: /r/$t/")
            } else respond(c, 403, "text/html; charset=utf-8", page("Kopplung abgelaufen", "<p>Bitte am Fernseher einen neuen QR-Code öffnen.</p>"))
            return
        }
        // Alles andere: /r/<token>/…
        val seg = path.split('/', '?').filter { it.isNotEmpty() }
        if (seg.size < 2 || seg[0] != "r" || !RemotePairing.allowed(seg[1], paired, local)) { respond(c, 403, "text/plain", "Nicht gekoppelt."); return }
        val route = seg.drop(2).joinToString("/")
        when {
            route.isEmpty() -> respond(c, 200, "text/html; charset=utf-8", app())
            route == "api/channels" -> {
                val q = path.substringAfter("q=", "").substringBefore('&').let { URLDecoder.decode(it, "UTF-8") }
                val a = JSONArray(); host.channels(q).take(200).forEach { (id, n, now) -> a.put(JSONObject().put("id", id).put("n", n).put("now", now)) }
                respond(c, 200, "application/json", a.toString())
            }
            route == "api/play" && method == "POST" -> respond(c, 200, "application/json", JSONObject().put("ok", host.play(JSONObject(body).optString("id"))).toString())
            route == "api/key" && method == "POST" -> {
                val k = JSONObject(body).optString("k")
                respond(c, 200, "application/json", JSONObject().put("ok", k in RemotePairing.keys && host.key(k)).toString())
            }
            route == "api/vol" && method == "POST" -> {
                when (JSONObject(body).optString("v")) { "up" -> host.volume(true); "down" -> host.volume(false); "mute" -> host.volume(null) }
                respond(c, 200, "application/json", "{\"ok\":true}")
            }
            else -> respond(c, 404, "text/plain", "Unbekannt.")
        }
    }

    private fun page(title: String, inner: String) = """<!doctype html><html lang="de"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>$title</title>
<style>body{margin:0;font-family:system-ui,sans-serif;background:#050A14;color:#F3F8FF;padding:18px}h1{font-size:18px;letter-spacing:2px}h1 span{background:linear-gradient(90deg,#317DFF,#1CD4F4,#854FFF,#D74EDC);-webkit-background-clip:text;color:transparent}</style></head>
<body><h1><span>T</span> TRENDO LIVE TV</h1>$inner</body></html>"""

    private fun app() = page("Trendo Live TV – Handy", """
<style>.pad{display:grid;grid-template-columns:repeat(3,1fr);gap:10px;max-width:330px;margin:10px auto}
button{padding:16px;border:0;border-radius:14px;background:#10233B;color:#F3F8FF;font-size:18px}button.ok{background:linear-gradient(135deg,#317DFF,#854FFF);font-weight:700}
.row{display:flex;gap:8px;justify-content:center;flex-wrap:wrap;margin:8px 0}.row button{flex:1;min-width:90px;font-size:14px}
input{width:100%;box-sizing:border-box;padding:12px;border-radius:12px;border:1px solid #317DFF;background:#10233B;color:#F3F8FF;font-size:16px;margin:10px 0}
.ch{padding:12px;border-bottom:1px solid #1d3354}.ch b{display:block}.ch small{color:#B5C6DE}.tabs{display:flex;gap:8px}.tabs button{flex:1;font-size:14px;padding:10px}</style>
<div class="tabs"><button onclick="show('fb')">Fernbedienung</button><button onclick="show('sl')">Sender</button></div>
<div id="fb"><div class="pad"><span></span><button onclick="k('up')">▲</button><span></span><button onclick="k('left')">◀</button><button class="ok" onclick="k('ok')">OK</button><button onclick="k('right')">▶</button><span></span><button onclick="k('down')">▼</button><span></span></div>
<div class="row"><button onclick="k('back')">Zurück</button><button onclick="k('playpause')">⏯</button></div>
<div class="row"><button onclick="k('chdown')">Sender −</button><button onclick="k('chup')">Sender +</button></div>
<div class="row"><button onclick="v('down')">Leiser</button><button onclick="v('mute')">Stumm</button><button onclick="v('up')">Lauter</button></div></div>
<div id="sl" style="display:none"><input id="q" placeholder="Sender suchen …" oninput="load()"><div id="list"></div></div>
<script>
function post(p,b){return fetch(p,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(b)})}
function k(x){post('api/key',{k:x})} function v(x){post('api/vol',{v:x})}
function show(id){document.getElementById('fb').style.display=id=='fb'?'':'none';document.getElementById('sl').style.display=id=='sl'?'':'none';if(id=='sl')load()}
let t;function load(){clearTimeout(t);t=setTimeout(async()=>{const q=document.getElementById('q').value;const r=await fetch('api/channels?q='+encodeURIComponent(q));const a=await r.json();
const l=document.getElementById('list');l.innerHTML='';a.forEach(c=>{const d=document.createElement('div');d.className='ch';const b=document.createElement('b');b.textContent=c.n;const s=document.createElement('small');s.textContent=c.now||'';
d.appendChild(b);d.appendChild(s);d.onclick=()=>post('api/play',{id:c.id});l.appendChild(d)})},250)}
</script>""")

    private fun respond(c: Socket, code: Int, type: String, body: String, extra: String = "") {
        val bytes = body.toByteArray(Charsets.UTF_8)
        val status = mapOf(200 to "OK", 302 to "Found", 403 to "Forbidden", 404 to "Not Found")[code] ?: "OK"
        c.getOutputStream().apply {
            write(("HTTP/1.1 $code $status\r\nContent-Type: $type\r\nContent-Length: ${bytes.size}\r\nCache-Control: no-store\r\nConnection: close\r\n" +
                (if (extra.isNotEmpty()) "$extra\r\n" else "") + "\r\n").toByteArray())
            write(bytes); flush()
        }
    }

    private fun localIp(): String? = runCatching {
        val cm = context.getSystemService(ConnectivityManager::class.java)
        cm.getLinkProperties(cm.activeNetwork)?.linkAddresses?.map { it.address }?.firstOrNull { it is Inet4Address && it.isSiteLocalAddress }?.hostAddress
    }.getOrNull()
}
