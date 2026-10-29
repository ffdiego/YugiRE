# Continuação do callback de Parasite Paracide

Décima segunda rodada, 2026-10-29. Análise estática; VA, base `0x00400000`.
O ramo 0x0058766E, selecionado quando `pedido+2 & 0x3000 == 0x2000`,
trata três valores do byte global em **0x00A55C8A**. A atribuição de ID 762
ao callback foi confirmada na [décima rodada](13-parasite-creation.md).

## Resultado

O ramo localiza uma ocorrência pela chave, verifica posições elegíveis e
emite operações conforme o estado da continuação. Há um caminho que prepara
colocação da carta numa posição e outro que emite o comando 0x79.
Uma etapa posterior insere tipo 13 na lista de metadados da posição escolhida
e envia **1000** ao helper que emite o comando **0x43**.

Confiança alta no fluxo e nos argumentos. A interpretação de 0x43 como dano
ou redução de pontos de vida **ainda não foi comprovada pelo consumidor**.
Também não foi demonstrado o efeito completo da carta por execução do jogo.

## Estados e operações

| Byte em 0x00A55C8A | Operações confirmadas |
|---|---|
| 0x80 | Extrai a chave de pedido+4; procura uma palavra de estado; decide entre dois caminhos |
| 0x7F | Seleciona uma posição elegível e chama 0x005ADA20 com a palavra encontrada anteriormente |
| 0x7E | Emite inserção de metadados tipo 13 para uma posição e chama 0x005784E0(s,1000) |

Outros valores alcançam retorno AX=0. Esses números são códigos de
continuação; não são os opcodes do dispatcher de comandos de oito bytes.
O caller em 0x005BBA78 armazena o byte retornado pelo callback em 0x00A55C8A,
como mostram as instruções já exportadas na rodada anterior.

### Estado 0x80 — localizar e escolher

Define `s = uint16[pedido+2] & 1` e `k = (uint16[pedido+4] >> 5) & 255`.
Chama **0x00568B30(s,k)**. Esse helper percorre palavras uint32 a partir de
**0x00A56434 + s*0xD44**, com contador byte em **0x00A55D68 + s*0xD44**;
compara a mesma chave `((w>>12)&1) + 2*((w>>24)&127)`. Retorna a posição
zero-based encontrada ou -1. Não compara ID nesse helper.

Se não encontra, retorna AX=0. Se encontra, consulta **0x0056A000(s)** e
**0x00570B40(s)**. Quando ambos são não zero, guarda a posição no byte
**0x00A55C8B** e retorna **0x7F**. Caso contrário, chama
**0x00579310(s,posicao,ponteiro_para_palavra,0)** e retorna zero.

0x0056A000 conta resultados positivos de 0x00569F70 para posições 0–4.
0x00569F70 exige índice zero no registro de passo 0x90 e valida condições
adicionais por 0x00569E10. Portanto são posições vazias e elegíveis segundo
essas condições; não se trata somente de contar todos os índices zero.
0x00570B40 verifica condições adicionais não interpretadas integralmente.

0x00579310 encapsula 0x00579330 com código de região 11. Este emite
**0x79**, além de outras notificações e condições específicas. Não chamamos
esse caminho de descarte/destruição sem seguir o consumidor de 0x79.

### Estado 0x7F — preparar colocação

**0x0056A030(s)** seleciona uma das posições elegíveis 0–4, com tratamento
adicional de registros temporários. Seu byte baixo é salvo em **0x00A55C8C**.
A posição da palavra de estado é recuperada de 0x00A55C8B; calcula-se novamente
seu ponteiro em 0x00A56434. O callback chama:

```text
0x005ADA20(s, ponteiro, 1, 1, 1, 11, posicao_da_palavra)
```

Esse helper verifica uma posição elegível e prepara globais
0x00A55080/84/88/8C/8E, copiando a palavra por 0x005675C0, antes de chamar
0x005AD8C0. Não seguimos a conclusão dessa operação nem provamos que ela
altere imediatamente um registro do campo. O callback retorna **0x7E**.

### Estado 0x7E — metadados da posição e valor 1000

Chama **0x005747E0(s, indice_do_pedido, s | (posicao<<8), 13 | (s<<8))**,
com `posicao = uint8[0x00A55C8C]`. Pela inserção genérica já validada,
isso anexa índice/metadados à lista da **posição**, cujo contador fica em
`0x00A55D7E + s*0xD44 + posicao*0x90`.

Essa lista difere da categoria 11 usada na associação global inicial.
Aqui, o byte alto dos metadados vale **s**, e não a chave completa `k`.
Não interpretar todo nibble 13 do executável como um único layout universal.

Depois chama **0x005784E0(s,1000)**. O helper emite um registro com
opcode `0x43 | (s ? 0x8000 : 0)`, segundo campo 1000, terceiro zero e quarto
um; também chama 0x00598590 com código 0x10. O ramo termina com AX=0.
O significado do opcode e sua alteração concreta no estado permanecem abertos.

## Evidências e validação

- [Ramo e estados](evidence/parasite-second-branch.txt).
- [Wrapper do caminho alternativo](evidence/parasite-second-branch-helpers.txt).
- [Busca pela chave e helpers](evidence/parasite-second-branch-resolve.txt).
- [Estado 0x7E e preparação de colocação](evidence/parasite-second-branch-completion.txt).
- [Emissor do comando 0x43](evidence/parasite-value-command.txt).

Regiões sem listing foram desassembladas transitoriamente; instruções de
funções adjacentes não usadas foram omitidas de alguns relatórios.

```bash
PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_parasite_continuation.py
```

Esperado: cinco linhas `PASS`. Revalida originais/banco e rodadas anteriores,
mais oito sequências de instruções desta rodada. O teste não executa o alvo.
[Relatório](evidence/parasite-continuation-validation.json).

Próximo incremento: seguir somente o consumidor de **0x43**, confirmando a
semântica do valor 1000 e o destinatário. Colocação final, produtor inicial
e significado completo dos campos continuam pendentes.
