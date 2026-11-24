# P002 — Revival Jam: elegibilidade, decisão e dispatcher nativo (21/11/2026)

**Alvo:** `joey_pc.exe`, SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`. **Método:** decompilação/disassembly read-only no Ghidra 12.2 a partir de `QueryArchive.java` no Fedora; algumas funções exigiram `tempfunc:`. Não houve execução nativa nesta etapa.

**Escopo:** comportamento estático demonstrado do perfil P002, resource ID 1405, índice DAT observado 1060. Não equivale a paridade de `Joey.Core`. Complementa [experimento humano](p002-revival-jam-human-yes-no-2026-11-20.md) e [issue #24](https://github.com/ffdiego/YugiRE/issues/24).

## Callbacks da carta e fronteira de LP

Catálogo P002: availability `0x00582990`, preparation `0x0057DC40`.

- `FUN_00582990(ctx)`: se `(*(uint16_t*)(ctx+2) & 0xFC0) == 0x540`, devolve `LP[side] > 1000`, com `side=(*(uint16_t*)(ctx+2)&1)` e leitura de `0x00A55D64 + side*0xD44`; fora dessa condição devolve falso.
- `FUN_0057DC40` no estágio inicial também exige pelo menos **1001 LP**. Portanto, a inelegibilidade com **1000 LP exatos** está demonstrada por comparação estática para esse caminho, mesmo sem teste humano na fronteira.
- A preparação possui um estágio derivado do byte alto da word em `0x00A55C8C`: **0** verifica LP, prepara/autoriza escolha; **1** consulta `0x00A558B4` e, quando não zero, invoca `FUN_00578460(side,1000)` e `FUN_00579790(...)`. Os papéis contratuais dessas duas últimas rotinas exigem confirmação isolada; são consistentes com custo e comando de retorno. Valores de estágio diferentes de 0/1 seguem outra saída.

## Caminho humano, decisão compartilhada e UI

Fluxo lógico, **não uma única cadeia síncrona**:

```text
FUN_0057DC40 (stage 0, side sem bit automático)
  FUN_00402290 -> FUN_005BDF40(..., 0x48, ...)  formatação
  FUN_005BF800(...)                         prepara mensagem; zera resposta
  FUN_005BF9A0(1,0)                         tipo de solicitação=1
    [processamento posterior]
FUN_005BF9C0                                dispatcher de UI (0..9)
  case 1: alocação 0x158; FUN_0043F740(texto)
    FUN_0043E2D0(1,0); FUN_0043E2D0(2,0)   constrói dois controles
    [evento/atualização da janela]
    FUN_0043E280 -> FUN_0043E0A0(result)
      FUN_005BFCC0(result)
        DAT_00A558B4 = (uint16_t)result
        FUN_005BDBD0(00A55044,00A55048,00A5504C)
          FUN_005BEE80 -> FUN_005BF230 (somente sob gate +0x1068)
  [FUN_0057DC40 stage 1] lê DAT_00A558B4
```

`FUN_005BF9C0` só cria quando `DAT_00A53774 != 0` e não há janela ativa (ponteiro em `DAT_005F3280+0x1308`, byte de estado `+4`). Ao construir, registra ponteiros do objeto e limpa flag de pedido. A tabela de dispatch fica em `0x005BFC8C` com **10 entradas** (tipos 0..9); tipo 1 chama `FUN_0043F740`. Isso é **infraestrutura compartilhada**, não API específica da carta.

`FUN_0043E280` usa `this+0x14` como estado e, se `this+0x19` e um método virtual em `vtable+0x1C` permitem, encaminha `this+0x10` como resultado para `FUN_0043E0A0`. O construtor `FUN_0043E2D0` grava um argumento de identificação em `button+0x30`. **Não confundir os IDs de controles 1/2 com os valores efetivos Yes/No**: ainda não se identificou quem escreve `dialog+0x10`. Há caminho `FUN_0043E0A0(0)` no construtor sob uma condição virtual, mas não classificamos a condição como cancelamento sem prova.

`FUN_005BFCC0` escreve a resposta e dispara `FUN_005BDBD0`. Esta guarda parâmetros de continuação em `0x00A55050/54/58`; `FUN_005BEE80` encaminha para `FUN_005BF230` apenas se `byte[DAT_005F2FB8+0x1068] != 0`. Não afirmar que o efeito é resolvido imediatamente ao clicar.

## Controle automático por lado

`FUN_0057DC40` calcula `mask=(DAT_00A54E5C >> 1)&3`. Se `(mask >> side)&1` for 1, atribui diretamente `DAT_00A558B4=1` e pula a construção da pergunta humana; na etapa seguinte isso ativa o caminho de custo/comando.

`FUN_005BFEB0` inicializa a word de modo com um bit entre `0x2` e `0x4`; para `sceneMode=7`, faz `mode |= 0x6`. Portanto **os dois lados da demo caem no caminho automático de aceite do P002**, desde que as demais pré-condições sejam satisfeitas. Isso **não prova** política geral para outras cartas, nem que cada fluxo de UI seja idêntico.

## Vtable e fechamento da janela

Vtable final de tipo 1 em `0x005DAD68`, decodificada little-endian:

| Offset | Ponteiro |
| --- | --- |
| `+0x1C` | `0x0043FB60` |
| `+0x20` | `0x0043FAF0` |
| `+0x24` | `0x0043FAA0` |
| `+0x28` | `0x004481F0` |
| `+0x2C` | `0x004481F0` |

Depois de `FUN_0043E0A0`, o contador `this+0x14` vira `-1`; em atualizações seguintes de `FUN_0043E280`, diminui `0x20` por chamada e fecha quando `counter < -0xFF`. Isso sugere **oito atualizações sem interferências** para finalização visual; **não converter para milissegundos** sem medir a frequência do tick. A vtable `0x005DACC4` pertence ao controle/botão, não à janela final.

## Comparação com o sandbox e pendências

O sandbox `EffectPromptMode.Off/Automatic` recusa custos opcionais sem estratégia injetada, **intencionalmente diferente** do caminho automático afirmativo demonstrado para P002 na demo. Não trocar a política do C# sem projetar o controlador de decisões. A correção Draw → hook pós-Draw já foi integrada no [PR #27](https://github.com/ffdiego/YugiRE/pull/27).

Pendências: significado exato de Yes/No e cancelamento no campo `this+0x10`; contratos de `0x00578460`/`0x00579790`; sincronização efetiva de fase; política no modo normal versus demo; owner/controller e identidade de ocorrência em GY. **Status continua Experimental/Sandbox; não Verified.**

**Proveniência operacional:** relatórios locais `p002-revival-jam-decompiled.txt`, `p002-prompt-router.txt`, `p002-ui-dispatch.txt`, `p002-duel-mode.txt`, `p002-yesno-callback.txt`, `p002-button-values.txt`, `p002-continuation.txt`, `p002-dialog-vtable.txt` (pasta `docs/evidence/` no Fedora). Os dumps integrais locais não são anexados a este commit.
