# Sparks — observação passiva de mão, campo e LP (14/11/2026)

**Escopo:** apenas a sequência observada por leitura externa de memória (`ReadProcessMemory`) e marcadores do operador. Não é um trace de comando, validação da carta inteira nem prova de causalidade nativa.

## Fonte privada preservada fora do Git

- ZIP local fornecido pelo operador: `duel-20261114-230355-pid4436.zip`.
- **SHA256 do ZIP:** `ef49a369c4c17e4329e725baa20c155e4f06a007b442430d0ee1c05b4faae52e`.
- **SHA256 do `manifest.json`:** `ab95c48dcc1319759d26bc9a1ddf962814aa259fa48c34d01a0f0a648e4a697b`.
- **SHA256 do `markers.jsonl`:** `254b5254ad9428a72db0ff79bd4732ddb89d3792b8c4bb81f95a618a9762f8f6`.
- **SHA256 do `state-changes.jsonl`:** `c2a197b5a504272713a182027ef78b86bd0a038d6896d51bfe974504ecde3cb1`.
- **SHA256 do `snapshots.jsonl`:** `a535a1d437fc28fddfab3b744f86e3fd1fc8948cab029e4c5f475cc6da3c6433`.
- **EXE declarado no manifesto:** `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`. Esse SHA é metadado da captura, não uma nova aferição do executável pelo autor deste relatório.
- Parâmetros: `poll_ms_requested=50`, `poll_count=1233`, `read_error_count=0`, `segment_count_observed=1`, `marker_count=2`.
- `sampling_is_atomic=false`, `native_command_hook_enabled=false`.
- Manifesto: `status=process_exited`; encerramento não foi manual. O ZIP original **não está neste repositório**.

Esses hashes permitem reencontrar e auditar os arquivos privados mediante acesso autorizado. **Não** se deve usar o documento como substituto dos brutos para evidência de paridade.

## Cronologia (tempo relativo ao início da captura)

O operador declarou que apertou `Ctrl+Shift+J` **antes de jogar Sparks** e novamente **após ver uma perda final de 200 LP**. As anotações semânticas são **relato do operador**, não informação nativa independente.

| Tempo (ms) | Fonte | Observação |
| ---: | --- | --- |
| 57250 | `markers.jsonl`, linha 1 | Marcador #1, sem nota |
| 60655 | `state-changes.jsonl`, linha 20 / seq 20 | Mão do lado 1: 6→5; índice 755 removido; índice 755 presente no slot 5 `other_field` do lado 1 |
| 61561 | `state-changes.jsonl`, linha 21 / seq 21 | Mudança de flags do jogador 1 (não interpretada) |
| 63343 | `state-changes.jsonl`, linha 22 / seq 22 | LP lado 0: 8000→7900 (−100) |
| 63405 | `state-changes.jsonl`, linha 23 / seq 23 | LP lado 0: 7900→7800 (−100); flags de slot mudaram |
| 63452 | `state-changes.jsonl`, linha 24 / seq 24 | Índice 755 removido do slot 5 `other_field` |
| 66390 | `markers.jsonl`, linha 2 | Marcador #2, sem nota |

Os snapshots demonstram o estado de LP após a sequência: lado 0 com 7800 e lado 1 com 8000. O índice interno **755** corresponde a Sparks (ID de recurso **342**) no catálogo pinado do projeto.

**Observação:** os dois passos de −100 foram amostrados com intervalo de 62 ms; a execução interna pode ter aplicado um único dano de 200, com uma apresentação animada, ou outra sequência de atualização. Os eventos de RAM **não permitem decidir isso**. Não criar dois comandos artificiais de 100, nem concluir que o tempo de animação é proporcional ao dano com essa única captura.

**Sem inferências indevidas:** `other_field` não prova o mapeamento para zona lógica de magia. O desaparecimento de uma carta da mão/campo não confirma cemitério. As duas janelas de marcadores sobrepõem o **mesmo** conjunto de eventos; não são duas ativações.

## Integração com o piloto diferencial

Este registro é uma **observação parcial**. Pode servir como evidência de campos de LP e transferência de índice interno após auditoria, mas não satisfaz o contrato `Joey.Lab effect-pilot` completamente.

Campos que seguem **Missing/Inconclusive**: `requestFlags`, `preparedTarget`, comando nativo e argumentos, identidade do alvo preparado, cemitério, confirmação do isolamento causal, estado completo de legalidade e política do demo. O comparador atual exige esses campos para `Match`; não se deve preencher ausentes com previsões C# ou valores convenientes.

**Estado do Card Coverage Registry: inalterado.** Sparks continua `Partial` no eixo implementação; validação da carta inteira continua `Unknown`. Esta evidência não autoriza promoção automática para `ValidatedInScope`.

## Próxima verificação

Com autorização separada, obter uma única observação nativa dos parâmetros de enfileiramento do efeito e uma captura passiva do LP na mesma janela. Preservar integridade/hashes da sessão e não modificar o resultado do jogo. Não confundir a leitura intermediária dos LP com uma segunda regra de dano.
