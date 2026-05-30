# Handoff - Cyberpunk Watchface Fix

## Objetivo
Corrigir a renderização do mostrador Cyberpunk HUB75 para alinhar com o design de alta fidelidade e código de referência solicitados, substituindo a implementação provisória.

## Escopo classificado
Funcional (com governança de caminho de arquivo ativada por tocar em `src/`).

## Arquivos alterados
- [WatchfaceLibrary.cs](file:///c:/Users/eliels/Documents/GitHub/mica-audio/src/Panels.Composition/ServerSide/WatchfaceLibrary.cs) - Adição do método helper `Clear` à struct `Ctx`.
- [WatchfaceCyberpunk.cs](file:///c:/Users/eliels/Documents/GitHub/mica-audio/src/Panels.Composition/ServerSide/Watchfaces/WatchfaceCyberpunk.cs) - Implementação completa do mostrador Cyberpunk com todas as sub-rotinas e retículas.
- [paineis.md](file:///c:/Users/eliels/Documents/GitHub/mica-audio/docs/wiki/modules/paineis.md) - Atualização da documentação para 10 mostradores, incluindo o novo `cyberpunk`.

## Decisoes tomadas
1. **Adição do método `Clear`**: Adicionamos o helper `Clear` diretamente na struct `Ctx` em `WatchfaceLibrary.cs` para suportar a limpeza nativa de frame e compatibilidade perfeita com a sintaxe do snippet de referência.
2. **Separação de horas e minutos com efeito glitch**: Desenhamos as horas e os minutos em blocos separados para permitir um posicionamento perfeito das 3 barras horizontais amarelas do colon no centro (x=33). O efeito de aberração cromática (RGB split) foi alcançado renderizando as strings repetidamente com deslocamentos de 1 pixel e cores diferentes (Cyan à esquerda, HotPink à direita, CyberYellow acima e White no centro).
3. **Status de RAM/BIO animados**: Implementamos as barras de `SYS.MEM` e os ticks de `BIO.PRT` com animações cíclicas baseadas no counter `c.Tick`, além de simular um log dinâmico de endereços hexadecimais rotativos para `0XB7A2` para dar dinamismo premium ao HUD.
4. **Rodapé com badge chanfrada e segundos**: A badge amarela inferior direita usa um corte diagonal TL de 4 pixels, exibindo os segundos da hora atual (`c.Now.Second.ToString("D2")`) em preto. A data em cyan e as hazard stripes em vermelho escuro complementam a barra.

## Validacoes executadas
1. Validação de documentação local:
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
```
Resultado: **OK (100% sucesso)**

2. Compilação da solução:
```powershell
dotnet build MicaAudio.sln -c Debug /p:UseSharedCompilation=false
```
Resultado: **Sucesso (0 erros, 76 avisos de vulnerabilidades normais)**

## Riscos e rollback
- **Riscos**: Baixíssimo risco. A alteração está isolada em renderizadores procedurais do mostrador HUB75 cyberpunk e não afeta outras áreas do sistema.
- **Rollback**: Descartar as alterações feitas em `WatchfaceLibrary.cs`, `paineis.md` e remover o arquivo de mostrador `WatchfaceCyberpunk.cs`.

## Proximos passos
1. Visualizar o resultado no painel de visualização física (HUB75) ou no simulador do app WinUI.
2. Obter feedback do usuário final sobre as proporções e temporizadores.
