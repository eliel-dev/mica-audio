namespace Visual.Win2D.Engine;

// DOCS: docs/wiki/modules/visual-win2d.md#contrato-de-capacidades
public interface IRendererCapabilitiesProvider
{
    RendererCapabilities Capabilities { get; }
}
