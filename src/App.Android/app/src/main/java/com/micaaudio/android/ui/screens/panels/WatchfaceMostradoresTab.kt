package com.micaaudio.android.ui.screens.panels

// DOCS: docs/wiki/modules/paineis.md#editor-hub75

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.drawscope.DrawScope
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.delay
import java.time.LocalDateTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import kotlin.math.abs
import kotlin.math.cos
import kotlin.math.max
import kotlin.math.min
import kotlin.math.roundToInt
import kotlin.math.sin
import kotlin.math.sqrt

private const val PanelW = 128
private const val PanelH = 64

private data class MostradorOption(
    val value: String,
    val label: String,
    val description: String,
    val draw: DrawScope.(LocalDateTime) -> Unit,
)

private val MostradorOptions = listOf(
    MostradorOption("cyberterminal", "Cyber Terminal", "Terminal ciano com scanline e bordas tecnicas.", DrawScope::drawCyberTerminal),
    MostradorOption("flipclock", "Flip Clock Retro", "Dois paineis ambar com linha de flip animada.", DrawScope::drawFlipClock),
    MostradorOption("neotokyo", "Neo Tokyo Night", "Skyline neon magenta com placas e reflexo.", DrawScope::drawNeoTokyo),
    MostradorOption("relogiochuva", "Relogio Chuva", "Noite chuvosa com cidade, poste e guarda-chuva.", DrawScope::drawRelogioChuva),
    MostradorOption("aurora", "Aurora Minimalista", "Auroras, montanhas e floresta em pixel art.", DrawScope::drawAurora),
    MostradorOption("gridscifi", "Grid Sci-Fi", "Sala de grade em perspectiva com sweep sutil.", DrawScope::drawGridSciFi),
    MostradorOption("retroambar", "Retro Ambar", "Display ambar grande com frame e laterais.", DrawScope::drawRetroAmbar),
    MostradorOption("cosmico", "Cosmico", "Ceu espacial com planeta, estrelas e montanhas.", DrawScope::drawCosmico),
    MostradorOption("monocromatico", "Monocromatico", "Lua crescente, domo e reflexo em tons de cinza.", DrawScope::drawMonocromatico),
)

@Composable
fun MostradoresTab(
    selectedValue: String,
    onSelect: (String) -> Unit,
    modifier: Modifier = Modifier,
) {
    var now by remember { mutableStateOf(LocalDateTime.now(ZoneId.of("America/Sao_Paulo"))) }
    LaunchedEffect(Unit) {
        while (true) {
            delay(80L)
            now = LocalDateTime.now(ZoneId.of("America/Sao_Paulo"))
        }
    }

    LazyColumn(
        modifier = modifier.fillMaxSize(),
        contentPadding = PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        items(MostradorOptions.size) { i ->
            val option = MostradorOptions[i]
            MostradorCard(
                option = option,
                selected = option.value == selectedValue,
                now = now,
                onClick = { onSelect(option.value) },
            )
        }
    }
}

@Composable
private fun MostradorCard(
    option: MostradorOption,
    selected: Boolean,
    now: LocalDateTime,
    onClick: () -> Unit,
) {
    val accent = if (selected) MaterialTheme.colorScheme.primary else Color.Transparent
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick),
        colors = CardDefaults.cardColors(
            containerColor = if (selected) {
                MaterialTheme.colorScheme.primaryContainer
            } else {
                MaterialTheme.colorScheme.surfaceContainerHigh
            },
        ),
        shape = RoundedCornerShape(12.dp),
    ) {
        Column(Modifier.padding(12.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(option.label, fontWeight = FontWeight.Bold, fontSize = 15.sp)
                if (selected) {
                    Spacer(Modifier.width(8.dp))
                    Text("selecionado", fontSize = 11.sp, color = MaterialTheme.colorScheme.primary)
                }
            }
            Spacer(Modifier.height(2.dp))
            Text(option.description, fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Spacer(Modifier.height(8.dp))
            Box(
                Modifier
                    .fillMaxWidth()
                    .aspectRatio(128f / 64f)
                    .clip(RoundedCornerShape(4.dp))
                    .background(Color.Black)
                    .border(2.dp, accent, RoundedCornerShape(4.dp)),
                contentAlignment = Alignment.Center,
            ) {
                Canvas(Modifier.fillMaxSize()) {
                    val sx = size.width / PanelW
                    val sy = size.height / PanelH
                    scaleLogical(sx, sy) { with(option) { draw(now) } }
                }
            }
        }
    }
}

private inline fun DrawScope.scaleLogical(sx: Float, sy: Float, body: DrawScope.() -> Unit) {
    drawContext.canvas.save()
    drawContext.canvas.scale(sx, sy)
    body()
    drawContext.canvas.restore()
}

private fun DrawScope.drawCyberTerminal(now: LocalDateTime) {
    fillGridNoise(rgb(0, 10, 13), rgb(0, 18, 24), 5)
    matrixTexture(rgb(0, 24, 29), 7)
    rect(1, 1, 126, 62, Cyan)
    rect(3, 5, 122, 54, CyanDim)
    corner(5, 7, 12, Cyan); corner(111, 7, 12, Cyan); corner(5, 49, 12, Cyan); corner(111, 49, 12, Cyan)
    text5(7, 8, "> SYS ONLINE", Cyan)
    text5(80, 8, "UP ${now.format(DateTimeFormatter.ofPattern("HH:mm"))}", Cyan)
    hLine(8, 17, 112, CyanDim)
    hLine(13, 45, 102, CyanDim)
    val time = timeText(now)
    dotText(centerX(time, 4, 2), 21, time, Cyan, 4, 2, true)
    val scanY = 18 + (tick(now) / 28 % 25)
    hLine(12, scanY, 104, rgb(16, 98, 112))
    for (x in 12 until 116 step 3) px(x, scanY + 1, rgb(6, 55, 66))
    val date = dateText(now)
    text5((PanelW - textWidth(date)) / 2, 51, date, Cyan)
}

private fun DrawScope.drawFlipClock(now: LocalDateTime) {
    fillGradient(rgb(8, 5, 0), rgb(18, 8, 0))
    matrixTexture(rgb(34, 14, 0), 6)
    flipPanel(8, 8, 53, 39)
    flipPanel(67, 8, 53, 39)
    dotText(16, 15, now.format(DateTimeFormatter.ofPattern("HH")), Amber, 4, 3, true)
    dotText(75, 15, now.format(DateTimeFormatter.ofPattern("mm")), Amber, 4, 3, true)
    fillRect(62, 24, 3, 3, Amber); fillRect(62, 33, 3, 3, Amber)
    hLine(11, 29, 47, AmberDim); hLine(70, 29, 47, AmberDim)
    hLine(11, 30 + tick(now) / 110 % 2, 47, rgb(120, 62, 8))
    hLine(70, 30 + tick(now) / 110 % 2, 47, rgb(120, 62, 8))
    val date = "${dayPt(now)} ${"%02d".format(now.dayOfMonth)} ${monthEn(now.monthValue)} ${now.year}"
    val dw = textWidth(date)
    rect((PanelW - dw) / 2 - 4, 52, dw + 8, 10, rgb(78, 42, 6))
    text5((PanelW - dw) / 2, 54, date, Amber)
}

private fun DrawScope.drawNeoTokyo(now: LocalDateTime) {
    fillGradient(rgb(8, 3, 32), rgb(6, 0, 18))
    matrixTexture(rgb(15, 9, 54), 5)
    val horizon = 42
    hLine(0, horizon, PanelW, rgb(102, 31, 180))
    tokyoBuildings(horizon, now)
    reflections(horizon, 16, 1 + tick(now) / 120 % 5)
    neonSign(4, 12, true, now)
    neonSign(115, 17, false, now)
    val time = timeText(now)
    dotText(centerX(time, 4, 2), 10, time, if (tick(now) / 120 % 3 == 0) rgb(255, 86, 178) else HotPink, 4, 2, true)
}

private fun DrawScope.drawRelogioChuva(now: LocalDateTime) {
    fillGradient(rgb(4, 9, 24), rgb(5, 13, 34))
    matrixTexture(rgb(10, 23, 42), 6)
    val horizon = 47
    rainCity(horizon)
    lamp(12, 19)
    rain(horizon, now)
    val time = timeText(now)
    dotText(centerX(time, 4, 2), 14, time, White, 4, 2, false)
    text5(4, 55, dateText(now), rgb(205, 218, 232))
    umbrella(109, 53)
}

private fun DrawScope.drawAurora(now: LocalDateTime) {
    fillGradient(rgb(5, 13, 31), rgb(6, 19, 34))
    matrixTexture(rgb(10, 31, 48), 4)
    auroraBand(now, 4, 18, Green, 0.10, 8)
    auroraBand(now, 12, 16, rgb(36, 180, 170), 1.30, 6)
    auroraBand(now, 21, 12, rgb(99, 64, 228), 2.10, 5)
    auroraBand(now, 8, 12, rgb(154, 54, 189), 3.00, 4)
    mountains(45, rgb(5, 14, 23), rgb(11, 25, 32))
    forest(46)
    val time = timeText(now)
    dotText(centerX(time, 3, 2), 22, time, White, 3, 2, false)
    val date = dateText(now)
    text5((PanelW - textWidth(date)) / 2, 42, date, rgb(220, 236, 232))
}

private fun DrawScope.drawGridSciFi(now: LocalDateTime) {
    fillGridNoise(rgb(0, 0, 10), rgb(2, 6, 23), 8)
    val vpX = 64
    val vpY = 31
    val drift = tick(now) / 160 % 4
    rect(0, 0, 128, 64, GridBlue)
    line(0, 0, vpX, vpY, GridBlue); line(127, 0, vpX, vpY, GridBlue)
    line(0, 63, vpX, vpY, GridBlue); line(127, 63, vpX, vpY, GridBlue)
    listOf(2, 8, 16, 27, 37, 48, 56, 63).forEach { hLine(0, it, 128, if (it == 37) Cyan else GridBlue) }
    hLine(0, 35 + drift, 128, rgb(14, 120, 190))
    for (x in (tick(now) / 24 % 16) until 128 step 16) {
        px(x, 34 + drift, Cyan)
        px(x, 36 + drift, Cyan)
    }
    for (i in 0..8) {
        val x = i * 16
        val color = if (i % 2 == drift % 2) rgb(22, 100, 190) else GridBlue
        line(vpX, vpY, x, 0, color)
        line(vpX, vpY, x, 63, color)
    }
    val time = timeText(now)
    val x0 = centerX(time, 4, 2)
    fillRect(x0 - 4, 17, dotTextWidth(time, 4) + 8, 27, Color.Black)
    dotText(x0, 20, time, Cyan, 4, 2, true)
    val footer = ">> --- ${dateText(now)} --- <<"
    text5((PanelW - textWidth(footer)) / 2, 53, footer, rgb(49, 190, 220))
}

private fun DrawScope.drawRetroAmbar(now: LocalDateTime) {
    fillGridNoise(rgb(9, 4, 0), rgb(17, 8, 0), 7)
    matrixTexture(rgb(41, 18, 0), 5)
    rect(0, 0, 128, 64, rgb(110, 61, 8)); rect(2, 2, 124, 60, AmberDim)
    for (x in 8 until 52 step 2) {
        px(x, 8, Amber); px(127 - x, 8, Amber)
        px(x, 55, rgb(112, 65, 10)); px(127 - x, 55, rgb(112, 65, 10))
    }
    for (y in 20 until 39 step 3) {
        hLine(4, y, 3, Amber); hLine(121, y, 3, Amber)
    }
    val time = timeText(now)
    val pulse = if (tick(now) / 160 % 2 == 0) Amber else rgb(236, 136, 22)
    dotText(centerX(time, 5, 3), 17, time, pulse, 5, 3, true)
    val date = dateText(now)
    text5((PanelW - textWidth(date)) / 2, 48, date, Amber)
    for (i in 0 until 6) fillRect(47 + i * 7, 58, 2, 2, if (i == tick(now) / 120 % 6) Amber else AmberDim)
}

private fun DrawScope.drawCosmico(now: LocalDateTime) {
    fillGradient(rgb(3, 1, 24), rgb(12, 4, 45))
    matrixTexture(rgb(15, 9, 70), 5)
    starfield(now, 80, true)
    planet(now, 111, 13, 18, rgb(55, 33, 152), rgb(190, 68, 210), true)
    planet(now, 21, 35, 5, rgb(22, 15, 78), rgb(123, 44, 178), false)
    orbit(now, 25, 15, 10, rgb(245, 86, 188))
    mountains(51, rgb(12, 4, 36), rgb(50, 13, 82))
    val time = timeText(now)
    dotText(centerX(time, 4, 2), 27, time, White, 4, 2, false)
}

private fun DrawScope.drawMonocromatico(now: LocalDateTime) {
    fillGridNoise(Color.Black, rgb(7, 8, 8), 9)
    matrixTexture(rgb(22, 22, 24), 6)
    crescent(15, 14, 8)
    monoDome()
    monoReflection(now)
    starfield(now, 24, false)
    val time = timeText(now)
    dotText(centerX(time, 4, 2), 28, time, White, 4, 2, false)
    hLine(0, 50, 128, rgb(110, 114, 118)); hLine(0, 51, 128, rgb(42, 44, 48))
}

private fun DrawScope.flipPanel(x: Int, y: Int, w: Int, h: Int) {
    fillRect(x + 2, y + 2, w - 4, h - 4, rgb(18, 9, 0))
    rect(x, y, w, h, rgb(118, 61, 8))
    rect(x + 2, y + 2, w - 4, h - 4, rgb(73, 38, 5))
    px(x + 1, y + 1, Color.Black); px(x + w - 2, y + 1, Color.Black)
    px(x + 1, y + h - 2, Color.Black); px(x + w - 2, y + h - 2, Color.Black)
}

private fun DrawScope.tokyoBuildings(horizon: Int, now: LocalDateTime) {
    val heights = intArrayOf(13, 22, 10, 29, 17, 24, 15, 27, 18, 23, 12, 20, 16, 26, 14, 18, 21, 12, 17)
    heights.forEachIndexed { i, height ->
        val w = if (i % 4 == 0) 5 else 4
        val x = 17 + i * 5
        val y = horizon - height
        fillRect(x, y, w, height, rgb(5, 4, 20))
        vLine(x, y, height, rgb(36, 14, 70))
        for (wy in y + 2 until horizon - 1 step 3) {
            for (wx in x + 1 until x + w - 1 step 2) {
                if ((wx * 3 + wy + i) % 5 < 2) px(wx, wy, if ((wx + wy + tick(now) / 180) % 7 == 0) HotPink else rgb(46, 70, 210))
            }
        }
    }
}

private fun DrawScope.reflections(horizon: Int, height: Int, sway: Int) {
    for (y in 1..height) {
        val fade = 1f - y / (height + 1f)
        for (x in 0 until PanelW step 2) {
            val color = if ((x + y) % 5 == 0) rgb((130 * fade).roundToInt(), (35 * fade).roundToInt(), (170 * fade).roundToInt()) else rgb((40 * fade).roundToInt(), (30 * fade).roundToInt(), (110 * fade).roundToInt())
            px((x + sway) % PanelW, horizon + y - 1, color)
        }
    }
}

private fun DrawScope.neonSign(x: Int, y: Int, left: Boolean, now: LocalDateTime) {
    val color = if (tick(now) / 170 % 4 == 0) rgb(255, 96, 200) else rgb(214, 54, 190)
    rect(x, y, 9, 25, color)
    for (i in 0 until 3) {
        val yy = y + 4 + i * 6
        hLine(x + 2, yy, 5, color)
        vLine(x + 4 + if (left) i % 2 else 1 - i % 2, yy - 2, 5, color)
    }
}

private fun DrawScope.rainCity(horizon: Int) {
    val heights = intArrayOf(7, 12, 9, 15, 8, 17, 10, 13, 6, 15, 11, 8, 13, 9, 17, 7)
    heights.forEachIndexed { i, height ->
        val x = i * 8
        val y = horizon - height
        fillRect(x, y, 7, height + 16, rgb(3, 7, 17))
        if (i % 3 == 0) hLine(x + 1, y + height - 2, 5, rgb(8, 35, 72))
    }
}

private fun DrawScope.lamp(x: Int, y: Int) {
    vLine(x, y, 31, rgb(76, 79, 82)); hLine(x, y, 10, rgb(76, 79, 82))
    disc(x + 10, y, 2, rgb(255, 232, 128))
    for (dy in 1 until 20) hLine(x + 10 - dy / 2, y + dy, dy + 1, rgb(120 - dy * 3, 101 - dy * 3, 46))
}

private fun DrawScope.rain(horizon: Int, now: LocalDateTime) {
    for (i in 0 until 48) {
        val x = (i * 19 + tick(now) / 18) % 132 - 2
        val y = (i * 11 + tick(now) / 11) % horizon
        px(x, y, rgb(122, 164, 214)); px(x - 1, y + 1, rgb(68, 111, 170))
    }
}

private fun DrawScope.umbrella(x: Int, y: Int) {
    val color = rgb(192, 220, 236)
    hLine(x, y, 13, color); hLine(x + 1, y - 1, 11, color); hLine(x + 3, y - 2, 7, color)
    vLine(x + 6, y, 7, color); px(x + 5, y + 7, color); px(x + 4, y + 7, color); px(x + 3, y + 6, color)
}

private fun DrawScope.auroraBand(now: LocalDateTime, baseY: Int, height: Int, color: Color, phase: Double, amplitude: Int) {
    val motion = tick(now) / 1000.0
    for (x in 0 until PanelW) {
        val y0 = baseY + (sin(x * 0.09 + phase + motion) * amplitude).roundToInt()
        for (y in 0 until height) {
            val fade = 1f - y / height.toFloat()
            val stripe = if ((x + y + tick(now) / 80) % 6 == 0) 0.95f else 0.66f
            fillRect(x, y0 + y, 1, 1, scale(color, fade * stripe))
        }
    }
}

private fun DrawScope.mountains(baseY: Int, low: Color, high: Color) {
    val peaks = intArrayOf(10, 18, 7, 25, 14, 20, 9, 16, 28, 13, 21, 10, 24, 16, 19, 11)
    peaks.forEachIndexed { i, peakHeight ->
        val x0 = i * 8
        val peak = baseY - peakHeight
        for (x in 0 until 8) {
            val top = peak + abs(x - 4) * 2
            for (y in top until PanelH) px(x0 + x, y, lerp(high, low, ((y - top) / 22f).coerceIn(0f, 1f)))
        }
    }
}

private fun DrawScope.forest(groundY: Int) {
    for (x in -2 until 130 step 7) {
        val height = 10 + abs(x * 7 % 8)
        for (row in 0 until height) {
            val half = max(1, row / 3)
            hLine(x + 3 - half, groundY - height + row, half * 2 + 1, rgb(2, 12, 13))
        }
    }
}

private fun DrawScope.starfield(now: LocalDateTime, count: Int, colorful: Boolean) {
    for (i in 0 until count) {
        val x = (i * 67 + 23) % PanelW
        val y = (i * 43 + 11) % 44
        val blink = (i + tick(now) / 150) % 7 == 0
        val color = if (colorful) {
            if (blink) rgb(244, 96, 203) else if (i % 3 == 0) rgb(106, 92, 214) else rgb(176, 160, 214)
        } else {
            if (blink) rgb(220, 220, 224) else rgb(90, 92, 96)
        }
        px(x, y, color)
    }
}

private fun DrawScope.planet(now: LocalDateTime, cx: Int, cy: Int, r: Int, dark: Color, light: Color, ring: Boolean) {
    for (dy in -r..r) for (dx in -r..r) {
        if (dx * dx + dy * dy <= r * r) {
            val shade = ((dx + dy + r) / (r * 2f)).coerceIn(0f, 1f)
            val bands = if ((dy + tick(now) / 180) % 6 == 0) 0.22f else 0f
            px(cx + dx, cy + dy, lerp(light, dark, (shade + bands).coerceIn(0f, 1f)))
        }
    }
    circle(cx, cy, r, scale(light, 0.8f))
    if (ring) {
        for (x in -r - 6..r + 6) {
            val y = (sin(x * 0.18) * 3).roundToInt()
            px(cx + x, cy + y, rgb(202, 158, 76))
            if (x % 2 == 0) px(cx + x, cy + y + 1, rgb(108, 42, 158))
        }
    }
}

private fun DrawScope.orbit(now: LocalDateTime, cx: Int, cy: Int, radius: Int, color: Color) {
    val angle = tick(now) / 1000.0 * Math.PI * 2
    disc(cx + (cos(angle) * radius).roundToInt(), cy + (sin(angle) * 4).roundToInt(), 1, color)
}

private fun DrawScope.crescent(cx: Int, cy: Int, r: Int) {
    disc(cx, cy, r, rgb(160, 164, 168)); disc(cx + 4, cy - 2, r - 1, Color.Black); circle(cx, cy, r, rgb(225, 226, 228))
}

private fun DrawScope.monoDome() {
    val cx = 68
    val cy = 70
    val r = 35
    for (dy in -r..0) for (dx in -r..r) {
        if (dx * dx + dy * dy <= r * r) {
            val dist = sqrt((dx * dx + dy * dy).toFloat()) / r
            val shine = ((dy + r) / r.toFloat()).coerceIn(0f, 1f)
            px(cx + dx, cy + dy, lerp(rgb(18, 18, 20), rgb(116, 120, 124), dist * shine))
        }
    }
    for (angle in 180..360 step 2) {
        val rad = angle * Math.PI / 180.0
        px(cx + (cos(rad) * r).roundToInt(), cy + (sin(rad) * r).roundToInt(), rgb(226, 228, 230))
    }
}

private fun DrawScope.monoReflection(now: LocalDateTime) {
    val shimmer = tick(now) / 90 % 5
    for (y in 51 until PanelH) {
        val width = max(1, 26 - (y - 51) * 2)
        hLine(64 - width / 2 + (y + shimmer) % 3 - 1, y, width, rgb(78, 80, 84))
    }
}

private fun DrawScope.corner(x: Int, y: Int, len: Int, color: Color) {
    val right = x > 64
    val bottom = y > 32
    hLine(x, y, len, color)
    vLine(if (right) x + len - 1 else x, y, len, color)
    if (bottom) hLine(x, y + len - 1, len, color)
}

private fun DrawScope.fillGradient(top: Color, bottom: Color) {
    for (y in 0 until PanelH) hLine(0, y, PanelW, lerp(top, bottom, y / 63f))
}

private fun DrawScope.fillGridNoise(base: Color, alt: Color, stride: Int) {
    fillRect(0, 0, PanelW, PanelH, base)
    for (y in 0 until PanelH) for (x in 0 until PanelW) if ((x * 3 + y * 5) % stride == 0) px(x, y, alt)
}

private fun DrawScope.matrixTexture(color: Color, modulo: Int) {
    for (y in 0 until PanelH step 2) for (x in (y / 2) % 2 until PanelW step 2) if ((x + y) % modulo == 0) px(x, y, color)
    for (y in 1 until PanelH step 4) hLine(0, y, PanelW, Color.Black.copy(alpha = 0.18f))
}

private fun DrawScope.dotText(x: Int, y: Int, text: String, color: Color, pitch: Int, dotSize: Int, glow: Boolean): Int {
    var cursor = x
    text.uppercase().forEach { ch ->
        val glyph = Font5x7[ch]
        if (glyph == null) {
            cursor += pitch * 3
        } else {
            val charWidth = if (ch == ':') 2 else 5
            glyph.forEachIndexed { row, bits ->
                for (col in 0 until min(charWidth, bits.length)) {
                    if (bits[col] == '1') {
                        val px = cursor + col * pitch
                        val py = y + row * pitch
                        if (glow) {
                            fillRect(px - 1, py, 1, 1, scale(color, 0.28f))
                            fillRect(px + dotSize, py, 1, 1, scale(color, 0.28f))
                        }
                        fillRect(px, py, dotSize, dotSize, if ((row + col) % 2 == 0) color else scale(color, 0.9f))
                    }
                }
            }
            cursor += if (ch == ':') pitch * 2 else pitch * 6
        }
    }
    return cursor - x
}

private fun DrawScope.text5(x: Int, y: Int, text: String, color: Color) {
    var cursor = x
    text.uppercase().forEach { ch ->
        val glyph = Font5x7[ch] ?: Font5x7['-']!!
        glyph.forEachIndexed { row, bits ->
            bits.forEachIndexed { col, bit -> if (bit == '1') px(cursor + col, y + row, color) }
        }
        cursor += 6
    }
}

private fun dotTextWidth(text: String, pitch: Int): Int = max(0, text.length * pitch * 6 - pitch)
private fun centerX(text: String, pitch: Int, dotSize: Int): Int = max(0, (PanelW - dotTextWidth(text, pitch) - dotSize) / 2)
private fun textWidth(text: String): Int = max(0, text.length * 6 - 1)

private fun DrawScope.px(x: Int, y: Int, color: Color) {
    if (x !in 0 until PanelW || y !in 0 until PanelH) return
    drawRect(color = color, topLeft = Offset(x.toFloat(), y.toFloat()), size = Size(1f, 1f))
}

private fun DrawScope.fillRect(x: Int, y: Int, w: Int, h: Int, color: Color) {
    if (w <= 0 || h <= 0) return
    drawRect(color = color, topLeft = Offset(x.toFloat(), y.toFloat()), size = Size(w.toFloat(), h.toFloat()))
}

private fun DrawScope.hLine(x: Int, y: Int, len: Int, color: Color) = fillRect(x, y, len, 1, color)
private fun DrawScope.vLine(x: Int, y: Int, len: Int, color: Color) = fillRect(x, y, 1, len, color)
private fun DrawScope.rect(x: Int, y: Int, w: Int, h: Int, color: Color) {
    hLine(x, y, w, color); hLine(x, y + h - 1, w, color); vLine(x, y, h, color); vLine(x + w - 1, y, h, color)
}

private fun DrawScope.line(x0: Int, y0: Int, x1: Int, y1: Int, color: Color) {
    var cx = x0
    var cy = y0
    val dx = abs(x1 - x0)
    val sx = if (x0 < x1) 1 else -1
    val dy = -abs(y1 - y0)
    val sy = if (y0 < y1) 1 else -1
    var err = dx + dy
    while (true) {
        px(cx, cy, color)
        if (cx == x1 && cy == y1) break
        val e2 = 2 * err
        if (e2 >= dy) { err += dy; cx += sx }
        if (e2 <= dx) { err += dx; cy += sy }
    }
}

private fun DrawScope.disc(cx: Int, cy: Int, r: Int, color: Color) {
    for (dy in -r..r) for (dx in -r..r) if (dx * dx + dy * dy <= r * r) px(cx + dx, cy + dy, color)
}

private fun DrawScope.circle(cx: Int, cy: Int, r: Int, color: Color) {
    var x = r
    var y = 0
    var d = 1 - r
    while (x >= y) {
        px(cx + x, cy + y, color); px(cx - x, cy + y, color); px(cx + x, cy - y, color); px(cx - x, cy - y, color)
        px(cx + y, cy + x, color); px(cx - y, cy + x, color); px(cx + y, cy - x, color); px(cx - y, cy - x, color)
        if (d <= 0) d += 2 * ++y + 1 else d += 2 * (++y - --x) + 1
    }
}

private fun tick(now: LocalDateTime): Int = now.second * 1000 + now.nano / 1_000_000
private fun timeText(now: LocalDateTime): String = now.format(DateTimeFormatter.ofPattern("HH:mm"))
private fun dateText(now: LocalDateTime): String = "${dayPt(now)} ${now.format(DateTimeFormatter.ofPattern("dd/MM/yyyy"))}"
private fun dayPt(now: LocalDateTime): String = when (now.dayOfWeek.value) {
    1 -> "SEG"; 2 -> "TER"; 3 -> "QUA"; 4 -> "QUI"; 5 -> "SEX"; 6 -> "SAB"; else -> "DOM"
}
private fun monthEn(month: Int): String = listOf("JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC")[month - 1]

private fun rgb(r: Int, g: Int, b: Int): Color = Color(r.coerceIn(0, 255), g.coerceIn(0, 255), b.coerceIn(0, 255))
private fun scale(color: Color, amount: Float): Color = Color(color.red * amount, color.green * amount, color.blue * amount, color.alpha)
private fun lerp(a: Color, b: Color, t: Float): Color = Color(
    red = a.red + (b.red - a.red) * t.coerceIn(0f, 1f),
    green = a.green + (b.green - a.green) * t.coerceIn(0f, 1f),
    blue = a.blue + (b.blue - a.blue) * t.coerceIn(0f, 1f),
    alpha = a.alpha + (b.alpha - a.alpha) * t.coerceIn(0f, 1f),
)

private val Cyan = rgb(38, 224, 239)
private val CyanDim = rgb(8, 72, 86)
private val Amber = rgb(255, 176, 36)
private val AmberDim = rgb(82, 47, 8)
private val HotPink = rgb(255, 63, 158)
private val Green = rgb(38, 210, 126)
private val GridBlue = rgb(16, 73, 160)
private val White = rgb(245, 248, 250)

private val Font5x7: Map<Char, Array<String>> = mapOf(
    '0' to arrayOf("01110", "10001", "10011", "10101", "11001", "10001", "01110"),
    '1' to arrayOf("00100", "01100", "00100", "00100", "00100", "00100", "01110"),
    '2' to arrayOf("01110", "10001", "00001", "00010", "00100", "01000", "11111"),
    '3' to arrayOf("11110", "00001", "00001", "01110", "00001", "00001", "11110"),
    '4' to arrayOf("00010", "00110", "01010", "10010", "11111", "00010", "00010"),
    '5' to arrayOf("11111", "10000", "10000", "11110", "00001", "00001", "11110"),
    '6' to arrayOf("01110", "10000", "10000", "11110", "10001", "10001", "01110"),
    '7' to arrayOf("11111", "00001", "00010", "00100", "01000", "01000", "01000"),
    '8' to arrayOf("01110", "10001", "10001", "01110", "10001", "10001", "01110"),
    '9' to arrayOf("01110", "10001", "10001", "01111", "00001", "00001", "01110"),
    'A' to arrayOf("01110", "10001", "10001", "11111", "10001", "10001", "10001"),
    'B' to arrayOf("11110", "10001", "10001", "11110", "10001", "10001", "11110"),
    'C' to arrayOf("01110", "10001", "10000", "10000", "10000", "10001", "01110"),
    'D' to arrayOf("11100", "10010", "10001", "10001", "10001", "10010", "11100"),
    'E' to arrayOf("11111", "10000", "10000", "11110", "10000", "10000", "11111"),
    'F' to arrayOf("11111", "10000", "10000", "11110", "10000", "10000", "10000"),
    'G' to arrayOf("01110", "10001", "10000", "10111", "10001", "10001", "01110"),
    'H' to arrayOf("10001", "10001", "10001", "11111", "10001", "10001", "10001"),
    'I' to arrayOf("01110", "00100", "00100", "00100", "00100", "00100", "01110"),
    'J' to arrayOf("00111", "00010", "00010", "00010", "00010", "10010", "01100"),
    'K' to arrayOf("10001", "10010", "10100", "11000", "10100", "10010", "10001"),
    'L' to arrayOf("10000", "10000", "10000", "10000", "10000", "10000", "11111"),
    'M' to arrayOf("10001", "11011", "10101", "10101", "10001", "10001", "10001"),
    'N' to arrayOf("10001", "11001", "10101", "10011", "10001", "10001", "10001"),
    'O' to arrayOf("01110", "10001", "10001", "10001", "10001", "10001", "01110"),
    'P' to arrayOf("11110", "10001", "10001", "11110", "10000", "10000", "10000"),
    'Q' to arrayOf("01110", "10001", "10001", "10001", "10101", "10010", "01101"),
    'R' to arrayOf("11110", "10001", "10001", "11110", "10100", "10010", "10001"),
    'S' to arrayOf("01111", "10000", "10000", "01110", "00001", "00001", "11110"),
    'T' to arrayOf("11111", "00100", "00100", "00100", "00100", "00100", "00100"),
    'U' to arrayOf("10001", "10001", "10001", "10001", "10001", "10001", "01110"),
    'V' to arrayOf("10001", "10001", "10001", "10001", "10001", "01010", "00100"),
    'W' to arrayOf("10001", "10001", "10001", "10101", "10101", "10101", "01010"),
    'X' to arrayOf("10001", "10001", "01010", "00100", "01010", "10001", "10001"),
    'Y' to arrayOf("10001", "10001", "01010", "00100", "00100", "00100", "00100"),
    'Z' to arrayOf("11111", "00001", "00010", "00100", "01000", "10000", "11111"),
    ':' to arrayOf("00000", "00100", "00100", "00000", "00100", "00100", "00000"),
    '/' to arrayOf("00001", "00010", "00010", "00100", "01000", "01000", "10000"),
    '-' to arrayOf("00000", "00000", "00000", "01110", "00000", "00000", "00000"),
    '>' to arrayOf("10000", "01000", "00100", "00010", "00100", "01000", "10000"),
    ' ' to arrayOf("00000", "00000", "00000", "00000", "00000", "00000", "00000"),
)
