package os.trendo.tv.apps

/** Reine Entscheidungsregeln des Startseiten-Wächters (testbar ohne Android). */
object HomeGuardRules {
    /** Schutz gegen Schleifen: höchstens so viele Sprünge in [WINDOW_MS], danach Pause. */
    const val MAX_JUMPS = 3
    const val WINDOW_MS = 10_000L

    fun shouldBringTrendo(pkg: String, homes: Set<String>, originalAllowed: Boolean, recent: ArrayDeque<Long>, now: Long): Boolean {
        if (pkg !in homes) return false
        if (originalAllowed) return false
        while (recent.isNotEmpty() && now - recent.first() > WINDOW_MS) recent.removeFirst()
        return recent.size < MAX_JUMPS
    }
}
