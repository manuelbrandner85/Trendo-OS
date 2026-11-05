package os.trendo.tv.phone

import android.content.Context
import android.net.ConnectivityManager
import java.io.BufferedReader
import java.io.InputStreamReader
import java.net.Inet4Address
import java.net.ServerSocket
import java.net.Socket
import java.net.URLDecoder
import java.security.SecureRandom

/**
 * „Vom Handy einfügen": kleine Eingabeseite im Heimnetz, nur solange der QR-Code angezeigt wird.
 * Schutz: zufälliger Einmal-Code in der Adresse, nur private Netzadressen, Abschalten nach der
 * ersten gültigen Eingabe oder nach 5 Minuten. Der Nutzer gibt die Daten selbst am Handy ein.
 */
class PhoneInput(private val context: Context, private val title: String, private val fields: List<Field>, private val onSubmit: (Map<String, String>) -> Unit) {

    data class Field(val key: String, val label: String, val password: Boolean = false)

    private var server: ServerSocket? = null
    val token: String = SecureRandom().let { r -> (1..20).map { "abcdefghjkmnpqrstuvwxyz23456789"[r.nextInt(31)] }.joinToString("") }
    var url: String? = null; private set

    fun start(): String? {
        val ip = localIp() ?: return null
        val s = ServerSocket(0)
        server = s
        val u = "http://$ip:${s.localPort}/$token"
        url = u
        Thread {
            val until = System.currentTimeMillis() + 5 * 60_000L
            s.soTimeout = 1000
            while (!s.isClosed && System.currentTimeMillis() < until) {
                val c = try { s.accept() } catch (_: Exception) { null } ?: continue
                runCatching { handle(c) }
                runCatching { c.close() }
            }
            stop()
        }.apply { isDaemon = true }.start()
        return u
    }

    fun stop() { runCatching { server?.close() }; server = null }

    private fun handle(c: Socket) {
        val remote = c.inetAddress
        if (!(remote.isSiteLocalAddress || remote.isLoopbackAddress || remote.isLinkLocalAddress)) { respond(c, 403, "Nur im Heimnetz."); return }
        c.soTimeout = 8000
        val input = BufferedReader(InputStreamReader(c.getInputStream(), Charsets.UTF_8))
        val requestLine = input.readLine() ?: return
        val parts = requestLine.split(' ')
        if (parts.size < 2) return
        val method = parts[0]; val path = parts[1]
        var length = 0
        while (true) {
            val h = input.readLine() ?: break
            if (h.isEmpty()) break
            if (h.startsWith("Content-Length:", true)) length = h.substringAfter(':').trim().toIntOrNull() ?: 0
        }
        if (!path.startsWith("/$token")) { respond(c, 404, "Unbekannt."); return }
        if (method == "POST") {
            val body = CharArray(length.coerceAtMost(16_384)).let { buf -> var n = 0; while (n < buf.size) { val r = input.read(buf, n, buf.size - n); if (r < 0) break; n += r }; String(buf, 0, n) }
            val values = body.split('&').mapNotNull { kv -> kv.split('=', limit = 2).takeIf { it.size == 2 } }
                .associate { URLDecoder.decode(it[0], "UTF-8") to URLDecoder.decode(it[1], "UTF-8").trim() }
            respond(c, 200, page(done = true))
            onSubmit(values.filterKeys { k -> fields.any { it.key == k } })
            stop()
        } else respond(c, 200, page(done = false))
    }

    private fun page(done: Boolean): String {
        val esc = { s: String -> s.replace("&", "&amp;").replace("<", "&lt;").replace("\"", "&quot;") }
        val form = fields.joinToString("") { f ->
            """<label>${esc(f.label)}<input name="${esc(f.key)}" type="${if (f.password) "password" else "text"}" autocomplete="off" autocapitalize="off" spellcheck="false"></label>"""
        }
        val inner = if (done) "<h2>Gesendet ✓</h2><p>Du kannst diese Seite schließen. Trendo lädt jetzt am Fernseher.</p>"
        else """<form method="post"><h2>${esc(title)}</h2>$form<button>Hinzufügen</button><p class="n">Nur für eigene, rechtmäßige Quellen. Die Daten gehen direkt an deinen Fernseher und werden dort verschlüsselt gespeichert.</p></form>"""
        return """<!doctype html><html lang="de"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>TRENDO OS</title>
<style>body{margin:0;font-family:system-ui,sans-serif;background:#050A14;color:#F3F8FF;padding:20px}h1{font-size:20px;letter-spacing:2px}h1 span{background:linear-gradient(90deg,#317DFF,#1CD4F4,#854FFF,#D74EDC);-webkit-background-clip:text;color:transparent}
label{display:block;margin:14px 0;font-size:14px;color:#B5C6DE}input{display:block;width:100%;box-sizing:border-box;margin-top:6px;padding:12px;border-radius:10px;border:1px solid #317DFF;background:#10233B;color:#F3F8FF;font-size:16px}
button{margin-top:10px;width:100%;padding:14px;border:0;border-radius:12px;background:linear-gradient(90deg,#317DFF,#854FFF);color:#fff;font-size:16px;font-weight:600}.n{font-size:12px;color:#B5C6DE}</style></head>
<body><h1><span>T</span> TRENDO OS</h1>$inner</body></html>"""
    }

    private fun respond(c: Socket, code: Int, html: String) {
        val bytes = html.toByteArray(Charsets.UTF_8)
        val status = when (code) { 200 -> "OK"; 403 -> "Forbidden"; else -> "Not Found" }
        c.getOutputStream().apply {
            write("HTTP/1.1 $code $status\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: ${bytes.size}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n".toByteArray())
            write(bytes); flush()
        }
    }

    private fun localIp(): String? = runCatching {
        val cm = context.getSystemService(ConnectivityManager::class.java)
        cm.getLinkProperties(cm.activeNetwork)?.linkAddresses?.map { it.address }?.firstOrNull { it is Inet4Address && it.isSiteLocalAddress }?.hostAddress
    }.getOrNull()
}
