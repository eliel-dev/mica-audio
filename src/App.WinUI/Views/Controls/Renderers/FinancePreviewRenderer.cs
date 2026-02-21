using Windows.UI;

namespace App.WinUI.Views.Controls.Renderers;

internal sealed class FinancePreviewRenderer : IAppPreviewRenderer
{
    public string Kind => "finance";

    public void Draw(in AppPreviewRenderContext context)
    {
        var ds = context.DrawingSession;
        var startX = 8f;
        var width = context.Width - 16f;

        for (var i = 0; i < 24; i++)
        {
            var fraction = i / 23f;
            var x = startX + fraction * width;
            var y = context.Height * 0.62f - MathF.Sin(context.Time * 1.35f + i * 0.35f) * 18f - MathF.Cos(i * 0.12f) * 6f;

            if (i > 0)
            {
                var prevFraction = (i - 1) / 23f;
                var prevX = startX + prevFraction * width;
                var prevY = context.Height * 0.62f - MathF.Sin(context.Time * 1.35f + (i - 1) * 0.35f) * 18f - MathF.Cos((i - 1) * 0.12f) * 6f;
                ds.DrawLine(prevX, prevY, x, y, AppPreviewDrawHelpers.AccentToColor(context.Item.Preview?.Accent, Color.FromArgb(255, 102, 210, 146)), 2f);
            }
        }

        ds.DrawText("R$ 8.742", 10f, 8f, Color.FromArgb(255, 245, 245, 245));
        ds.DrawText("+1.8%", context.Width - 46f, 8f, Color.FromArgb(255, 97, 232, 140));
    }
}
