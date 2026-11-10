# Checkpoint — seleção de decks da CPU

## Objetivo atual
Fechar o contrato dos bytes de perfil `0x00A54DB3` e `0x00A54DB4`, o RNG
de `0x0055C140` e a relação entre suas tabelas e as estrelas exibidas.
Trabalho exclusivamente estático no Joey; branch `main`.

## Descobertas confirmadas
- EXE SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
  Endereços são VA, base `0x00400000`.
- Cena 0/1 em `0x0044BEE0` carrega deck do jogador por `0x0044AA00`, depois
  chama `0x0044A920(1, caminho)` em `0x0044C0E3` (retorno `0x0044C0E8`).
  O caminho vem do segundo argumento da configuração de cena.
- `0x0044A920` abre o recurso por `0x0055DA10` e encaminha tipo 1 para
  `0x0044A690`, que desserializa no source slot 1 `0x00A54F42`.
- **Seletor efetivo: `0x0044BBE0`**, chamado antes da configuração de cena.
  Byte `A54DB4 == 0`: normaliza `A54DB3` a 1..7, usa cinco posições por nível
  na tabela `0x005DB158`, nome `data/J/file/CPU_NNN.ydc`.
  Byte não zero: `clamp(A54DB3 + 2*A54DB4,3,9)`; contagens em `0x005DB080`,
  matriz de sete posições por linha em `0x005DB0A8`; nome `data/J/file/FUL_*.ydc`.
- Ambos usam `0x0055C140` e redução por módulo; não é `0x005BDDB0` do shuffle.
- Recursos CPU/FUL correspondentes existem no DAT. Não é necessário gerar
  composição procedural para explicar essa rota.

## Hipóteses em investigação
- `A54DB3` representa dificuldade base e `A54DB4` configuração do pool.
  **Semântica ainda não confirmada:** seguir leitores/escritores e UI.
- `0x0055C140` é um gerador de estado 624 palavras, twist/tempering estilo MT;
  global `0x005F25E8`. Seed, inicialização e chamadas indiretas em investigação.
- Condições de progressão e caminhos alternativos ainda não fechados.

## Caminhos descartados / limites
- Não deduzir quantidade de decks normais dos três presets observados no Demo.
- `0x0044A920` carrega um caminho já escolhido; seguir só esse loader perde o seletor.
- Linha zero da tabela CPU e índices 0..2 de contagens FUL não são linhas válidas.
- Ghidra 12.2 existente e wrapper funcionais; não reimportar/reanalisar.
- Alterações locais preexistentes em docs/15, scripts/README e wrapper,
  mais evidência toolchain, devem permanecer fora dos commits desta pesquisa.

## Próximos passos prioritários
1. Ler `evidence/cpu-deck-rng-seed-method.txt` e `cpu-deck-rng-vtable-and-progression-gates.txt`.
2. Fechar seed/inicialização do RNG e progresso: `0x00446DF0`, `0x0044BAF0`,
   `0x005BBF60`, UI `0x00489490` e opções `0x00488F50` / `0x0048AA10`.
3. Tabelas já extraídas e validadas: 21 CPU + 37 FUL distintos, 58 YDC válidos.
4. Seguir callers do seletor em cenas 0/1, Demo e rematch.
5. Implementar modelo verificável e pseudocódigo C#; preparar falsificadores Windows.

## Última ação realizada
Consultas Ghidra em `cpu-deck-normal-entry.txt`, `cpu-deck-stream-and-callsite.txt`;
inspeção de instruções do seletor e tabelas no EXE, inventário direcionado do DAT.
Extração completa em `cpu-deck-selection-tables.json`, quatro testes passaram.
Relatório técnico em `cpu-deck-selection.md`. RNG construtor `0x0040F150`
coloca índice 625; ainda conferir método virtual `0x0040F180` que pode semear.
**Ler as duas consultas listadas no próximo passo antes de repeti-las.**

## Commits relevantes
- `e1bd36c`: cadeia cena→loader→recurso e seletor anterior, checkpoint inicial.
- Unidade seguinte: tabelas extraídas, YDC conferidos e primeiros campos do perfil.

## Bloqueios
Nenhum para seguir estaticamente. A captura normal de 1 estrela descrita pelo
operador ainda não foi correlacionada a caminho/estado exatos; não executar
Joey no Fedora nem usar `native_rules.py --spawn`. Se necessário, definir
experimento Windows passivo com endereço, momento e valores esperados.

## Evidências a consultar
- `../evidence/joey-star-level-vs-demo-deck-selection-2026-11-09.md`
- `../evidence/cpu-deck-normal-entry.txt`
- `../evidence/cpu-deck-stream-and-callsite.txt`
- `../evidence/cpu-deck-profile-and-rng.txt`
- [Relatório técnico](cpu-deck-selection.md) e arquivos de evidência nele referenciados.
