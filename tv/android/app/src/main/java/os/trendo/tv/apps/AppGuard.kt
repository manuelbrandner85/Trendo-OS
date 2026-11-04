package os.trendo.tv.apps

/**
 * P0-Schutz: Trendo darf fremde Apps ausschließlich STARTEN oder deren Android-Infoseite ÖFFNEN.
 * Es gibt bewusst keine Aktion zum Deinstallieren, Deaktivieren, Leeren, Verstecken oder Anhalten.
 * Jede neue Aktion muss hier ausdrücklich ergänzt und getestet werden.
 */
enum class AppAction { LAUNCH, OPEN_SYSTEM_INFO, UNINSTALL, DISABLE, CLEAR_DATA, HIDE, SUSPEND, FORCE_STOP }

object AppGuard {
    private val allowed = setOf(AppAction.LAUNCH, AppAction.OPEN_SYSTEM_INFO)

    fun isAllowed(action: AppAction, packageName: String, ownPackage: String): Boolean =
        packageName.isNotBlank() && packageName != ownPackage && action in allowed

    /** Shell-/ADB-Befehle, die Trendo gegen fremde Pakete nie erzeugen oder ausführen darf. */
    val forbiddenCommands = listOf(
        "pm uninstall", "cmd package uninstall", "pm clear", "pm disable-user",
        "pm disable", "pm suspend", "pm hide", "am force-stop",
    )

    fun isForbiddenCommand(command: String): Boolean {
        val c = command.trim().lowercase().replace(Regex("\s+"), " ")
        return forbiddenCommands.any { c.startsWith(it) || c.contains(" $it") || c.contains(";$it") || c.contains("&&$it") }
    }
}
