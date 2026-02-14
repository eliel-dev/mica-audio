# Revisão da base de código — tarefas sugeridas

## 1) Correção de erro de digitação
- **Problema:** no README, a frase "`Bands64` e derivado..." está com erro gramatical/acentuação em português.
- **Evidência:** seção **Contratos estáveis (public APIs)**, regra sobre `Bands64`.
- **Tarefa sugerida:** corrigir para "`Bands64` **é** derivado do mesmo espectro calculado no frame...".
- **Critério de aceite:** README revisado sem esse erro de digitação.

## 2) Correção de bug
- **Problema:** quando `SimulatorLedOutput.Send` recebe `Frame64x32`, o caminho de cópia direta ignora o fator de brilho (`brightness`) aplicado por `SetBrightness`.
- **Impacto:** inconsistência de comportamento entre envio por `Bins64` (respeita brilho) e envio por frame pronto (não respeita brilho).
- **Evidência:** no branch `payload.Frame64x32`, ocorre apenas `Array.Copy(...)`; não há ajuste de brilho por pixel.
- **Tarefa sugerida:** aplicar brilho também no fluxo de `Frame64x32` (durante cópia ou pré-processamento), mantendo clamp em `[0..255]`.
- **Critério de aceite:** `SetBrightness` afeta ambos os fluxos (`Bins64` e `Frame64x32`) de forma consistente.

## 3) Ajuste de documentação (discrepância/clareza)
- **Problema:** o README informa SDK `10.0.102` e, em paralelo, target de app `net8.0-windows...` sem explicitar que o SDK é apenas requisito de toolchain.
- **Impacto:** pode gerar dúvida para contribuidores (parece conflito de versões, embora não seja necessariamente).
- **Evidência:** seção **Tecnologias e requisitos**.
- **Tarefa sugerida:** adicionar uma nota breve esclarecendo a relação entre SDK usado para build e target framework da aplicação.
- **Critério de aceite:** seção de requisitos deixa explícito por que SDK 10 e target net8 coexistem.

## 4) Melhoria de teste
- **Problema:** o teste `SetBrightness_ShouldClampValue` valida apenas que canais RGB são `<= 255`, o que sempre é verdadeiro por serem `byte`.
- **Impacto:** teste fraco, com baixa capacidade de detectar regressão real no clamp de brilho.
- **Evidência:** asserção atual não compara com estado antes/depois nem valida efeito visível de clamp.
- **Tarefa sugerida:** reescrever o teste para verificar comportamento observável, por exemplo:
  - com brilho `0`, frame deve ficar apagado;
  - com brilho `>1`, resultado deve equivaler a brilho `1` (saturação);
  - opcionalmente validar ramo `Frame64x32` além de `Bins64`.
- **Critério de aceite:** teste falha quando clamp/brilho quebrar e passa quando implementação estiver correta.
