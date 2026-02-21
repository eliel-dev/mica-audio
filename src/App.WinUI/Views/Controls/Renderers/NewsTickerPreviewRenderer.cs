using Windows.UI;

namespace App.WinUI.Views.Controls.Renderers;

internal sealed class NewsTickerPreviewRenderer : IAppPreviewRenderer
{
    public string Kind => "news";

    public void Draw(in AppPreviewRenderContext context)
    {
        var ds = context.DrawingSession;

        var lineY = context.Height * 0.55f;
        ds.DrawLine(0f, lineY, context.Width, lineY, Color.FromArgb(200, 83, 109, 140), 1f);

        var message = "ULTIMAS: mercado em alta e clima estavel na semana";
        var speed = 62f;
        var textWidth = MathF.Max(context.Width * 1.6f, 300f);
        var x = context.Width - ((context.Time * speed) % textWidth);

        ds.DrawText(message, x, lineY - 12f, Color.FromArgb(255, 245, 245, 245));
        ds.DrawText("NEWS", 8f, 8f, AppPreviewDrawHelpers.AccentToColor(context.Item.Preview?.Accent, Color.FromArgb(255, 255, 171, 64)));
    }
}
