package dev.scrobblix.app

import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.drawable.AdaptiveIconDrawable
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File
import java.io.FileOutputStream

// Launcher icon qualification: resolves the packaged launcher icon through
// PackageManager (the same path launchers use) on a real device and renders
// each adaptive layer to pixels. No Assume-skips: every assertion is hard.
class LauncherIconQualificationTest {

    private fun render(name: String, width: Int, height: Int, draw: (Canvas) -> Unit) {
        val bitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888)
        val canvas = Canvas(bitmap)
        draw(canvas)
        val dir = InstrumentationRegistry.getInstrumentation().targetContext.getExternalFilesDir(null)
            ?: error("external files dir unavailable")
        val out = File(dir, "scrobblix-$name.png")
        FileOutputStream(out).use { bitmap.compress(Bitmap.CompressFormat.PNG, 100, it) }
        assertTrue(out.isFile && out.length() > 0)
    }

    @Test
    fun packagedLauncherIconExposesAllAdaptiveLayers() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val icon = context.packageManager.getApplicationIcon(context.packageName)
        assertTrue("launcher icon must be adaptive, was ${icon.javaClass.name}", icon is AdaptiveIconDrawable)
        val adaptive = icon as AdaptiveIconDrawable
        assertNotNull("adaptive icon must have a background layer", adaptive.background)
        assertNotNull("adaptive icon must have a foreground layer", adaptive.foreground)

        // The composed icon exactly as packaged: background then foreground.
        render("launcher-icon-composed", 432, 432) { canvas ->
            adaptive.setBounds(0, 0, 432, 432)
            adaptive.draw(canvas)
        }

        if (android.os.Build.VERSION.SDK_INT >= 33) {
            val monochrome = adaptive.monochrome
            assertNotNull("adaptive icon must expose a monochrome layer for themed launchers", monochrome)
            // Themed-icon evidence: the system tints the monochrome silhouette.
            // Render the exact packaged monochrome asset tinted onto a dark surface,
            // the same drawable object the platform hands to launchers.
            render("launcher-icon-monochrome-tinted", 432, 432) { canvas ->
                canvas.drawColor(Color.rgb(16, 18, 32))
                val tinted = monochrome!!.constantState!!.newDrawable().mutate()
                tinted.setTint(Color.rgb(178, 157, 255))
                tinted.setBounds(0, 0, 432, 432)
                tinted.draw(canvas)
            }
        }
    }
}
