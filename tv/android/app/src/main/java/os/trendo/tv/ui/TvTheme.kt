package os.trendo.tv.ui

import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color

/** Design-Tokens der TV-Edition (laut Übergabe 05_DESIGN/DESIGN_SYSTEM.md). Kein Gold. */
object TvColors {
    val Ink = Color(0xFF050A14)
    val DeepNavy = Color(0xFF071223)
    val Panel = Color(0xFF10233B)
    val ElectricBlue = Color(0xFF317DFF)
    val Cyan = Color(0xFF1CD4F4)
    val Violet = Color(0xFF854FFF)
    val Magenta = Color(0xFFD74EDC)
    val Text = Color(0xFFF3F8FF)
    val TextSecondary = Color(0xFFB5C6DE)

    val Prism = Brush.linearGradient(listOf(ElectricBlue, Cyan, Violet, Magenta))
    val Background = Brush.verticalGradient(listOf(DeepNavy, Ink))
}

/** Renderstufen; Performance ist Standard, bis Messungen am CHiQ etwas anderes erlauben. */
enum class RenderMode { PERFORMANCE, PREMIUM, ACCESSIBILITY }
