# Referencia - Glossary

- `bin (FFT)`: cada faixa de frequencia discreta resultante da FFT. Uma FFT de tamanho N produz N/2 + 1 bins unicos, cada um representando a energia de um intervalo de frequencia proporcional a taxa de amostragem.
- `Bins128`: payload do protocolo v2 que contem 128 bytes, onde cada byte (0-255) e a amplitude normalizada de uma faixa de frequencia do espectro de audio. Os 128 valores sao derivados do espectro de potencia da FFT de 2048 pontos, agregados em bandas por `LogBandMapper` e remapeados para 128 colunas (largura nativa do painel HUB75 128x64) por `LedPayloadFactory.ResampleSpectrumBins`. E o modo de envio padrao do pipeline de audio para o dispositivo fisico.
- `BinCount128`: constante `= 128` em `StreamFrameV2`; define o numero de bins no payload `Bins128` e corresponde a largura em pixels do painel canonico.
- `StreamFrameV2`: contrato binario ativo para stream HUB75 128x64.
- `HUB75 preview`: simulacao local nativa do painel 128x64 no app.
- `PanelType`: identificador tecnico do painel. Valor canonico: `hub75_p2_5_128x64_smd2121_scan32`.
