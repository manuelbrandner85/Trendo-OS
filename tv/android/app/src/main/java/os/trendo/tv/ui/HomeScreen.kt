package os.trendo.tv.ui

import android.graphics.drawable.Drawable
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.graphics.drawable.toBitmap
import androidx.tv.material3.Border
import androidx.tv.material3.Card
import androidx.tv.material3.CardDefaults
import androidx.tv.material3.Glow
import androidx.tv.material3.Text
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.withContext
import os.trendo.tv.R
import os.trendo.tv.apps.AppEntry
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/** Eine Kachel der Startseite: entweder eine App oder eine Systemaktion. */
data class Tile(val key: String, val title: String, val app: AppEntry?, val onOpen: () -> Unit)

@Composable
fun HomeScreen(
    favorites: List<Tile>,
    apps: List<Tile>,
    system: List<Tile>,
    loadBanner: (String) -> Drawable?,
) {
    Column(
        Modifier
            .fillMaxSize()
            .background(TvColors.Background)
            .padding(horizontal = 48.dp, vertical = 27.dp)
    ) {
        Header()
        Spacer(Modifier.height(20.dp))
        LazyColumn(verticalArrangement = Arrangement.spacedBy(24.dp)) {
            if (favorites.isNotEmpty()) item { TileRow("Favoriten", favorites, loadBanner) }
            item { TileRow("Alle Apps", apps, loadBanner) }
            item { TileRow("System", system, loadBanner) }
        }
    }
}

@Composable
private fun Header() {
    Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.fillMaxWidth()) {
        Image(painterResource(R.drawable.ic_trendo_t), contentDescription = null, modifier = Modifier.size(56.dp))
        Spacer(Modifier.width(14.dp))
        Column {
            Text(stringResource(R.string.app_name), color = TvColors.Text, fontSize = 26.sp, fontWeight = FontWeight.Bold)
            Row(verticalAlignment = Alignment.Bottom) {
                Text(stringResource(R.string.edition), color = TvColors.Cyan, fontSize = 13.sp, letterSpacing = 2.sp)
                Spacer(Modifier.width(10.dp))
                Text(stringResource(R.string.credit), color = TvColors.TextSecondary, fontSize = 13.sp)
            }
        }
        Spacer(Modifier.weight(1f))
        Clock()
    }
}

@Composable
private fun Clock() {
    var now by remember { mutableStateOf(Date()) }
    LaunchedEffect(Unit) {
        // Einmal pro Minute aktualisieren, kein Dauer-Polling.
        while (true) {
            now = Date()
            delay(60_000L - System.currentTimeMillis() % 60_000L)
        }
    }
    Text(SimpleDateFormat("HH:mm", Locale.GERMANY).format(now), color = TvColors.Text, fontSize = 24.sp)
}

@Composable
private fun TileRow(title: String, tiles: List<Tile>, loadBanner: (String) -> Drawable?) {
    Column {
        Text(title, color = TvColors.TextSecondary, fontSize = 18.sp, fontWeight = FontWeight.SemiBold)
        Spacer(Modifier.height(10.dp))
        LazyRow(
            horizontalArrangement = Arrangement.spacedBy(18.dp),
            contentPadding = PaddingValues(vertical = 8.dp, horizontal = 4.dp),
        ) {
            items(tiles, key = { it.key }) { AppCard(it, loadBanner) }
        }
    }
}

@Composable
private fun AppCard(tile: Tile, loadBanner: (String) -> Drawable?) {
    val shape = RoundedCornerShape(10.dp)
    val banner by produceState<ImageBitmap?>(null, tile.app?.packageName) {
        val pkg = tile.app?.packageName ?: return@produceState
        // Bildgröße begrenzt (Ressourcenbudget), Laden außerhalb des UI-Threads.
        value = withContext(Dispatchers.IO) { loadBanner(pkg)?.toBitmap(320, 180)?.asImageBitmap() }
    }
    Card(
        onClick = tile.onOpen,
        modifier = Modifier.width(224.dp).height(126.dp),
        shape = CardDefaults.shape(shape),
        colors = CardDefaults.colors(containerColor = TvColors.Panel, focusedContainerColor = TvColors.Panel),
        scale = CardDefaults.scale(focusedScale = 1.08f),
        border = CardDefaults.border(focusedBorder = Border(BorderStroke(3.dp, TvColors.Prism), shape = shape)),
        glow = CardDefaults.glow(focusedGlow = Glow(elevationColor = TvColors.Violet, elevation = 12.dp)),
    ) {
        Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
            val img = banner
            if (img != null) {
                Image(img, contentDescription = tile.title, contentScale = ContentScale.Fit, modifier = Modifier.fillMaxSize())
            } else {
                Text(
                    tile.title, color = TvColors.Text, fontSize = 17.sp, maxLines = 2,
                    overflow = TextOverflow.Ellipsis, modifier = Modifier.padding(12.dp),
                )
            }
        }
    }
}
