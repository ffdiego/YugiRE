# Checkpoint — seleção de decks da CPU

## Objetivo atual
Finalizar limites da especificação, anotações persistentes e protocolo Windows
passivo. Presets, resolução de recursos e caminhos normal/match/Demo já mapeados.
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
- `0x0055C140` é MT compartilhado UI/deck. Construtor `0x0040F150` põe índice
  625; primeiro Next usa seed 4357 via `0x0055C0E0` (LCG 69069 duas vezes/palavra).
  `0x0040F180` é **leitor de snapshot**, não seed; consumidor virtual pendente.
- `0x00446DF0` guarda sequência de resultado=1 versus demais em `A54DAF/DB0`;
  muda base só quando `005F3254 != 0`, com limiares `005DAF40/005DAF64`.
  `0x0044B950`: modos 0,1,3; `0x0044B970`: modo1 (match), `0x0044B980`: modo3.
- Progresso transcrito em `CpuDifficultyProgression.cs`: limiares, agregação de
  match, saturação, flag40 e modo3. Build e 255 verificações passaram.
- Escritor da flag40: comando40 → `00408410`, zera LP e seta0x240; semântica
  provável desistência/aborto, produtor UI ainda não identificado.
- Presets A/B/C conferem **as três listas e ordem** com FUL_0GE1/0VAL/0UMI.
  As listas adicionais do arquivo trocam ordem no source; 341 checks C# passaram.
- Rematch cena2 não sorteia/recarrega deck; novos modos0/1 o fazem.
  Demo7 sorteia dois caminhos, primeiro o externo, depois o interno; carrega
  interno no source0 e externo no source1 antes da distribuição aos lados.
- Condições de progressão e caminhos alternativos ainda não fechados.

## Caminhos descartados / limites
- Não deduzir quantidade de decks normais dos três presets observados no Demo.
- `0x0044A920` carrega um caminho já escolhido; seguir só esse loader perde o seletor.
- Linha zero da tabela CPU e índices 0..2 de contagens FUL não são linhas válidas.
- Ghidra 12.2 existente e wrapper funcionais; não reimportar/reanalisar.
- Alterações locais preexistentes em docs/15, scripts/README e wrapper,
  mais evidência toolchain, devem permanecer fora dos commits desta pesquisa.

## Próximos passos prioritários
1. Conferir relatório `cpu-deck-ghidra-symbols.txt` e script de renomeação:
   execução persistente já concluída com Save succeeded e 12 funções nomeadas.
2. Consolidar protocolo Windows: breakpoint no seletor, MT antes/depois, loader,
   bytes de perfil e rematch. Não usar spawn nem alterar perfil/save.
3. Tabelas já extraídas e validadas: 21 CPU + 37 FUL distintos, 58 YDC válidos.
4. Seguir callers do seletor em cenas 0/1, Demo e rematch.
5. Implementar modelo verificável e pseudocódigo C#; preparar falsificadores Windows.

## Última ação realizada
Consultas Ghidra em `cpu-deck-normal-entry.txt`, `cpu-deck-stream-and-callsite.txt`;
inspeção de instruções do seletor e tabelas no EXE, inventário direcionado do DAT.
Build e 341 verificações C# passaram, incluindo 58 recursos originais e
capturas das três listas. Quatro testes Python do extrator passaram.
`RenameCpuDeckSelection.java` já executado: 12 funções nomeadas no banco,
Save succeeded. Uma primeira execução falhou por cache path relativo;
segunda execução usou caminhos absolutos e concluiu. Não repetir sem necessidade.

## Commits relevantes
- `e1bd36c`: cadeia cena→loader→recurso e seletor anterior, checkpoint inicial.
- `5458908`: tabelas extraídas, 58 YDC conferidos e primeiros campos do perfil.
- `c560b9e`: seletor C# e RNG MT transcritos, 221 verificações.
- `36399ce`: progressão do perfil, gates e consulta direcionada de escritores.
- Próxima unidade: correspondência dos presets e confirmação dos pools com DAT.

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
