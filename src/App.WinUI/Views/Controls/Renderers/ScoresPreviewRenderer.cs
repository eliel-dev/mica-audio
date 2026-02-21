using Windows.UI;

namespace App.WinUI.Views.Controls.Renderers;

internal sealed class ScoresPreviewRenderer : IAppPreviewRenderer
{
    public string Kind => "scores";

    public void Draw(in AppPreviewRenderContext context)
    {
        var ds = context.DrawingSession;
        var baseline = context.Height * 0.75f;

        for (var i = 0; i < 10; i++)
        {
            var t = context.Time * 1.8f + i * 0.45f;
            var h = 8f + MathF.Abs(MathF.Sin(t)) * 30f;
            var x = 10f + i * ((context.Width - 20f) / 10f);
            var color = AppPreviewDrawHelpers.RainbowByFraction(i / 9f);
            ds.FillRectangle(x, baseline - h, 9f, h, color);
        }

        ds.DrawText("3 - 2", context.Width - 45f, 8f, Color.FromArgb(255, 245, 245, 245));
        ds.DrawText("AO VIVO", 10f, 8f, Color.FromArgb(255, 255, 105, 97));
    }
}
