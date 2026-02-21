using Windows.UI;

namespace App.WinUI.Views.Controls.Renderers;

internal sealed class ProductivityPreviewRenderer : IAppPreviewRenderer
{
    public string Kind => "productivity";

    public void Draw(in AppPreviewRenderContext context)
    {
        var ds = context.DrawingSession;

        ds.DrawText("Meta do ano", 10f, 8f, Color.FromArgb(255, 245, 245, 245));
        var progress = 0.25f + (MathF.Sin(context.Time * 0.35f) * 0.1f + 0.1f);
        progress = Math.Clamp(progress, 0f, 1f);

        var barX = 10f;
        var barY = context.Height * 0.52f;
        var barW = context.Width - 20f;
        var barH = 12f;

        ds.DrawRectangle(barX, barY, barW, barH, Color.FromArgb(220, 88, 106, 134), 1f);
        ds.FillRectangle(barX + 1f, barY + 1f, (barW - 2f) * progress, barH - 2f, AppPreviewDrawHelpers.AccentToColor(context.Item.Preview?.Accent, Color.FromArgb(255, 92, 184, 92)));
        ds.DrawText($"{(int)(progress * 100f)}%", context.Width - 45f, barY - 20f, Color.FromArgb(255, 245, 245, 245));
    }
}
