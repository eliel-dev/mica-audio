namespace Visual.Win2D.Engine;

public interface IRenderer
{
    string RendererId { get; }

    string DisplayName { get; }

    void Render(RenderContext context);
}
