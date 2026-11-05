package os.trendo.tv.ui

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Shape
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.tv.material3.Border
import androidx.tv.material3.ClickableSurfaceDefaults
import androidx.tv.material3.Glow
import androidx.tv.material3.Icon
import androidx.tv.material3.Surface
import androidx.tv.material3.Text

/** Fokussierbare Fläche mit farbigem Fokusrahmen und leichtem Leuchten – Grundbaustein jeder Kachel. */
@Composable
fun FocusSurface(
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    shape: Shape = RoundedCornerShape(10.dp),
    container: Color = TvColors.Glass,
    focusedContainer: Color = TvColors.Panel,
    scale: Float = 1.06f,
    ring: Dp = 2.dp,
    idleBorder: Boolean = true,
    onLongClick: (() -> Unit)? = null,
    content: @Composable () -> Unit,
) {
    Surface(
        onClick = onClick,
        onLongClick = onLongClick,
        modifier = modifier,
        shape = ClickableSurfaceDefaults.shape(shape),
        colors = ClickableSurfaceDefaults.colors(
            containerColor = container, focusedContainerColor = focusedContainer,
            contentColor = TvColors.Text, focusedContentColor = TvColors.Text,
        ),
        scale = ClickableSurfaceDefaults.scale(focusedScale = scale),
        border = ClickableSurfaceDefaults.border(
            border = if (idleBorder) Border(BorderStroke(1.dp, TvColors.Line), shape = shape) else Border.None,
            focusedBorder = Border(BorderStroke(ring, TvColors.FocusRing), shape = shape),
        ),
        glow = ClickableSurfaceDefaults.glow(focusedGlow = Glow(elevationColor = TvColors.Cyan.copy(alpha = 0.55f), elevation = 10.dp)),
    ) { content() }
}

/** Kleiner Pillen-Knopf wie „Jetzt entdecken >". */
@Composable
fun PillButton(text: String, onClick: () -> Unit, modifier: Modifier = Modifier, icon: ImageVector? = null) {
    val shape = RoundedCornerShape(50)
    FocusSurface(onClick, modifier, shape = shape, container = Color(0x66050A14), scale = 1.05f) {
        Row(Modifier.padding(horizontal = 14.dp, vertical = 6.dp), verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(6.dp)) {
            Text(text, fontSize = 11.sp, color = TvColors.Text)
            if (icon != null) Icon(icon, contentDescription = null, tint = TvColors.Text, modifier = Modifier.size(12.dp))
        }
    }
}

@Composable
fun IconLabel(icon: ImageVector, label: String, tint: Color = TvColors.Text, iconSize: Dp = 18.dp, fontSize: Int = 13) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Icon(icon, contentDescription = null, tint = tint, modifier = Modifier.size(iconSize))
        Spacer(Modifier.width(10.dp))
        Text(label, color = TvColors.Text, fontSize = fontSize.sp)
    }
}
