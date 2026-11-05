package os.trendo.tv.ui

import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.Font
import androidx.compose.ui.text.font.FontFamily
import os.trendo.tv.shared.R

/** Design-Tokens der TV-Edition (Übergabe 05_DESIGN + Vorlage vom 05.11.2026). Kein Gold. */
object TvColors {
    val Ink = Color(0xFF050A14)
    val DeepNavy = Color(0xFF071223)
    val Panel = Color(0xFF10233B)
    val Glass = Color(0x9910233B)
    val GlassStrong = Color(0xCC0B1A2E)
    val ElectricBlue = Color(0xFF317DFF)
    val Cyan = Color(0xFF1CD4F4)
    val Violet = Color(0xFF854FFF)
    val Magenta = Color(0xFFD74EDC)
    val Text = Color(0xFFF3F8FF)
    val TextSecondary = Color(0xFFB5C6DE)
    val Line = Color(0x33B5C6DE)

    val Prism = Brush.linearGradient(listOf(ElectricBlue, Cyan, Violet, Magenta))
    val FocusRing = Brush.linearGradient(listOf(Cyan, ElectricBlue, Violet))
    val Background = Brush.verticalGradient(listOf(DeepNavy, Ink))
}

/** Schreibschrift für den Marken-Credit „by Manuel" (Great Vibes, SIL OFL 1.1). */
val ScriptFont = FontFamily(Font(R.font.great_vibes))

/** Renderstufen; Performance ist Standard, bis Messungen am CHiQ etwas anderes erlauben. */
enum class RenderMode { PERFORMANCE, PREMIUM, ACCESSIBILITY }
