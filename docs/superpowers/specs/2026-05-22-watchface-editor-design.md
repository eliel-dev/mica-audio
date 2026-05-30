# Watchface Editor — Design Spec

**Data:** 2026-05-22  
**Scope:** Cyberpunk + Aurora  
**Status:** Aprovado

---

## Objetivo

Ferramenta web para ajuste fino de watchfaces, com preview JS no browser e preview real no app WinUI via HTTP, sem recompilar.

---

## Arquitetura

```
watchface-editor.html (browser)
  ├── JS canvas (preview aproximado, atualiza imediato)
  └── fetch PUT → http://localhost:{PORT}/api/watchface-params/{name}
                          ↓
              WatchfaceParamsStore (C#, in-memory, thread-safe)
                          ↓
              WatchfaceLibrary.Render(...) lê do store
                          ↓
              HUB75 / WinUI preview (render real C#)
```

**Fluxo:** slider/color muda → JS canvas atualiza imediato (local) + `fetch PUT` debounced 50ms → app C# recebe → próximo frame usa novos parâmetros.

---

## Seção 1 — C# (backend)

### Arquivo novo: `src/Panels.Composition/ServerSide/WatchfaceParams.cs`

```csharp
record CyberpunkParams(
    int[] Background,       // default: [12, 14, 18]
    int[] Accent,           // default: [252, 205, 0]
    int[] Alert,            // default: [255, 24, 64]
    int[] Crimson,          // default: [50, 6, 12]
    int[] Grid,             // default: [24, 28, 36]
    int GlitchProbability,  // default: 15  (%)
    int MemBarSpeed,        // default: 1000 (ms/tick)
    int BioBarSpeed,        // default: 700
    int LogSpeed,           // default: 1500
    int TimeY,              // default: 20
    int DividerX            // default: 72
) {
    public static CyberpunkParams Default => new(...); // valores acima
}

record AuroraBand(int BaseY, int Height, int[] Color, double Phase, int Amplitude);

record AuroraParams(
    int[] SkyTop,           // default: [4, 10, 28]
    int[] SkyBottom,        // default: [5, 16, 32]
    AuroraBand[] Bands,     // 5 bandas (defaults = valores atuais)
    double MotionSpeed,     // default: 1200.0 (ms por ciclo)
    int[] MountainLow,      // default: [4, 12, 20]
    int[] MountainHigh      // default: [9, 22, 30]
) {
    public static AuroraParams Default => new(...);
}

static class WatchfaceParamsStore {
    private static volatile CyberpunkParams _cyberpunk = CyberpunkParams.Default;
    private static volatile AuroraParams _aurora = AuroraParams.Default;

    public static CyberpunkParams Cyberpunk { get => _cyberpunk; set => _cyberpunk = value; }
    public static AuroraParams Aurora { get => _aurora; set => _aurora = value; }
}
```

### Endpoint HTTP (adicionar ao servidor existente)

```
PUT /api/watchface-params/cyberpunk   Content-Type: application/json
PUT /api/watchface-params/aurora      Content-Type: application/json
→ 200 OK
```

Deserializar JSON → atualizar `WatchfaceParamsStore`. ~20 linhas.

### Mudanças em `WatchfaceLibrary`

- `DrawCyberpunk` e `DrawAurora` leem de `WatchfaceParamsStore` em vez de valores hardcoded
- Defaults idênticos aos valores atuais → sem mudança de comportamento quando editor não está aberto

---

## Seção 2 — Web Editor

**Arquivo:** `tools/watchface-editor.html` — arquivo único, zero dependências externas.

### Layout

```
┌─────────────────────────────────────────────────────────┐
│  MicaAudio Watchface Editor  [CYBERPUNK] [AURORA]  ●    │
├──────────────────────────────┬──────────────────────────┤
│  CORES                       │                          │
│  ■ Accent      [color picker]│   ┌──────────────────┐   │
│  ■ Alert       [color picker]│   │  canvas 128×64   │   │
│  ■ Background  [color picker]│   │  (escala 4×)     │   │
│                              │   └──────────────────┘   │
│  ANIMAÇÃO                    │   Preview JS (~aprox.)   │
│  Glitch prob  ──●────  15%   │   ↕ app WinUI p/ real   │
│  Mem speed    ─────●  1000   │                          │
│  Bio speed    ──●────   700  │                          │
│                              │                          │
│  LAYOUT                      │                          │
│  Time Y       ──●────   20   │                          │
│  Divider X    ────●──   72   │                          │
│                              │                          │
│  [Exportar JSON] [Importar]  │                          │
└──────────────────────────────┴──────────────────────────┘
```

**Aurora — seção extra com 5 bandas expansíveis:**
```
▼ BANDA 1   BaseY: 8  Altura: 16  Amp: 8  [cor]  Phase: 0.00
▶ BANDA 2   ...
```

### Comportamento

- Cada controle (slider/color picker) dispara `fetch PUT` com debounce 50ms
- Indicador no header: verde = app conectado, vermelho = offline
- "Exportar JSON" baixa o arquivo de parâmetros atual
- "Importar JSON" carrega um preset
- PORT do app configurável via input no header (default: porta atual do servidor)

### Preview JS

Porta a lógica de render de Cyberpunk e Aurora para Canvas API. Fidelidade ~90%:
- Cores, posições e layout: idênticos
- Animações: aproximadas (sem `Tick` sincronizado ao C#)
- Fontes: pixel font equivalente em JS

---

## Seção 3 — JSON Schema

### Cyberpunk
```json
{
  "background": [12, 14, 18],
  "accent":     [252, 205, 0],
  "alert":      [255, 24, 64],
  "crimson":    [50, 6, 12],
  "grid":       [24, 28, 36],
  "glitchProbability": 15,
  "memBarSpeed": 1000,
  "bioBarSpeed": 700,
  "logSpeed":   1500,
  "timeY":      20,
  "dividerX":   72
}
```

### Aurora
```json
{
  "skyTop":    [4, 10, 28],
  "skyBottom": [5, 16, 32],
  "bands": [
    { "baseY": 8,  "height": 16, "color": [22, 190, 120], "phase": 0.00, "amplitude": 8 },
    { "baseY": 12, "height": 14, "color": [30, 175, 165], "phase": 1.35, "amplitude": 7 },
    { "baseY": 16, "height": 12, "color": [90,  58, 215], "phase": 2.15, "amplitude": 6 },
    { "baseY": 10, "height": 10, "color": [145, 48, 178], "phase": 3.10, "amplitude": 5 },
    { "baseY": 4,  "height":  8, "color": [42, 210,  90], "phase": 0.70, "amplitude": 4 }
  ],
  "motionSpeed": 1200.0,
  "mountainLow":  [4, 12, 20],
  "mountainHigh": [9, 22, 30]
}
```

---

## Arquivos a criar / modificar

| Ação | Arquivo |
|---|---|
| Criar | `src/Panels.Composition/ServerSide/WatchfaceParams.cs` |
| Modificar | `src/Panels.Composition/ServerSide/WatchfaceLibrary.cs` (DrawCyberpunk, DrawAurora) |
| Modificar | servidor HTTP existente (+endpoint PUT) |
| Criar | `tools/watchface-editor.html` |

---

## Fora de escopo

- Outras 8 watchfaces (expansão futura)
- Persistência de presets no servidor
- Desfazer/refazer
