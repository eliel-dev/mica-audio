# Convencoes de links wiki <-> codigo

## Regra 1: wiki -> codigo

Sempre usar `arquivo#Llinha` com caminho relativo a partir da pagina atual.

Exemplo:

```md
[MainPage.CreateAnalyzer](../../../src/App.WinUI/Views/MainPage.xaml.cs#L97)
```

## Regra 2: assinatura esperada

Logo apos cada link tecnico, documentar assinatura esperada para facilitar busca quando linha mudar.

Exemplo:

```md
- [MainPage.CreateAnalyzer](../../../src/App.WinUI/Views/MainPage.xaml.cs#L97) - assinatura: `private IAnalyzer CreateAnalyzer(PresetDefinition preset)`
```

## Regra 3: codigo -> wiki

Usar marcador padrao:

```csharp
// DOCS: docs/wiki/modules/analyzer-dsp.md#modulo-analyzerdsp
```

## Regra 4: local de comentarios DOCS

- 1 no topo da classe/arquivo chave.
- 1 em metodo critico.
- Para DTO pequeno, usar 1 no topo e 1 em propriedade/campo importante.

## Regra 5: exemplos HTTP e WS

Para exemplos de protocolo, referenciar sempre paginas canonicas:

- HTTP: `docs/wiki/reference/http-api-v1.md`
- WS: `docs/wiki/reference/ws-protocol-v1.md`

## Regra 6: anchors estaveis

- Titulos em PT-BR tecnico (preferencia sem acentos).
- Evitar pontuacao desnecessaria no titulo.
- Quando renomear titulo, atualizar `DOCS:` e links dependentes no mesmo commit.

## Validacao

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
```
## Regra 7: politica canonica de IA

Toda regra de governanca deve apontar para o manifesto:

- docs/wiki/reference/ai-contract.v1.yaml
- docs/wiki/reference/ai-contract.schema.json

Scripts e markdown que definem policy devem referenciar estes arquivos como fonte unica.

