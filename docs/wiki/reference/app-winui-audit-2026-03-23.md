# Auditoria desktop WinUI - 2026-03-23

## Objetivo

Validar a superficie desktop do repositorio quanto a:

1. orientacoes de configuracao precisas
2. inicializacao da aplicacao
3. aderencia a decisoes de UX modernas do Windows
4. uso de padroes concretos de implementacao WinUI / Windows App SDK

## Escopo e metodo

- Escopo principal: `src/App.WinUI`, `installer/`, `scripts/dev-run.ps1`, `scripts/dev-doctor.ps1`, `README.md` e wiki tecnica relevante ao desktop.
- Shared code foi lido apenas quando impacta startup, packaging, shell, runtime ou experiencia desktop.
- A auditoria foi executada em `2026-03-23`, com verificacao estatica da base e verificacao objetiva de launch local do binario `Debug`.
- Critérios de referencia:
  - [System requirements for Windows app development](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/system-requirements)
  - [Settings for developers / Developer Mode](https://learn.microsoft.com/en-us/windows/advanced-settings/developer-mode)
  - [Deploy unpackaged apps](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-unpackaged-apps)
  - [NavigationView](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/navigationview)
  - [Responsive layouts with XAML](https://learn.microsoft.com/en-us/windows/apps/develop/ui/layouts-with-xaml)
  - [Title bar design](https://learn.microsoft.com/en-us/windows/apps/design/basics/titlebar-design)
  - [WinUI Gallery](https://github.com/microsoft/WinUI-Gallery)

## Resumo executivo

| Eixo | Status | Nota curta |
| --- | --- | --- |
| Configuracao | parcial | A base roda, mas a documentacao local ainda nao fecha o ambiente WinUI com precisao suficiente para onboarding reprodutivel. |
| Inicializacao | parcial | O startup esta robusto e foi validado em runtime, mas a janela ainda abre com identidade padrao `WinUI Desktop`. |
| UX moderna do Windows | parcial | A shell usa controles nativos corretos, porem falta estrategia explicita para largura estreita e polimento de title bar/identidade. |
| Padroes de implementacao | conforme | O app segue um caminho WinUI pragmatico: `NavigationView`, `CommandBar`, DI, shell lazy e separacao explicita entre `Debug` unpackaged e `Release` MSIX. |

## Achados priorizados

### [P1] As orientacoes locais de setup nao fecham um ambiente WinUI reproduzivel

**Status do eixo afetado:** `parcial`

**Impacto**

O repositório fornece build local e script de execucao, mas ainda nao descreve com precisao o conjunto minimo de pre-requisitos WinUI para preparar uma maquina nova sem tentativa e erro. Isso aumenta risco de onboarding quebrado e mascara diferenca entre "este repo compila nesta maquina" e "a maquina esta pronta para desenvolvimento WinUI de forma previsivel".

**Evidencias**

- O `README` lista requisitos de forma generica, sem fechar workloads/componentes, Windows SDK, MSBuild e template/fluxo WinUI: [README requisitos](../../../README.md#L193), [README fluxo local](../../../README.md#L216).
- O projeto depende explicitamente de um modelo hibrido de deploy: `Debug` unpackaged e `Release` MSIX: [App.WinUI.csproj](../../../src/App.WinUI/App.WinUI.csproj#L5), [Debug unpackaged](../../../src/App.WinUI/App.WinUI.csproj#L25), [Release MSIX](../../../src/App.WinUI/App.WinUI.csproj#L31).
- O script de execucao recomendado inicia em `RunMode = "publish"` por default, o que reforca a necessidade de documentar claramente o modelo de empacotamento usado em cada fluxo local: [dev-run.ps1](../../../scripts/dev-run.ps1#L3).
- Evidencia pratica do host atual: `dotnet build MicaAudio.sln -c Debug` passou, mas `dotnet new list winui` nao encontrou template. Isso confirma que "repo funcional" nao equivale a "toolchain WinUI integralmente pronta".

**Recomendacao objetiva**

Documentar um checklist fechado de prontidao desktop em pagina propria da wiki, cobrindo no minimo:

1. Windows suportado e build minimo
2. Visual Studio / workloads / componentes exigidos
3. Windows SDK minimo
4. .NET SDK exigido pelo `global.json`
5. quando `Developer Mode` e necessario
6. diferenca entre fluxo `Debug` unpackaged, `publish` local e `Release` empacotado
7. como interpretar ausencia do template `winui` sem confundir isso com bloqueio do build atual

### [P2] A verificacao de launch expôs a identidade padrao `WinUI Desktop`

**Status do eixo afetado:** `parcial`

**Impacto**

O app abre e mostra janela principal, mas a identidade visual do produto no startup ainda parece scaffold/template. Isso afeta percepcao de acabamento, screenshots, multitarefa e alinhamento com guidance moderno de title bar, cujo papel principal e identificar a aplicacao.

**Evidencias**

- O startup cria `Window`, resolve a shell e ativa a janela, mas nao define titulo explicito no caminho principal: [App.OnLaunched](../../../src/App.WinUI/App.xaml.cs#L57).
- A shell ja esta estruturada com `NavigationView` como chrome principal: [ShellPage.xaml](../../../src/App.WinUI/Views/ShellPage.xaml#L10), [ShellPage code-behind](../../../src/App.WinUI/Views/ShellPage.xaml.cs#L38).
- Evidencia pratica do host atual: o launch local do binario `Debug` abriu uma janela principal com titulo `WinUI Desktop`.
- A documentacao oficial de title bar da Microsoft destaca que a funcao principal da barra de titulo e permitir que o usuario identifique o app: [Title bar design](https://learn.microsoft.com/en-us/windows/apps/design/basics/titlebar-design).

**Recomendacao objetiva**

Definir o titulo da janela de forma explicita no startup e manter ownership claro entre titulo, shell e possivel customizacao futura da title bar. Nao e necessario adotar title bar customizada agora; basta remover a identidade de template do launch path.

### [P2] As paginas principais nao exibem estrategia explicita para largura estreita

**Status do eixo afetado:** `parcial`

**Impacto**

As superficies principais usam controles nativos corretos, mas a base ainda nao mostra uma estrategia de breakpoints clara para janelas estreitas. Em Windows moderno, a app deve continuar usavel em resize agressivo, snap e larguras proximas de tablet/phone-width, com reflow, show/hide ou simplificacao intencional.

**Evidencias**

- A `MainPage` abre uma settings pane lateral fixa de `420` px: [MainPage SplitView](../../../src/App.WinUI/Views/MainPage.xaml#L20).
- A `DevicesPage` monta uma shell de duas colunas com lista fixa em `340` px antes de qualquer regra explicita de reflow: [DevicesPage UI](../../../src/App.WinUI/Views/DevicesPage.Ui.cs#L94), [DevicesPage columns](../../../src/App.WinUI/Views/DevicesPage.Ui.cs#L108).
- A `PanelsPage` usa larguras minimas relevantes no header/editor (`MinWidth = 240` e `MinWidth = 260`) sem estrategia pagina-a-pagina de colapso no topo: [PanelsPage gallery header](../../../src/App.WinUI/Views/PanelsPage.Ui.cs#L90), [PanelsPage editor header](../../../src/App.WinUI/Views/PanelsPage.Ui.cs#L130).
- A `MonitoringPage` usa header com tres colunas e KPI grid sem breakpoints declarados no proprio layout: [MonitoringPage UI](../../../src/App.WinUI/Views/MonitoringPage.Ui.cs#L19), [MonitoringPage header](../../../src/App.WinUI/Views/MonitoringPage.Ui.cs#L44).
- Na leitura das `Views` nao apareceu uma estrategia consistente com `VisualStateManager`, `AdaptiveTrigger` ou regra equivalente de breakpoints nas paginas principais; o que existe hoje e adaptacao pontual de controles, nao uma politica de shell + pagina.

**Recomendacao objetiva**

Definir estados `wide`, `medium` e `narrow` para as paginas principais e validar pelo menos:

1. quando colunas viram pilha unica
2. quando acoes saem do header e vao para overflow
3. quando paines laterais deixam de competir com o conteudo principal
4. quando metadados secundarios deixam de ocupar a primeira dobra

## Pontos conformes

- O modelo de deploy esta explicitado no projeto e segue guidance valido para app unpackaged em `Debug`: [App.WinUI.csproj](../../../src/App.WinUI/App.WinUI.csproj#L25). Isso esta alinhado com o guidance oficial para `WindowsPackageType=None` em apps unpackaged.
- O caminho de release desktop esta coerente com instalador dedicado e bootstrap do runtime .NET Desktop: [Bundle WiX](../../../installer/MicaAudio.Bundle/Bundle.wxs#L1).
- A shell usa `NavigationView` para navegacao de topo e `CommandBar` nas superficies de acao, evitando chrome bespoke desnecessario: [ShellPage.xaml](../../../src/App.WinUI/Views/ShellPage.xaml#L10), [DevicesPage CommandBar](../../../src/App.WinUI/Views/DevicesPage.Ui.cs#L141).
- O startup foi desenhado com observabilidade e degradacao controlada: DI no `App`, resolucao lazy de paginas e fallback por aba em caso de falha: [App startup](../../../src/App.WinUI/App.xaml.cs#L73), [ShellPage fallback](../../../src/App.WinUI/Views/ShellPage.xaml.cs#L79).

## Evidencias praticas do host usado na auditoria

- Data da auditoria: `2026-03-23`
- SO: `Windows 10 Pro`, build `26200`
- SDKs .NET presentes: `10.0.103` e `10.0.201`
- Visual Studio detectado localmente: familia `18` (`Community` e `Insiders`)
- Windows SDKs presentes no host: `10.0.26100.0` e `10.0.22621.0`
- `Developer Mode`: ativo no host auditado
- `dotnet build MicaAudio.sln -c Debug`: bem-sucedido
- Launch verificado do binario `Debug`: janela principal aberta com handle valido
- `dotnet new list winui`: sem template no host auditado

## Conclusao

A base desktop esta tecnicamente solida no que mais importa para um app WinUI real: shell nativa, separacao explicita entre modos de deploy, startup resiliente e uso predominante de controles de plataforma. Os gaps identificados sao de qualidade de produto e prontidao operacional, nao de inviabilidade arquitetural.

Prioridade recomendada de correcao:

1. fechar documentacao de setup desktop WinUI
2. remover o titulo padrao `WinUI Desktop`
3. formalizar breakpoints e comportamento `narrow` das paginas principais
