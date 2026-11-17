# Joey original — correlação Cemitério / 300 LP sob Skull Invitation

Captura enviada pelo operador em 16/11/2026 (horário local),
arquivo `duels-20261117-020520-485960-pid20416.jsonl`
com SHA256 `e76d67678a3843d14541fed5ec5d94aabbca603a129554ab2f46f69e27919994`.
Origem dos números: relatório offline `state-report.json` e
`state-summary.md` provenientes da ferramenta `yugilab.state_interactions`.
**Não foi recebido nesta etapa o JSONL bruto**, e portanto todas as
afirmações são limitadas ao relatório e suas transições amostradas.

## Saúde da coleta

- 1 duelo, integridade `true`, 382 command-entry samples, 1 baseline,
  108 deltas, 0 readErrors, 18 comandos sem amostra de estado.
- 178 alterações individuais: 71 de fieldSlot, 28 de handCount,
  28 de handIndices, 27 de lifePoints, 13 de graveyardCount,
  11 de deckCount. Diferentes alterações podem compartilhar seq.
- A leitura ocorre na entrada do produtor e **não é atômica**;
  uma saída e uma entrada rápidas entre amostras podem ser perdidas.

## Skull Invitation no campo

- Seq 67: carta índice interno 414 sai da mão do lado 0 e surge
  no slot físico 5, com `occurrenceWord=2`.
- Seq 73: `positionWord` no slot 5 passa de `0` para `1024`.
- Seq 76: `flagsWord` passa de 0 para 2.
- Seq 77: `positionWord` passa a 1026 e `occurrenceWord` a 4.
- Seq 78: `flagsWord` passa de 2 para 0; cardIndex permanece 414.
- Nenhuma saída de 414 foi reportada após isso, inclusive no
  instante do CF de Rain seq 335. Isso é **presença observada**,
  não prova por si só de efeito contínuo ativo.

## Rain of Mercy em contexto

| Seq | Observação |
|---:|---|
| 333 | Lado 0 compra Rain (índice interno 969) |
| 335 | Comando CF ligado ao resource ID 1315; Skull índice 414 no slot 5 do lado 0, positionWord 1026 |
| 336 | Rain sai da mão, entra no slot 7 do lado 0 |
| 340 | LP mudam de [6600,2600] para [7600,3600], +1000 em ambos |
| 341 | Rain continua no slot 7, alteração de flagsWord de 6 para 4 |
| 344 | Rain sai do slot 7 (969→0) **e** contador do Cemitério do lado 0 passa de 3 a 4 |
| 346 | Comando de dano 300 direcionado ao lado 0, segundo `state-summary.md` |
| 348 | LP do lado 0 passam de 7600 para 7300 |

Essa é uma sequência **compatível** com o efeito de Rain resolvido
e Skull Invitation reagindo ao envio ao Cemitério, mas o campo
de destino real da carta e a ativação da armadilha não são
diretamente observados pela captura. A conclusão oficial do
relatório permanece `Unverified`.

## Padrão geral no mesmo duelo — muito mais que uma carta

Aplicando apenas correlação temporal por lado, **todos os
13 incrementos unitários** de `graveyardCount` foram seguidos
por `lifePoints delta=-300` no **mesmo lado**, com intervalo
de **3 a 5 sequências nativas**. Os 13 pares são:

| Lado | Seq +1 no Cemitério | Seq −300 LP | Distância |
|---:|---:|---:|---:|
| 1 | 82 | 85 | 3 |
| 1 | 100 | 104 | 4 |
| 0 | 106 | 111 | 5 |
| 1 | 113 | 116 | 3 |
| 1 | 159 | 162 | 3 |
| 1 | 209 | 212 | 3 |
| 0 | 259 | 262 | 3 |
| 0 | 264 | 267 | 3 |
| 1 | 300 | 303 | 3 |
| 0 | 344 | 348 | 4 |
| 1 | 377 | 380 | 3 |
| 0 | 421 | 424 | 3 |
| 1 | 457 | 460 | 3 |

Há **14 quedas de −300 LP** no total, mas somente 13 pares
graves→perdas: a queda adicional na **seq 455, lado 1**
aparece depois que uma carta sai do campo na seq 454,
mas **antes** do incremento do contador do Cemitério na seq 457.
Não explicar essa perda automaticamente com Skull nem
utilizá-la duas vezes. Pode representar outra causa ou timing
não observado; requer contexto bruto da captura.

Esse padrão, associado à permanência observada de Skull no campo,
é forte evidência comportamental **consistente com** 300 LP
por carta enviada ao Cemitério, debitado de seu dono.
**Não é validação causal:** os contadores não dão a identidade
da carta no Cemitério, e não sabemos se houve outros efeitos
simultâneos, leitura parcial ou eventos intermediários.

## Ferramenta adicionada para reuso

`yugilab.temporal_correlations.correlate_graveyard_lp300`
e integração em `yugilab.state_interactions` quantificam
pares temporais sem duplicação por duelo/lado, com janela
pós-hoc de até seis sequências. O relatório guarda
`graveyardLp300TemporalCorrelations`, pares e perdas
sem correspondência, sempre `TemporalCorrelationOnly`.
A janela de seis foi escolhida **após observar esta captura**:
não generalizar como lei da engine sem reprodução independente.

Próxima etapa: rodar testes offline da nova correlação e
reanalisar o **mesmo JSONL** em diretório novo, sem outro duelo.
Depois planejar um controle negativo, com Skull ausente do
campo, e controle positivo com Skull inequivocamente ativada.
Somente então classificar a hipótese como comportamento
causal reproduzido. Não implementar regras no C# baseando-se
apenas nessa correlação.
