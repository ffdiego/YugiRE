# Seleção de decks da CPU — especificação estática em construção

Alvo Joey, SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Endereços VA, base `0x00400000`. Sem executar o jogo ou alterar originais/saves.
Estado operacional e prioridades: [checkpoint](cpu-deck-selection-checkpoint.md).

## Seleção precede o carregamento

`0x0044BBE0` monta o caminho. Chamadores passam esse caminho à configuração
de cena `0x0044BEE0`. Nas cenas 0/1, ela carrega o deck do jogador com
`0x0044AA00` e então chama `0x0044A920(1, caminho)` em `0x0044C0E3`, retorno
`0x0044C0E8`. O tipo 1 seleciona `0x0044A690`, que lê o YDC no source slot 1
`0x00A54F42`. `0x00569380` transfere depois a composição para o duelo;
`0x00568D80` embaralha. Não confundir esses estágios com a escolha do recurso.

## Algoritmo do caminho — comprovado

`base` é o byte **sem sinal** em `0x00A54DB3`; `pool` é o byte em
`0x00A54DB4`. São campos distintos. A semântica de progressão será fechada
separadamente; não chamar `base` sozinho de quantidade de estrelas universal.

```text
if pool == 0:
    level = clamp(base, 1, 7)
    roll = RNG_0055C140(global_005F25E8)
    number = CPU_TABLE[level * 5 + (roll % 5)]
    return "data/J/file/CPU_" + decimal(number, width=3) + ".ydc"
else:
    level = clamp(base + 2 * pool, 3, 9)
    count = FUL_COUNTS[level]
    slot = count == 0 ? 0 : RNG_0055C140(global_005F25E8) % count
    return "data/J/file/FUL_" + FUL_TABLE[(level - 3) * 7 + slot] + ".ydc"
```

As sete contagens FUL reais são não nulas, logo toda seleção válida consome
um valor do gerador. O módulo é divisão **sem sinal de 32 bits**, conforme
`DIV` em `0x0044BC86` e o ramo CPU. Não há rejeição de resultado repetido
nem pesos adicionais dentro desta função. O módulo introduz o viés residual
usual se a distribuição de entrada for uniforme e sua cardinalidade não
for divisível pela quantidade de opções; uniformidade de execução não foi medida.

## Opções por nível

Tabelas CPU `0x005DB158`, contagens FUL `0x005DB080`, nomes FUL `0x005DB0A8`.
Não interpretar a linha zero CPU nem os índices FUL inferiores a três:
estão fora do domínio usado pelo seletor e incluem dados adjacentes.

| Nível efetivo | CPU, `pool=0` (sufixos) | FUL, `pool!=0` (sufixos) |
|---|---|---|
| 1 | 000,001,002,003,004 | — |
| 2 | 003,004,010,011,012 | — |
| 3 | 010,011,012,013,014 | 0GE1,0UMI,0VAL |
| 4 | 013,014,015,020,021 | 1DEC,1RED,1TOO,1INS,1UIJ,1UMI,1VAL |
| 5 | 020,021,022,023,024 | 1FUS,1GAM,1GE1,1GE2,1HIK,1MIX |
| 6 | 023,024,025,026,027 | 2DEC,2VAL,2TOO,2GE1,2HIK,2INS,2UIJ |
| 7 | 025,026,027,028,029 | 2FUS,2GAM,2GE2,2MIX,2RED,2UMI |
| 8 | — | 3ALL,3DEC,3YAM,3FUS,3TOO,3HND |
| 9 | — | 3ALL,3DEC,3EXO,3FUS,3GRV,3HND |

Há 35 posições CPU e 41 posições FUL, mas **21 e 37 recursos distintos**,
respectivamente. Sobreposição de opções entre níveis é deliberada nas tabelas;
subir de nível não força um deck diferente. Níveis FUL 8 e 9 compartilham quatro
das seis opções; os sufixos não constituem prova dos efeitos/estratégias do deck.

`scripts/extract_cpu_deck_selection.py` confere fingerprints EXE/DAT, lê as
tabelas e valida cada YDC diretamente no arquivo original, sem exportar cartas.
As 58 referências existem, todas sem compressão nessas entradas, todas com
listas válidas e consumo integral do recurso. Lista principal entre 40 e 45
cartas; contagens das demais listas e hashes em
`../evidence/cpu-deck-selection-tables.json`. A natureza completa das listas
adicionais permanece fora do escopo deste passo.

Reprodução:

```sh
python scripts/extract_cpu_deck_selection.py ../passion/joey_pc.exe ../passion/data.dat
python -m unittest discover -s tests -p test_cpu_deck_tables.py
```

A captura descrita de CPU com 43 cartas e perfil de uma estrela é compatível
com `CPU_002.ydc`, a única opção com 43 cartas na linha CPU 1. **Isso é só
compatibilidade por tamanho**, não identificação sem comparar caminho ou composição.

## Perfil e opções — evidências iniciais

`0x005BEDD0` percorre tipos possuídos. Retorna 1 ao encontrar carta elegível
pela máscara acumulada de edições (`0x005BEA60`) e ausente na máscara corrente
(`0x005BEA40`). No Joey, são máscaras 7 e 4. É detecção de posse de carta de
edição anterior; não consulta diretamente a presença dos outros executáveis.

`0x005BEE30`, chamado por `0x004834A0`, inicializa o perfil quando `A54DB1==0`.
Ele escreve dificuldade anterior/atual e depois define o byte de pool pelo
resultado de `0x005BEDD0`. A ordem usa o **valor anterior** de `A54DB4` no cálculo
inicial `1 - 2*pool`, com truncamento para byte; não simplificar antes de fechar
as pré-condições de inicialização. `0x0048AA10` também escreve o pool com um
booleano (`opção == 7`); não confundir auto-habilitação e opção selecionada.

`0x00446DF0` modifica `A54DB3` com base em resultados e sequência consecutiva,
com tabelas de limiares. O gráfico de estrelas em `0x00489490` usa
`A54DB3 + 2*A54DB4` e uma animação transitória em relação a `A54DB2`.
O mapeamento completo dos gates, resultado, desenho estável e sequência
fica para a próxima unidade verificável.

## Evidências

- `../evidence/cpu-deck-normal-entry.txt`
- `../evidence/cpu-deck-stream-and-callsite.txt`
- `../evidence/cpu-deck-profile-and-rng.txt`
- `../evidence/cpu-deck-pool-progression.txt`
- `../evidence/cpu-deck-pool-options-and-result.txt`
- `../evidence/cpu-deck-selection-tables.json`
