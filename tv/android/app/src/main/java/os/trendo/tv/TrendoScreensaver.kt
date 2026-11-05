package os.trendo.tv

import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.os.Handler
import android.os.Looper
import android.service.dreams.DreamService
import android.view.Gravity
import android.view.ViewGroup.LayoutParams.MATCH_PARENT
import android.view.ViewGroup.LayoutParams.WRAP_CONTENT
import android.widget.FrameLayout
import android.widget.ImageView
import android.widget.LinearLayout
import android.widget.TextView
import androidx.core.content.res.ResourcesCompat
import os.trendo.tv.weather.WeatherRepo
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/**
 * TRENDO-OS-Bildschirmschoner: eigene Landschaftsbilder mit weicher Überblendung und langsamem Zoom,
 * dazu Uhr, Datum und – falls ein Ort gewählt ist – das Wetter. Bewusst schlank (normale Views,
 * nur zwei Bilder gleichzeitig im Speicher), damit er auf Fernsehern mit wenig RAM flüssig bleibt.
 */
class TrendoScreensaver : DreamService() {
    private val images = intArrayOf(
        R.drawable.tv_saver_01, R.drawable.tv_saver_02, R.drawable.tv_saver_03, R.drawable.tv_saver_04,
        R.drawable.tv_saver_05, R.drawable.tv_saver_06, R.drawable.tv_saver_07, R.drawable.tv_saver_08,
        R.drawable.tv_saver_09, R.drawable.tv_saver_10, R.drawable.tv_saver_11, R.drawable.tv_saver_12, R.drawable.tv_hero,
    )
    private val handler = Handler(Looper.getMainLooper())
    private lateinit var front: ImageView
    private lateinit var back: ImageView
    private lateinit var clock: TextView
    private lateinit var date: TextView
    private lateinit var weather: TextView
    private var index = (System.currentTimeMillis() / 1000 % 13).toInt()

    override fun onAttachedToWindow() {
        super.onAttachedToWindow()
        isInteractive = false
        isFullscreen = true
        isScreenBright = false // dezent – der Fernseher bleibt im Bildschirmschoner gedimmt

        val root = FrameLayout(this).apply { setBackgroundColor(Color.BLACK) }
        back = ImageView(this).apply { scaleType = ImageView.ScaleType.CENTER_CROP; alpha = 0f }
        front = ImageView(this).apply { scaleType = ImageView.ScaleType.CENTER_CROP }
        root.addView(back, FrameLayout.LayoutParams(MATCH_PARENT, MATCH_PARENT))
        root.addView(front, FrameLayout.LayoutParams(MATCH_PARENT, MATCH_PARENT))

        // Lesbarkeit unten links
        root.addView(android.view.View(this).apply {
            background = GradientDrawable(GradientDrawable.Orientation.BOTTOM_TOP, intArrayOf(0xCC050A14.toInt(), 0x00050A14))
        }, FrameLayout.LayoutParams(MATCH_PARENT, dp(220), Gravity.BOTTOM))

        val script = ResourcesCompat.getFont(this, os.trendo.tv.shared.R.font.great_vibes)
        val box = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL; setPadding(dp(48), 0, 0, dp(36)) }
        clock = TextView(this).apply { textSize = 64f; setTextColor(0xFFF3F8FF.toInt()); typeface = Typeface.create("sans-serif-light", Typeface.NORMAL) }
        date = TextView(this).apply { textSize = 16f; setTextColor(0xFFB5C6DE.toInt()) }
        weather = TextView(this).apply { textSize = 15f; setTextColor(0xFFB5C6DE.toInt()) }
        val brand = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL; gravity = Gravity.BOTTOM; setPadding(0, dp(12), 0, 0)
            addView(ImageView(this@TrendoScreensaver).apply { setImageResource(os.trendo.tv.shared.R.drawable.ic_trendo_mark) }, LinearLayout.LayoutParams(dp(26), dp(26)))
            addView(TextView(this@TrendoScreensaver).apply { text = "  TRENDO OS  "; textSize = 13f; letterSpacing = 0.15f; setTextColor(0xFFF3F8FF.toInt()) })
            addView(TextView(this@TrendoScreensaver).apply { text = getString(os.trendo.tv.shared.R.string.credit); textSize = 16f; typeface = script; setTextColor(0xFFF3F8FF.toInt()) })
        }
        box.addView(clock); box.addView(date); box.addView(weather); box.addView(brand)
        root.addView(box, FrameLayout.LayoutParams(WRAP_CONTENT, WRAP_CONTENT, Gravity.BOTTOM or Gravity.START))
        setContentView(root)

        front.setImageResource(images[index])
        kenBurns(front)
        tickClock()
        loadWeather()
    }

    override fun onDreamingStarted() {
        super.onDreamingStarted()
        handler.postDelayed(::next, SLIDE_MS)
    }

    private fun next() {
        index = (index + 1) % images.size
        back.setImageResource(images[index])
        back.scaleX = 1f; back.scaleY = 1f
        back.animate().alpha(1f).setDuration(FADE_MS).start()
        front.animate().alpha(0f).setDuration(FADE_MS).withEndAction {
            // Rollen tauschen, altes Bild freigeben
            val old = front; front = back; back = old
            back.setImageDrawable(null); back.alpha = 0f
            kenBurns(front)
        }.start()
        handler.postDelayed(::next, SLIDE_MS)
    }

    private fun kenBurns(v: ImageView) {
        v.scaleX = 1f; v.scaleY = 1f
        v.animate().scaleX(1.08f).scaleY(1.08f).setDuration(SLIDE_MS + FADE_MS).start()
    }

    private fun tickClock() {
        val now = Date()
        clock.text = SimpleDateFormat("HH:mm", Locale.GERMANY).format(now)
        date.text = SimpleDateFormat("EEEE, d. MMMM yyyy", Locale.GERMANY).format(now)
        handler.postDelayed(::tickClock, 60_000L - System.currentTimeMillis() % 60_000L)
    }

    private fun loadWeather() {
        Thread {
            val w = runCatching { WeatherRepo(this).current() }.getOrNull()
            handler.post { weather.text = if (w != null) "${w.place} · ${w.tempC} °C · ${w.text}" else "" }
        }.start()
    }

    override fun onDreamingStopped() { super.onDreamingStopped(); handler.removeCallbacksAndMessages(null) }
    override fun onDetachedFromWindow() { super.onDetachedFromWindow(); handler.removeCallbacksAndMessages(null) }

    private fun dp(v: Int) = (v * resources.displayMetrics.density).toInt()

    companion object {
        const val SLIDE_MS = 20_000L
        const val FADE_MS = 2_500L
    }
}
