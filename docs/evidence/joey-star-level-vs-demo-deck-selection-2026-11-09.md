# Decks do Joey por estrelas: separar duelo normal de modo Demo

**Data da captura:** 2026-11-09 UTC (20 duelos). **Arquivo recebido:**
`parallel-4ppi9kaj.zip`. **EXE**:
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

## Hipótese do operador: deck conforme nível de estrelas

O usuário observa no duelo normal que a dificuldade sobe com
vitórias, cai com derrotas e pode modificar os decks do Joey.
Fontes externas descrevem **1–9 estrelas**, maior dificuldade
com vitórias e decks mais fortes, com **múltiplos decks nos
níveis altos (8–9)**, sujeitos às configurações de cartas
permitidas. Isso apoia a existência de seleção por dificuldade,
mas **não revela o formato de uma tabela nem o endereço no EXE**.

Referências públicas:
- https://gamefaqs.gamespot.com/pc/920746-yu-gi-oh-power-of-chaos-joey-the-passion/faqs/30509
- https://ftp.yugipedia.com/wiki/Yu-Gi-Oh%21_Power_of_Chaos%3A_Joey_the_Passion

## Evidência diretamente nos dois processos da captura

O ZIP contém `captures/duels-20261109-032311-421368-pid16748.jsonl`
e `captures/duels-20261109-032312-665106-pid8212.jsonl`.
Ambos apresentaram o MESMO caminho inicial de carregamento:

| Etapa | Cena | Função | Caller/return | Parâmetro bruto | Resultado |
|---|---:|---|---|---:|---|
| Carregamento inicial, seq 4–5 | 0 | `FUN_0044A920` | `0x0044AA3C` | 0 | Source 0 recebe deck de **40** cartas |
| Solicita demo, seq 12 | 0→7 | `FUN_0044BEE0` | `0x0048A555` | 7 | Prepara mudança para o **modo Demo** |
| Primeiro carregamento do modo 7, seq 13–14 | 7 | `FUN_0044A920` | `0x0044BF7A` | 1 | Source 1 recebe preset A de **40** cartas |
| Cópia de `0xE2` bytes | 7 | código de `FUN_0044BEE0` | — | — | Source 1 → source 0 |
| Segundo carregamento, seq 15–16 | 7 | `FUN_0044A920` | `0x0044BFDD` | 1 | Source 1 recebe preset B de **43** cartas |

A composição **pré-Demo** é diferente dos três
`demo-A/B/C.json`, com SHA256 dos índices internos
separados por vírgula:
`8997db7c7ceaffc0...` (40 cartas).
Os três presets Demo observados em 20 duelos têm hashes
(prefixos):
- A, 40 cartas: `496007b5faea92cc`
- B, 43 cartas: `289c432cf5d9a70a`
- C, 41 cartas: `34093b7373a6c011`.

**Nova conclusão concreta:** existe pelo menos **um
caminho de carregamento fora do modo 7**, que lê
ou prepara uma composição distinta antes do timeout
da demonstração. Isso **não prova** se tal composição
é um deck do jogador, um preset inicial, um deck
do Joey de certa estrela ou um save. A função
`FUN_0044A920` recebe 0 nesse caminho, contra 1
nos dois carregamentos do Demo.

## O que os 20 duelos NÃO testaram

- Não houve duelo normal iniciado pelo menu de Duelo.
- Não houve mudança de estrelas entre os processos;
  ambos estavam na mesma instalação/perfil.
- Não foi observada leitura do campo de estrelas nem
  uma tabela `estrela → deck`.
- Portanto, **três decks no modo Demo NÃO limitam a
  quantidade de decks dos duelos normais**.
- Tampouco foi demonstrado que a seleção no Demo
  seja independente das estrelas: os 20 duelos não
  alteraram o nível do perfil.

## Próxima investigação ordenada

1. **Dinâmica primeiro:** anexe Frida sem DLL ao Joey
   no menu, use `trace on 20000`, `trace demo on`;
   inicie **um duelo normal**, deixe completar o
   bootstrap de decks e use `trace off`/`trace dump`.
   Registre separadamente o número de estrelas
   mostrado na interface (não está no JSONL).
   Deve-se verificar se `scene_config`, `deck_prepare`
   e `deck_resource_step` são atingidos nesses modos.
2. Compare pelo menos **dois níveis de estrelas reais**,
   preferencialmente no mesmo perfil após progressão
   normal. **Não modificar um save protegido ou seu
   checksum** para simular estrelas sem conhecer o formato.
3. No Ghidra, priorize `FUN_0044A920`, o chamador
   `0x0044AA3C`, o corpo de `FUN_0044BEE0`
   e qualquer outra rota que configure uma cena de
   duelo normal. Seguir leitura da dificuldade e
   seleção de presets/tabela antes da cópia e do shuffle.
4. Extrair composição `main` antes do shuffle por
   índice de estrela, configuração de pool de cartas
   e modo de jogo. Comparar conjuntos completos
   de índices internos e frequências dentro de um
   mesmo nível para detectar sorteio de múltiplos decks.
5. Só depois converter essas composições em catálogo
   tipado de estratégias/arquetipos para o C#.

**Fato:** a documentação pública sustenta dificuldade
graduada com troca de decks; os dados nativos desta
captura sustentam o caminho separado de carregamento.
**Hipótese em aberto:** implementação exata da seleção
como lookup table, índice interno de estrelas ou pesos.
