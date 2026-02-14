using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

public sealed class WaterfallRenderer : IRenderer
{
    private readonly Queue<float[]> history = new();

    public string RendererId => RendererIds.Waterfall;

    public string DisplayName => "Waterfall";

    public void Render(RenderContext context)
    {
        var ds = context.DrawingSession;
        var bands = context.Frame.BandsDisplay;
        if (bands.Length == 0)
        {
            return;
        }

        history.Enqueue((float[])bands.Clone());
        var maxRows = Math.Max(32, (int)(context.Height / 2f));
        while (history.Count > maxRows)
        {
            history.Dequeue();
        }

        var rowHeight = context.Height / maxRows;
        var stepX = context.Width / Math.Max(1, bands.Length - 1);

        var rowIndex = 0;
        foreach (var row in history.Reverse())
        {
            var yOffset = rowIndex * rowHeight;
            for (var i = 1; i < row.Length; i++)
            {
                var x0 = (i - 1) * stepX;
                var x1 = i * stepX;
                var y = context.Height - yOffset;
                var h0 = row[i - 1] * rowHeight * 0.9f;
                var h1 = row[i] * rowHeight * 0.9f;
                var fade = 1f - (rowIndex / (float)maxRows);
                var color = context.Palette.ColorAt(i / (float)(row.Length - 1), fade * 0.8f);
                ds.DrawLine(x0, y - h0, x1, y - h1, color, 1.4f);
            }

            rowIndex++;
            if (rowIndex >= maxRows)
            {
                break;
            }
        }
    }
}
