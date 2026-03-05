# Esptool Bundle (Windows x64)

`DevicesPage` onboarding USB usa `tools/esptool/win-x64`.

Ordem de execucao:

1. `esptool.exe` local (quando presente)
2. fallback `python -m esptool` via `esptool.cmd`

Uso esperado em release:

- empacotar `esptool.exe` nesta pasta para operar sem dependencia de PlatformIO.
- manter `esptool.cmd` como fallback de desenvolvimento.
