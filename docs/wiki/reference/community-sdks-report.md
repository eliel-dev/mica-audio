# Relatório - SDKs e Bibliotecas da Comunidade para Mica Audio

> **Objetivo:** Mapear soluções da comunidade ou oficiais (.NET / NuGet) que podem facilitar a implementação das funcionalidades planejadas ou existentes no projeto, seguindo o modelo já adotado com `open-meteo-dotnet-client-sdk` para o app de Clima.

---

## Contexto

O projeto hoje usa `HttpClient` diretamente para integrar com a API Open-Meteo (clima). Esse padrão funciona, mas algumas funcionalidades planejadas podem se beneficiar de SDKs prontos da comunidade — reduzindo código boilerplate, aumentando confiabilidade e acelerando entregas.

**Apps atualmente no catálogo:**
- ✅ **Clima** — Open-Meteo (HttpClient direto)
- ✅ **Relógio** — sem dependência externa
- ✅ **GIF HUB75** — player de GIF animado

**Apps planejados (sem implementação atual):**
- 🔲 News Ticker
- 🔲 Finance (ações/cripto)
- 🔲 Scores (esportes)
- 🔲 Productivity
- 🔲 Decorativo

---

## 1. Clima (já implementado)

| Biblioteca | NuGet | Licença | Tipo |
|-----------|-------|---------|------|
| `open-meteo-dotnet-client-sdk` | [NuGet](https://www.nuget.org/packages/open-meteo-dotnet-client-sdk) | MIT | Comunidade |

**Status atual:** A implementação usa `HttpClient` diretamente no `WeatherPreviewDataService`. O SDK da comunidade (`colinnuk/open-meteo-dotnet-client-sdk`) oferece modelos tipados e abstrai a construção da URL de forecast, mas a integração atual funciona bem. Migrar para o SDK oficial seria uma refatoração opcional de baixo risco.

---

## 2. GIF HUB75 (implementado parcialmente)

### SixLabors.ImageSharp

| Propriedade | Valor |
|------------|-------|
| **NuGet** | `SixLabors.ImageSharp` ≥ 3.1.x |
| **GitHub** | [SixLabors/ImageSharp](https://github.com/SixLabors/ImageSharp) |
| **Licença** | Apache 2.0 (uso comercial requer licença Six Labors) |
| **Compatibilidade** | .NET 6+ / .NET 10 ✅ |

**Para que serve no projeto:**
- Decodificação de GIFs animados (frame-by-frame) para extração de pixels RGBA por frame
- Controle de `FrameDelay` por frame (timing preciso para reprodução no HUB75)
- Suporte a resize, crop e conversão de paleta para adaptar GIFs ao grid 64x32 ou 128x64

```csharp
using Image<Rgba32> gif = Image.Load<Rgba32>("animation.gif");
foreach (ImageFrame<Rgba32> frame in gif.Frames)
{
    int delayMs = frame.Metadata.GetGifMetadata().FrameDelay * 10;
    // extrair pixels e enviar ao LedPayload
}
```

**Prioridade: Alta** — O app GIF HUB75 depende diretamente de uma lib robusta de GIF. Sem isso, a implementação precisa lidar manualmente com o formato binário GIF.

---

## 3. News Ticker (planejado)

### CodeHollow.FeedReader

| Propriedade | Valor |
|------------|-------|
| **NuGet** | `CodeHollow.FeedReader` |
| **GitHub** | [arminreiter/FeedReader](https://github.com/arminreiter/FeedReader) |
| **Licença** | MIT |
| **Compatibilidade** | .NET 6+ / .NET 10 ✅ |

**Para que serve no projeto:**
- Leitura de feeds RSS e Atom para o app News Ticker
- Parse automático de títulos, descrições e links de qualquer feed público
- Suporta os formatos RSS 0.9x, 1.0, 2.0, Atom e MediaRSS

```csharp
var feed = await FeedReader.ReadAsync("https://feeds.bbci.co.uk/news/rss.xml");
foreach (var item in feed.Items.Take(5))
    Console.WriteLine(item.Title);
```

### System.ServiceModel.Syndication (oficial Microsoft)

| Propriedade | Valor |
|------------|-------|
| **NuGet** | `System.ServiceModel.Syndication` |
| **GitHub** | [dotnet/runtime](https://github.com/dotnet/runtime) |
| **Licença** | MIT |
| **Compatibilidade** | .NET 6+ / .NET 10 ✅ |

**Para que serve:** Alternativa oficial da Microsoft para leitura de RSS/Atom. Mais verbosa que `FeedReader`, mas sem dependências de terceiros.

**Recomendação:** Usar `CodeHollow.FeedReader` pela API mais simples e suporte ativo.

---

## 4. Finance — Ações e Cripto (planejado)

### Skender.Stock.Indicators

| Propriedade | Valor |
|------------|-------|
| **NuGet** | `Skender.Stock.Indicators` |
| **GitHub** | [DaveSkender/Stock.Indicators](https://github.com/DaveSkender/Stock.Indicators) |
| **Licença** | Apache 2.0 |
| **Compatibilidade** | .NET 6+ / .NET 10 ✅ |

**Para que serve no projeto:**
- Cálculo de indicadores técnicos (SMA, RSI, MACD, Bollinger Bands) sobre dados de ativos
- Modo streaming (v3+): recebe tick a tick e atualiza indicadores em tempo real
- Útil para exibir variação percentual, tendência e sinais de alta/baixa no display HUB75

**Nota:** A biblioteca calcula indicadores; os dados de preços devem vir de uma API de mercado (ex: Alpaca, Alpha Vantage ou Brapi.dev para BR).

### Alpaca.Markets (.NET SDK oficial)

| Propriedade | Valor |
|------------|-------|
| **NuGet** | `Alpaca.Markets` |
| **GitHub** | [alpacahq/alpaca-trade-api-csharp](https://github.com/alpacahq/alpaca-trade-api-csharp) |
| **Licença** | Apache 2.0 |
| **Compatibilidade** | .NET 6+ / .NET 10 ✅ |

**Para que serve:** Fonte de dados de mercado (cotações em tempo real e histórico) de ações e criptomoedas americanas via WebSocket. Inclui conta gratuita para dados de mercado.

### Brapi.dev (via HttpClient — mercado brasileiro)

> Não possui SDK .NET oficial, mas oferece API REST gratuita para cotações da B3 (bolsa brasileira).

```http
GET https://brapi.dev/api/quote/{ticker}
```

**Para que serve:** Cotações de ações brasileiras (PETR4, VALE3, etc.) e Bitcoin em BRL.

---

## 5. Scores — Esportes (planejado)

### TheSportsDB (via HttpClient)

> Não possui SDK .NET oficial. API REST gratuita com dados de ligas, partidas e placar ao vivo.

| Propriedade | Valor |
|------------|-------|
| **Site** | [thesportsdb.com/api.php](https://www.thesportsdb.com/api.php) |
| **Gratuito** | Sim (tier free com limitações) |
| **Licença** | CC BY-SA 4.0 |

**Para que serve:**
- Placar ao vivo de jogos de futebol, NBA, NFL, etc.
- Escalações, logotipos de times, resultados recentes
- API REST JSON integrada com `HttpClient` (padrão já usado no projeto)

### Refit (geração automática de clientes REST)

| Propriedade | Valor |
|------------|-------|
| **NuGet** | `Refit` |
| **GitHub** | [reactiveui/refit](https://github.com/reactiveui/refit) |
| **Licença** | MIT |
| **Compatibilidade** | .NET 6+ / .NET 10 ✅ |

**Para que serve:** Geração automática de clientes HTTP tipados a partir de interfaces C#, eliminando código repetitivo de `HttpClient`. Útil para APIs sem SDK oficial (TheSportsDB, Brapi.dev, etc.).

```csharp
[Headers("Accept: application/json")]
public interface ISportsDbApi
{
    [Get("/v1/json/3/eventslastleague.php?id={leagueId}")]
    Task<SportsResponse> GetLastEventsAsync(int leagueId);
}
```

---

## 6. Música em Reprodução — Integração Spotify

### SpotifyAPI-NET

| Propriedade | Valor |
|------------|-------|
| **NuGet** | `SpotifyAPI-NET` |
| **GitHub** | [JohnnyCrazy/SpotifyAPI-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET) |
| **Licença** | MIT |
| **Compatibilidade** | .NET 6+ / .NET 10 ✅ |

**Para que serve no projeto:**
- App "Agora Tocando" no HUB75: exibir nome da música, artista e capa do álbum
- Controle de reprodução (play/pause/skip) via display
- Integração com OAuth PKCE (sem armazenar segredos)

**Pré-requisito:** Cadastro gratuito no Spotify Developer Portal para obter `ClientId`.

---

## 7. IoT — Comunicação via MQTT

### MQTTnet

| Propriedade | Valor |
|------------|-------|
| **NuGet** | `MQTTnet` |
| **GitHub** | [dotnet/MQTTnet](https://github.com/dotnet/MQTTnet) |
| **Licença** | MIT |
| **Compatibilidade** | .NET 6+ / .NET 10 ✅ |

**Para que serve no projeto:**
- Alternativa ou complemento ao WebSocket atual para comunicação com ESP32-S3
- Suporta MQTT 5.0, QoS, retain, last will e reconexão automática
- Pode substituir o protocolo WebSocket customizado em cenários multi-device com broker (ex: Mosquitto local)
- Já há bibliotecas MQTT para Arduino/ESP32 compatíveis com MQTTnet no servidor

**Nota:** Mudança de protocolo de comunicação é estrutural e exige ADR. Avalie se o overhead de um broker MQTT compensa para o volume de devices previsto.

---

## 8. Atualização Automática do App

### Velopack

| Propriedade | Valor |
|------------|-------|
| **NuGet** | `Velopack` |
| **GitHub** | [velopack/velopack](https://github.com/velopack/velopack) |
| **Licença** | MIT |
| **Compatibilidade** | .NET 6+ / .NET 10 ✅, WinUI ✅ |

**Para que serve no projeto:**
- Substituição do instalador MSI/MSIX atual por um fluxo com atualização silenciosa/delta
- Verificação de nova versão no background e aplicação sem reinstalação completa
- Publicação de releases diretamente no GitHub Releases (já usado no projeto)
- Delta updates: baixa apenas o diff entre versões (instalador menor para o usuário)

```csharp
// Program.cs — antes de qualquer App.Start()
VelopackApp.Build().Run();

// Verificação de update em background
var mgr = new UpdateManager("https://github.com/eliel-dev/mica-audio/releases");
var info = await mgr.CheckForUpdatesAsync();
if (info != null) await mgr.DownloadUpdatesAsync(info);
```

**Prioridade: Média** — O projeto já tem instalador MSI funcional. Velopack agrega valor em cenários com atualizações frequentes.

---

## 9. Notificações Windows

### Microsoft.Windows.AppNotifications (WinAppSDK)

| Propriedade | Valor |
|------------|-------|
| **NuGet** | Incluso no `Microsoft.WindowsAppSDK` (já referenciado) |
| **Docs** | [learn.microsoft.com](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/notifications/app-notifications/) |
| **Licença** | MIT |

**Para que serve no projeto:**
- Notificações Toast nativas do Windows 11 (ex: "Dispositivo ESP32 conectado", "Nova atualização disponível")
- Sem dependência adicional — já está disponível via `WindowsAppSDK` que o projeto usa
- Suporta botões de ação, imagens inline e notificações persistentes na Central de Ações

```csharp
var builder = new AppNotificationBuilder()
    .AddText("Dispositivo conectado")
    .AddText("ESP32-S3 #1 agora está online.");
AppNotificationManager.Default.Show(builder.BuildNotification());
```

---

## 10. Processamento de Imagens para HUB75

### SixLabors.ImageSharp.Drawing

| Propriedade | Valor |
|------------|-------|
| **NuGet** | `SixLabors.ImageSharp.Drawing` |
| **GitHub** | [SixLabors/ImageSharp.Drawing](https://github.com/SixLabors/ImageSharp.Drawing) |
| **Licença** | Apache 2.0 |
| **Compatibilidade** | .NET 6+ / .NET 10 ✅ |

**Para que serve no projeto:**
- Renderização de texto, gráficos vetoriais e ícones em memória para envio ao HUB75
- Complemento ao Win2D para geração de frames no contexto do servidor (sem contexto gráfico WinUI)
- Útil para o app Finance (barras de variação) e Scores (placar com fontes customizadas)

---

## Resumo por Prioridade

| Prioridade | Biblioteca | App/Funcionalidade | Esforço de Integração |
|-----------|-----------|-------------------|----------------------|
| 🔴 Alta | `SixLabors.ImageSharp` | GIF HUB75 | Baixo — troca de decode |
| 🔴 Alta | `Microsoft.Windows.AppNotifications` | Notificações | Muito baixo — já no SDK |
| 🟡 Média | `CodeHollow.FeedReader` | News Ticker | Baixo — só parse RSS |
| 🟡 Média | `Alpaca.Markets` + `Skender.Stock.Indicators` | Finance | Médio — API key + modelos |
| 🟡 Média | `Velopack` | Auto-update | Médio — mudança no startup |
| 🟢 Baixa | `SpotifyAPI-NET` | App "Agora Tocando" | Médio — OAuth + novo app |
| 🟢 Baixa | `MQTTnet` | IoT multi-device | Alto — mudança de protocolo |
| 🟢 Baixa | `Refit` | Sports / APIs genéricas | Baixo — melhoria de DX |
| 🟢 Baixa | `TheSportsDB` (HttpClient) | Scores | Médio — novo app |

---

## Compatibilidade com .NET 10

Todos os pacotes listados suportam `.NET Standard 2.0+` ou `.NET 6+`, sendo portanto compatíveis com o `.NET 10` usado no projeto. Recomenda-se verificar vulnerabilidades conhecidas via `gh-advisory-database` antes de adicionar qualquer pacote.

---

## Referências

- [open-meteo-dotnet-client-sdk](https://github.com/colinnuk/open-meteo-dotnet-client-sdk) — padrão de referência
- [NuGet Gallery](https://www.nuget.org/) — repositório central
- [NuGet Trends](https://nugettrends.com/) — comparativo de popularidade
- [.NET IoT Libraries](https://learn.microsoft.com/en-us/dotnet/iot/) — Microsoft oficial
- [Six Labors ImageSharp](https://docs.sixlabors.com/articles/imagesharp/) — docs oficiais
- [Velopack Docs](https://docs.velopack.io) — guia de migração do Squirrel
