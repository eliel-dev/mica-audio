# Fix - Provisioning AP Direct Start

## Objetivo

Corrigir o fluxo de primeiro boot do firmware ESP32-S3 para que o AP `MicaAudio-Setup-xxxx` seja aberto de forma deterministica em flash limpo ou quando a configuracao obrigatoria estiver ausente.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - o firmware abre o portal AP diretamente quando entra em provisioning explicito;
  - o primeiro boot apos `erase-all` nao depende de tentativa STA previa para expor o AP;
  - o submit do portal continua conectando ao Wi-Fi e seguindo para o pareamento HTTP;
  - a documentacao oficial do setup reflete o comportamento corrigido.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/mica_provisioning.cpp`
- `docs/wiki/guides/setup-new-device.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`

## Decisoes tomadas

1. O firmware passou a chamar `WiFiManager::startConfigPortal()` diretamente em `startProvisioningPortal(...)`, em vez de `autoConnect()`.
2. A decisao foi tomada porque o caminho de `autoConnect()` na `WiFiManager 2.0.17` ainda tenta STA primeiro e pode ficar preso em `waitForConnectResult()` antes de abrir o AP no baseline `Arduino-ESP32 v3.3.8 / ESP-IDF v5.5.4`.
3. O submit do portal ganhou timeout explicito (`setConnectTimeout` + `setSaveConnectTimeout`) alinhado a `kWifiConnectAttemptTimeoutMs`, para manter o comportamento de conexao do formulario sem espera indefinida.
4. O valor default de `Nome dispositivo` passou a ser materializado em `String` local antes de criar `WiFiManagerParameter`, evitando dependencias de `c_str()` sobre temporario.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> sucesso (516 links validados, nenhuma falha)
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> sucesso
dotnet build MicaAudio.sln -c Debug -> bloqueado por arquivos em uso pelo processo App.WinUI (20560)
powershell -ExecutionPolicy Bypass -File .\scripts\build-precompiled-firmware.ps1 -> sucesso (RAM 39.0%, Flash 48.4%)
```

## Riscos e rollback

- Risco principal: algum device que dependia implicitamente do comportamento antigo de `autoConnect()` pode deixar de reutilizar credenciais STA salvas antes de abrir o portal quando o provisioning for chamado explicitamente.
- Esse risco e aceitavel porque o contrato desse caminho ja e "abrir setup", nao "tentar operacao normal".
- Rollback:
  - restaurar `wm.autoConnect(apName.c_str())` em `mica_provisioning.cpp`;
  - remover a secao documental desta correcao na wiki;
  - revalidar o pacote precompilado do app.

## Proximos passos

1. Rodar `build-precompiled-firmware.ps1` e smoke manual em hardware com `erase-all` para confirmar que o AP aparece no primeiro boot.
2. Capturar boot serial real do device apos o fix para confirmar a sequencia `[portal_open]` -> `[provisioning] AP=...`.
3. Se houver novo incidente no portal, considerar substituir `WiFiManager` por um fluxo AP/HTTP proprio na Phase 1B ou 2, reduzindo dependencia de heuristicas legadas da biblioteca.
