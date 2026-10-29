# Man-Eater Bug — seleção e revalidação de alvo

Análise estática em 2026-10-29. Alvo SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Endereços são VA, image base `0x00400000`. Consultas Ghidra 12.2 com
`-noanalysis -readOnly`; jogo não executado e banco não salvo.

## Resultado e registros

O pedido guarda o alvo como uma palavra de 16 bits: lado, posição e chave
de ocorrência. A resolução exige que a posição continue ocupada por uma
palavra de estado com a chave registrada. A chave de oito bits não é uma
garantia de identidade ilimitada; reutilização e colisões não foram estudadas.

| Carta | Índice | ID | Registro de 24 bytes |
|---|---:|---:|---:|
| Man-Eater Bug | 753 | 500 / 0x1F4 | 91 |
| Tribute to The Doomed | 363 | 1023 / 0x3FF | 187 |

Na tabela em `0x005ED0A8`, ambas têm **0x00585C80 em +4** e
**0x005944D0 em +20**. O slot +8 difere: **0x0057A930** para Man-Eater
Bug e **0x0057A9A0** para Tribute. Man-Eater Bug tem +12/+16 nulos;
Tribute tem 0x00580D20/0x0057D7C0. A continuação em
[custo de Tribute](tribute-discard-cost.md) reconstrói esses dois callbacks
e identifica 0x00583510 como caso Big Shield Gardna. Identidades e descrições
foram lidas dos recursos locais.

## Candidatos e gravação

**0x0059DC50(pedido, lado, posição)** procura o registro por ID e chama
o slot +8 com `lado | posição << 8`. **0x0059DCA0** usa esse mesmo slot
para contar candidatos, percorrendo dois lados e onze posições por lado.

O filtro de Man-Eater Bug, **0x0057A930**, rejeita posições >=5, consulta
**0x0056C510** e exige índice não zero nos bits 0–11 da palavra em
`0x00A55D74 + (lado & 1)*0xD44 + posição*0x90`.
Não exige lado oposto nem testa orientação diretamente nesse trecho.
0x0056C510 também pode rejeitar por uma condição global envolvendo ID
0x2E4, flags da posição e 0x0056F6D0; a semântica dessa exceção continua
aberta. Portanto, não concluímos que todo monstro ocupado seja sempre elegível.
O filtro de Tribute compartilha o início, mas acrescenta uma condição
envolvendo a classe 0x16 e 0x0056C460; não é um filtro idêntico.

**0x005944D0** escolhe entre 0x005B3FA0 e **0x00593440**, segundo flags
globais em 0x00A54E5C. Não atribuímos aqui significado humano/IA a esses bits.
No caminho 0x00593440, o estado inicial zera os três bits altos de pedido+4
e conta candidatos. Depois, usa os globais de seleção, chama 0x0059DC50
para validar o candidato e **0x00592B00** para confirmá-lo. O caminho de
confirmação passa por **0x00592A90 → 0x005929F0 → 0x00592A50**.

0x005929F0 lê a palavra de estado `w` da posição e constrói:

```text
chave(w) = ((w >> 12) & 1) + 2*((w >> 24) & 0x7F)
entrada = (lado & 0x0F) | (posição << 4) | (chave(w) << 8)
```

0x00592A50 acrescenta essa palavra em `pedido+6 + contador*2` e incrementa
o contador nos bits 13–15 de pedido+4, preservando os 13 bits baixos.
Para lados 0/1 e posições 0–4, o layout é:

| Bits da entrada | Uso observado |
|---|---|
| 0 | lado do estado consultado |
| 1–3 | preservados do nibble baixo do argumento; significado aberto |
| 4–7 | posição |
| 8–15 | chave de ocorrência |

Essa entrada é diferente do destinatário simples 0/1 usado por
[Sparks/Ookazi](sparks-ookazi.md), embora ambas usem a mesma lista em pedido+6.
O consumidor determina como interpretar a palavra.

## Resolução

O callback **0x00585C80**, compartilhado pelas duas cartas:

1. Evita o trecho de efeito se `uint8[pedido+4] & 4` estiver definido.
   O significado completo desse bit ainda não foi estabelecido.
2. Chama **0x00583470(pedido, 0, &local)** para resolver a primeira entrada.
3. Só prossegue se houver entrada, posição ocupada e chave correspondente.
4. Para ID 0x3FF (Tribute) ou 0x4BB, exige também sucesso de 0x00583510.
   Man-Eater Bug não passa por essa condição por ID.
5. Chama **0x00577880(pedido, lado, posição)**, que constrói máscara
   `1 << (lado*16 + posição)` e passa a **0x00576940**.
   Este encaminha a `0x00576960(pedido, máscara, 14, 2)`.
6. Termina com **AX=0**, confirmado em 0x00585D1E. Bits altos no retorno
   do pseudocódigo não estabelecem um contrato adicional.

0x00583470 extrai posição do nibble alto do byte baixo e lado do bit 0;
escreve também o nibble baixo e a posição no parâmetro de saída. Rejeita
`índice >= (uint16[pedido+4] >> 13)`, palavra vazia (`w & 0xFFF == 0`) e
chave diferente. **Não repete o filtro +8** nesse helper nem busca a chave
em outra posição. Trocar a chave na posição invalida essa referência;
conservar a mesma chave em outra posição não a redireciona automaticamente.

O helper lê os bytes da entrada antes de verificar o contador e não verifica
limites de posição no trecho examinado. Isso descreve um caminho para pedidos
internos válidos, não um parser defensivo para dados arbitrários.

Esta etapa fecha seleção, codificação e revalidação do alvo. A continuação em
[destruição por efeito](monster-destruction.md) segue a emissão e o consumidor
até retirada da origem e listas de destino, incluindo Banisher e fichas.
O custo foi seguido na continuação de Tribute; animação e matriz completa
de interações continuam abertos.

## Evidências e reprodução

- [Callback de resolução](../evidence/monster-target-callbacks.txt).
- [Filtro e leitor de alvo](../evidence/monster-target-selection.txt).
- [Máquina de seleção e helper comum](../evidence/monster-target-write.txt).
- [Dispatch de candidatos e filtro de Tribute](../evidence/monster-target-encoding.txt).
- [Listing do leitor e confirmação](../evidence/monster-target-entry.txt).
- [Empacotamento e comparação de chave](../evidence/monster-target-pack.txt).
- [Validação reproduzível](../evidence/monster-target-validation.json).

```bash
PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_monster_targets.py
```

Esperado: sete linhas `PASS`. Revalida preservação dos originais/banco,
identidades, registros e trechos de instruções. O modelo Python cobre
2560 combinações de lado/posição/chave, 652800 rejeições de chave diferente,
2560 posições vazias e 2560 entradas ausentes. É um teste das fórmulas
transcritas, não execução nem prova independente de equivalência do código nativo.

Experimentos futuros: selecionar e resolver sem mudança de campo; remover o
alvo antes da resolução; ocupar o mesmo espaço com outra chave; mover a carta
para outra posição. Registrar pedido e palavra de estado antes/depois para
separar revalidação da chave de regras adicionais do fluxo de destruição.
