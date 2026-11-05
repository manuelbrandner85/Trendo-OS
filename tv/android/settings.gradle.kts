pluginManagement {
    repositories { google(); mavenCentral(); gradlePluginPortal() }
}
dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories { google(); mavenCentral() }
}
rootProject.name = "TrendoTV"
// app = TRENDO OS (Startseite), live = Trendo Live TV (eigene App), shared = gemeinsamer Code
include(":app", ":live", ":shared")
