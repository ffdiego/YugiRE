# Joey original — primeira rodada multi-magia, janelas CF (17/11/2026 UTC)

Status: **observações de comandos**, não validação integral de efeitos nem paridade C#. Resultado extraído dos arquivos enviados pelo operador `windows(2).json` e `windows(2).md`, gerados por `yugilab.inspect_effect_windows`. O `state-report.json` e o JSONL nativo completo **não foram anexados a este envio**; não presumir movimentação de campo, seleção de alvos ou atividade contínua.

## Fonte e identificações

- Captura declarada no arquivo: `duels-20261117-035345-497597-pid8400.jsonl`, SHA256 `403ee790bea2661456ec721d722a3dc9d451d37ae324ef646b99d9ce73d04a52`, 613 eventos, 1 comando terminal.
- `windows(2).json`: schema `yugire-native-cf-windows-v1`; resource IDs requisitados `329, 1007, 1213, 1311, 1315`; **seis janelas CF** (Raimei duas vezes).
- A identificação de cada resource ID foi cruzada com `docs/evidence/card-callback-catalog.json` nesta branch:
  - Forest = índice 990, recurso **329**, perfil P036;
  - Raimei = índice 1049, recurso **1007**, perfil P045;
  - Mystic Box = índice 370, recurso **1213**, perfil P213;
  - Burning Land = índice 987, recurso **1311**, perfil P229;
  - Rain of Mercy = índice 969, recurso **1315**, perfil P042.
- Todas as cinco identidades aparecem no catálogo como `class_code=22`; o CF somente liga evento ao resource ID, não prova resolução completa.

## Evidências diretas por janela

| Magia | Seq CF | Comandos posteriores na janela | LP antes/depois observado | Leitura permitida |
|---|---:|---|---|---|
| Raimei (1007) | 62 | `0x43 0x12C` seq65, seguido da amostra seq66 | [8000,8000]→[8000,7700] (−300 lado1) | Forte consistência com dano fixo P045 conhecido |
| Burning Land (1311) | 212 | `0x72` seq213 e novamente seq222; `0x43 0x1F4` seq223 | [7400,6500]→[6900,6500] (−500 lado0) na seq224 | Comando e perda 500 próximos ao recurso, investigar se contínuo/turno |
| Rain of Mercy (1315) | 368 | Dois `0x42 0x3E8` seq371–372, alvos 0 e 1, mesmo caller 0x578823 | [5350,5500]→[6350,6500] (+1000/+1000) na seq373 | Evidência forte e reutilizável de recuperação de ambos |
| Forest (329) | 405 | `0x86` seq408 e 409; `0x11` seq410 | Nenhum ganho/perda de LP **posterior imediato** nesta janela; −500 seq403–404 é **anterior ao CF** | Investigar slot de Field Spell, troca de terreno, flags e efeitos de atributos |
| Mystic Box (1213) | 421 | Dois `0x08` seq423–424; `0x79` seq425; `0x86` seq426; `0x83` seq427 | −300 lado0 seq440–441 ocorre no fim da janela, **sem vínculo causal demonstrado** | Investigar dois slots de monstros e trocas/saídas com state-diff |
| Raimei (1007) | 543 | `0x43 0x12C` seq546, amostra seq547 | [3350,4600]→[3050,4600] (−300 lado0) | Segunda observação de dano fixo de 300 |

**Cuidado com as janelas largas:**
- Antes do CF de Burning Land seq212, houve −600 lado0 seq202→203; **não** atribuir a Burning Land.
- Antes do CF de Rain seq368 houve −500 lado0 seq366→367. Depois, na seq385→386, houve −300 lado1, **sem atribuição causal à Rain**. A amostra +1000/+1000 no meio é delimitada pelos comandos `0x42`.
- Antes do CF de Forest seq405 houve −500 lado1 seq403→404; um evento anterior não pode ser consequência do CF seq405.
- As janelas de Forest seq405 e Mystic Box seq421 **se sobrepõem**; os comandos de uma aparecem na outra. Não contar alterações duas vezes nem atribuir movimentos apenas pela proximidade.
- Antes do segundo CF de Raimei seq543 houve −500 lado1 seq541→542; depois houve também −200 lado0 seq559→560. O valor observado associado à sequência imediata de Raimei é o `0x43` 300 na seq546 e delta −300 na seq547.
- Não afirmar destruição/troca, presença contínua ou modificação de ATK/DEF sem `state-report.json` e sem medir os atributos efetivos.

## Resultado para arquitetura do laboratório

A mesma coleta revelou pelo menos três famílias de assinaturas nativas exploratórias: alteração direta de LP (Raimei/Rain; Burning Land como candidato de dano 500), sequências de campo com `0x86`/`0x11` (Forest) e `0x08`/`0x79`/`0x83` (Mystic Box). Os opcodes genéricos ainda **não são semânticas validadas**.

Próximo passo **sem executar outro duelo**:
1. Solicitar `scratch/multi-spell-states-r01-v1/state-report.json` gerado da mesma captura SHA256 acima (ou JSONL original se necessário), e conferir `captures[].sha256`, `integrityOk` e transições próximas a seq 212, 368, 405, 421 e 543.
2. Cruzar CF com mudanças de `fieldSlot`, contagens de mão/Cemitério e LP, preservando o valor `previousSampleSeq`. O observador registra estado **na entrada** do produtor de comando, não depois dele.
3. Verificar especialmente slot físico 10 para Forest (sem atribuir automaticamente classe de magia ao slot), e posições de monstros antes/depois da seq 421 para Mystic Box. Requer medição de ATK/DEF para testar o buff da Forest.
4. Só então priorizar controle dedicado por família e rodadas de repetição. Manter `readyForFullCsharpComparison=false` até validar comportamento, pré-condições, efeitos colaterais e resultados negativos.

Arquivo de janela não demonstra que todas as 18 magias verificadas nos decks foram compradas ou usadas. As seis janelas CF contêm somente cinco identidades: duas Raimei e uma de cada das outras.
