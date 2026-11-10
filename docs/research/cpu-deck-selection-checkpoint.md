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
- `0x0055C140` pode usar estado independente do CRT; seguir implementação.
- Condições de progressão e caminhos alternativos ainda não fechados.

## Caminhos descartados / limites
- Não deduzir quantidade de decks normais dos três presets observados no Demo.
- `0x0044A920` carrega um caminho já escolhido; seguir só esse loader perde o seletor.
- Linha zero da tabela CPU e índices 0..2 de contagens FUL não são linhas válidas.
- Ghidra 12.2 existente e wrapper funcionais; não reimportar/reanalisar.
- Alterações locais preexistentes em docs/15, scripts/README e wrapper,
  mais evidência toolchain, devem permanecer fora dos commits desta pesquisa.

## Próximos passos prioritários
1. Ler `evidence/cpu-deck-profile-and-rng.txt` assim que terminar a consulta.
2. Seguir xrefs de `A54DB3/A54DB4`, fechar getters, progresso e UI das estrelas.
3. Extrair tabelas com fingerprint e correlacionar cada entrada ao DAT.
4. Seguir callers do seletor em cenas 0/1, Demo e rematch.
5. Implementar modelo verificável e pseudocódigo C#; preparar falsificadores Windows.

## Última ação realizada
Consultas Ghidra em `cpu-deck-normal-entry.txt`, `cpu-deck-stream-and-callsite.txt`;
inspeção de instruções do seletor e tabelas no EXE, inventário direcionado do DAT.
Consulta de RNG e referências do perfil em execução/recém-finalizada:
`cpu-deck-profile-and-rng.txt`. **Ler esse resultado antes de outra consulta.**

## Commits relevantes
Esta primeira unidade registra a cadeia cena→loader→recurso e o seletor anterior.
Hashes das próximas unidades serão adicionados após os commits.

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
