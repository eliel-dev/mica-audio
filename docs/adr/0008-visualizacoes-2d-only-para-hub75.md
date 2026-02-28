# 0008 - Visualizacoes 2D-only para HUB75

## Status

Aceito

## Contexto

O modulo de visualizacao acumulou um caminho de shader GPU e um renderer pseudo-3D (Hyper Tunnel) que contrariam a direcao do produto. Como as visualizacoes precisam permanecer legiveis no HUB75 e operacionais em maquinas mais fracas, manter esse caminho eleva a complexidade sem ganho proporcional.

## Decisao

1. O modulo `Visual.Win2D` passa a ser oficialmente 2D-only.
2. O caminho de shader GPU e o caminho pseudo-3D do Hyper Tunnel deixam de ser suportados.
3. Novas visualizacoes devem ser implementadas em Win2D 2D, sem `ComputeSharp`.
4. Presets legados com renderer aposentado devem migrar automaticamente para `AudioMotion Clone`.

## Consequencias

### Positivas

- Menor complexidade operacional e de build.
- Melhor alinhamento com HUB75 e com VMs/maquinas sem GPU dedicada.
- Menos drift entre politica de produto e implementacao real.

### Negativas

- O caminho Hyper Tunnel deixa de existir como opcao suportada.
- Presets legados com esse renderer perdem a identidade visual antiga e passam a usar `AudioMotion Clone`.

## Referencias

- `src/Visual.Win2D/Engine/VisualizerEngine.cs`
- `src/Visual.Win2D/Visual.Win2D.csproj`
- `src/App.WinUI/Services/PresetRepository.cs`
- `docs/wiki/modules/visual-win2d.md`
