# Famílias visuais ainda não contabilizadas no badge (21/11/2026)

Alvo: `joey_pc.exe`, SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Fonte: `joey-knowledge.sqlite` enviado pelo usuário em 21/11/2026 e o inventário versionado do YugiRE.

## Significado correto da cobertura

O badge contabiliza apenas **rotinas Draw nativas distintas identificadas**, cruzando
`docs/architecture/visual-function-coverage.json` com
`docs/architecture/native-renderer-inventory.md`. Um Draw `Partial` já soma no
numerador; `Pending` não soma. Hoje são **19/31 (61,29%)**, **0 Verified**.
Novos construtores estudados não aumentam a implementação, e os componentes
sem Draw identificado não entram no denominador. Não é progresso total do visual.

Convém manter **identificação**, **análise estática**, **implementação C#** e
**validação contra o original** como dimensões distintas, sem denominadores
inventados. O número de Draws aumentará quando identificarmos novos entrypoints,
inclusive podendo cair o percentual mesmo sem regressões.

## Evidências complementares do SQLite

| Componente | Evidência estática | Pendente |
| --- | --- | --- |
| Seleção/listas | `0040C940 → 0043A060`, vtable `005DABD8`; até 5 objetos de carta; `0043ABA0` calcula `anchorX + 64*(slot-2)`; recursos `selectlist`, `gravelist`, `fusionlist`, `excludelist` | Slots Update/Draw, scroll, manifest real, hitbox e pixels |
| Botões reutilizáveis | `0043E2D0`, vtable `005DACC4`, recurso `dialog/button_?.txt`; `0043F120` cria 1 botão, `0043F740` cria 2, `0043FD20` cria N com passo 23 px | Draw, estados normal/hover, posições, input |
| Corrente | `0041F5C0`, vtable `005DABA8`, `duel/chain/chain.txt`; layer `param_3 + 0x120`. `0041FD60` evita duplicar instância com mesma chave na lista consultada | Draw `0041F870`, transformação, relação com resolução de regras |
| Corrente desativada | `00408730` verifica término de outros objetos e condições do estado; instancia `00452F20`, vtable `005DB374`, layer `0x111`, `duel/chain/disable.txt` | Draw `004531C0`, seleção de sprite, duração |
| Efeito persistente/HUD | `0044C3E0` chama `0040DAC0` na inicialização do gerenciador; vtable `005DA44C`, `duel/effect/affect.txt`, layer `0x101` | Draw `0040E260`; não presumir que o objeto signifique ícone visível |
| Camadas de texto | `00404580` instancia `00455D90` quando `0044B990` indica modo interno 5/7, ou `00456100` quando `0044B980` indica modo 3; vtables `005DB3C4`/`005DB3D4`; ambos usam `layerfont.txt` | Resolver os dois Draws, suas variantes e o significado dos modos |
| Busto e transições | `004152D0` usa `bust_up/start.txt` e vtable própria; `00483B10` carrega `duelend.txt` em outra classe | Draws e cenários; não duplicar Draw do banner normal |

**Detalhe novo de corrente:** `0041FD60` compara `obj+0x14` com uma chave antes de alocar
outro indicador. Isso indica deduplicação de apresentação nessa lista, sem
demonstrar a semântica completa de encadeamento no duelo.

**Detalhe novo de layerfont:** os predicados são inspecionáveis no SQLite:
`0044B990` retorna verdadeiro para `DAT_005F3294 == 5 || == 7`,
e `0044B980` retorna verdadeiro para `DAT_005F3294 == 3`.
Esses números não são nomes comprovados dos modos.

## Próxima extração direcionada

Ler no Ghidra os slots `+8` (Update) e `+0x0C` (Draw) das vtables
`005DABD8`, `005DACC4`, `005DABA8`, `005DB374`, `005DA44C`,
`005DB3C4` e `005DB3D4`, rastreando entradas que caiam dentro de
funções existentes. O índice `pointers` do SQLite é esparso:
**ausência de slot indexado não prova ponteiro nulo**.

Consultar os manifests pelo leitor `Archive` usando o DAT local, sem versionar
conteúdo proprietário. Registrar sprite, escolha de variante, coordenadas,
contadores/updates e ordem de composição; comparar no canvas nativo 800×600.
Promover `Verified` apenas após comparar com o original, preservando
`Joey.Core` e as regras existentes.

### SQL de reprodução

```sql
SELECT f.address, f.size, d.code
FROM functions f JOIN decompilations d USING(address)
WHERE f.address IN ('0041F5C0','0041FD60','00408730','00452F20',
 '0040DAC0','00404580','00455D90','00456100');

SELECT source_function, target_function, from_address
FROM refs
WHERE target_function IN ('0041F5C0','00452F20','0040DAC0',
 '00455D90','00456100')
 AND reference_type = 'UNCONDITIONAL_CALL';
```

Esta rodada é análise estática; não houve execução nativa, captura de frames
nem aumento de implementação do cliente C#.
