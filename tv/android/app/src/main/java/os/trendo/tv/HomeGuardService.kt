package os.trendo.tv

import android.accessibilityservice.AccessibilityService
import android.content.Intent
import android.os.SystemClock
import android.view.accessibility.AccessibilityEvent
import os.trendo.tv.apps.AndroidApps
import os.trendo.tv.apps.FavoritesStore
import os.trendo.tv.apps.HomeGuardRules

/**
 * Startseiten-Wächter. Viele Android-TV-Geräte übernehmen eine andere Standard-Startseite nicht.
 * Dieser Dienst beobachtet ausschließlich, ob die Original-Startseite nach vorne kommt, und zeigt
 * dann TRENDO OS. Er liest keine Fensterinhalte (canRetrieveWindowContent=false) und ändert nichts
 * an anderen Apps. Ein- und Ausschalten macht der Nutzer in den Bedienungshilfen.
 */
class HomeGuardService : AccessibilityService() {
    private lateinit var store: FavoritesStore
    private var homes: Set<String> = emptySet()
    private val recent = ArrayDeque<Long>()

    override fun onServiceConnected() {
        store = FavoritesStore(this)
        homes = AndroidApps(this).originalHomePackages()
        // Nur Ereignisse der Original-Startseite(n) empfangen – keine anderen Apps.
        serviceInfo = serviceInfo.apply { packageNames = homes.toTypedArray() }
    }

    override fun onAccessibilityEvent(event: AccessibilityEvent?) {
        if (event?.eventType != AccessibilityEvent.TYPE_WINDOW_STATE_CHANGED) return
        val pkg = event.packageName?.toString() ?: return
        val now = SystemClock.elapsedRealtime()
        if (!HomeGuardRules.shouldBringTrendo(pkg, homes, store.originalHomeAllowed, recent, now)) return
        recent.addLast(now)
        startActivity(
            Intent(this, MainActivity::class.java)
                .addCategory(Intent.CATEGORY_HOME)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_REORDER_TO_FRONT)
        )
    }

    override fun onInterrupt() = Unit
}
