namespace App.WinUI.Views.Controls.Renderers;

internal sealed class DecorativePreviewRenderer : IAppPreviewRenderer
{
    public string Kind => "decorative";

    public void Draw(in AppPreviewRenderContext context)
    {
        var ds = context.DrawingSession;

        var bars = 28;
        var centerY = context.Height * 0.5f;
        for (var i = 0; i < bars; i++)
        {
            var fraction = i / (float)(bars - 1);
            var x = 8f + fraction * (context.Width - 16f);
            var dynamic = MathF.Abs(MathF.Sin(context.Time * 1.7f + i * 0.48f));
            var h = 2f + dynamic * (context.Height * 0.42f);
            var color = AppPreviewDrawHelpers.RainbowByFraction(fraction);
            ds.DrawLine(x, centerY - h * 0.5f, x, centerY + h * 0.5f, color, context.IsSelected ? 2.4f : 1.8f);
        }
    }
}
