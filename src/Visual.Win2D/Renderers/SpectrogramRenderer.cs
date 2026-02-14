using Visual.Win2D.Engine;

namespace Visual.Win2D.Renderers;

public sealed class SpectrogramRenderer : IRenderer
{
    private readonly Queue<float[]> history = new();

    public string RendererId => RendererIds.Spectrogram;

    public string DisplayName => "Spectrogram";

    public void Render(RenderContext context)
    {
        var ds = context.DrawingSession;
        var bands = context.Frame.BandsDisplay;
        if (bands.Length == 0)
        {
            return;
        }

        history.Enqueue((float[])bands.Clone());

        var maxColumns = Math.Max(32, (int)(context.Width / 2f));
        while (history.Count > maxColumns)
        {
            history.Dequeue();
        }

        var colWidth = context.Width / maxColumns;
        var rowHeight = context.Height / bands.Length;

        var index = 0;
        foreach (var column in history.Reverse())
        {
            var x = context.Width - ((index + 1) * colWidth);
            for (var i = 0; i < column.Length; i++)
            {
                var value = Math.Clamp(column[i], 0f, 1f);
                var y = context.Height - ((i + 1) * rowHeight);
                var color = context.Palette.ColorAt(value, 0.95f);
                ds.FillRectangle(x, y, colWidth + 0.5f, rowHeight + 0.5f, color);
            }

            index++;
            if (index >= maxColumns)
            {
                break;
            }
        }
    }
}
