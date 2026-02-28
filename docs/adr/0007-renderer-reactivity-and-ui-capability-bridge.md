# ADR 0007 - Bridge de reatividade e capacidades de UI para renderers

## Status

Aceito

## Contexto

O modulo `Visual.Win2D` passou a receber renderers novos sem um contrato explicito para declarar suporte aos controles da lateral e sem um baseline compartilhado de reatividade. Isso permitiu que presets novos entrassem com comportamento inconsistente, especialmente no caso do `PolarArcsRenderer`.

Mudar `IRenderer` de forma breaking nesta fase travaria a migracao do catalogo inteiro. O rollout precisava ser incremental.

## Decisao

1. `IRenderer` permanece inalterado nesta iteracao.
2. O contrato novo entra por `IRendererCapabilitiesProvider`.
3. `VisualizerEngine.GetCapabilities(...)` fornece capacidades explicitas para renderers migrados e fallback `LegacyAssumed` para os legados.
4. `ReactiveBandSampler` e `ReactiveEnvelopeState` passam a ser o baseline compartilhado de reatividade.
5. A `MainPage` passa a aplicar o estado do controle `Quantidade de barras` com base nas capacidades do renderer ativo.
6. A migracao inicial cobre apenas `AudioMotionCloneRenderer` e `PolarArcsRenderer`.

## Consequencias

- O rollout inicial fica viavel sem migracao em massa.
- Renderers novos passam a ter um caminho canonico de integracao com UI e reatividade.
- Renderers legados continuam funcionando sob fallback, mas identificados como `LegacyAssumed`.
- O gate principal de reatividade fica deterministico e centrado no sampler, nao em comparacao fragil de bitmap.
