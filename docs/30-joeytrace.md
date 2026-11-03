# JoeyTrace v1 — evidências contínuas do duelo original

Estado: **implementação funcional do coletor externo**, testada com dados sintéticos e integração do parser YDR em Linux. **A versão original já foi usada pelo operador em cinco sessões reais; as novas leituras da mão e dos bits terminais ainda precisam de validação no Windows.** Não existe hook de comandos nativos nesta versão.

## Objetivo

Executar o `joey_pc.exe` normalmente e produzir uma linha do tempo das **mudanças observadas** nas estruturas de memória já estudadas, além de marcadores manuais e importação posterior do `.ydr` salvo ao fim da partida.

O coletor não tenta inferir causalidade: `hand_count_changed` não prova compra/descarte/invocação. Os comandos YDR importados **não têm timestamps compatíveis** com os da captura RAM; nenhum pareamento temporal é apresentado como comprovado.

## Instalação

Python **3.10+** no Windows 10/11 (incluindo 64-bit); nenhuma dependência PyPI. Arquivos:

- `scripts/joeytrace.py`: CLI, Windows API read-only e replay import.
- `scripts/joeytrace_core.py`: parser de memória, detecção de diferenças e persistência.
- `tests/test_joeytrace.py`: testes offline, com dados sintéticos.
- `scripts/inspect_joey_replay.py`: **já existente** no repositório, usado somente na importação YDR.

**Só aceita** `joey_pc.exe` com SHA-256 `DB8FE7B7EA4B12F18A9BB1777B5F5B8EAB254011DB2A52571A0264725505B6DE`. O layout de memória documentado não é confiável para outros hashes (inclusive versões modificadas para destravar cartas). O programa lê memória; nunca escreve, suspende o processo ou injeta código.

A partir da raiz do repositório YugiRE, no **PowerShell**:

```powershell
py -3 -m unittest discover -s tests -p 'test_joeytrace.py' -v
py -3 scripts/joeytrace.py capture
```

É possível começar a captura antes de abrir o Joey. O coletor espera `joey_pc.exe`, valida o executável, lê dois blocos contíguos e só começa a registrar estados quando duas amostras consecutivas parecem corresponder a um duelo: LP válidos para os dois lados, mãos com quantidades plausíveis e índices de cartas dentro do catálogo conhecido. **Isso é apenas uma heurística**; também não detecta rigorosamente fim/início de outro duelo no mesmo processo. O registro continua após LP chegar a zero.

Durante a partida, **Ctrl+Shift+J** registra um marcador global (por borda de pressionamento, com debounce de 800 ms); não precisa trocar o foco do jogo. Quando voltar ao terminal, **digite a descrição em texto livre e pressione Enter**: o texto será associado ao marcador mais recente em `marker-annotations.jsonl`. Também é possível usar `note descrição` para anotar o último, ou `mark descrição` para criar um novo marcador com texto. Sem marcador anterior, texto livre cria um marcador de console. Digite `stop` ou use Ctrl+C para finalizar e gerar `report.md`.

O atalho antigo F9 causa conflito com o histórico do console Windows, que apresenta **Enter command number:**. Por isso não é mais o padrão; `--hotkey f9` permanece disponível apenas para testes. O novo padrão é `--hotkey ctrl+shift+j`. Para desativar, use `--no-hotkey`. Pressionar o atalho global enquanto o console estiver recebendo comandos pode interferir na digitação; use-o de preferência com o jogo em foco.

## Melhorias após análise dos cinco duelos

O JoeyTrace agora lê os bits de condição terminal já documentados em `docs/23-terminal-conditions.md`: **0x100 (deck-out), 0x200 (Exodia), 0x400 (Destiny Board)**, no word em `0x00A55D6C + lado*0xD44`. São indicadores do estado observado, não provas isoladas de vitória.

A opção **`--hand-cards`** acrescenta aos snapshots a lista de palavras `uint32` das cartas da mão dos dois lados, do array em `0x00A56434 + lado*0xD44`, conforme `docs/21-native-card-draw.md`. Registra índice `word & 0xFFF` e `raw_word_hex` (índice interno, não ID de recurso), além das partes de Exodia observadas. Aplica limite defensivo de 80 posições para evitar leituras fora do bloco; não é um limite nativo comprovado. Compara cartas por palavra completa, preservando repetições. **Revela as cartas ocultas do oponente**, por isso é opt-in; não publique essas capturas brutas.

```powershell
python scripts/joeytrace.py capture --hand-cards --verbose
```

Para diminuir confusões entre partidas, o coletor marca **`possible_duel_reset`** quando ambos os LP voltam a 8000, mãos ficam vazias e as dez zonas de monstro estão livres após um estado ativo. Cada ocorrência aumenta o `segment_id`, presente nas amostras e marcadores. Essa é **apenas uma heurística**: pode perder resets rápidos entre amostras ou gerar falsos positivos. Nenhuma amostra é descartada e o replay YDR não é automaticamente sincronizado aos segmentos.

Opcionalmente, para uma investigação de **bytes ainda desconhecidos**:

```powershell
py -3 scripts/joeytrace.py capture --track-unknown --raw-blocks
```

`--track-unknown` registra alterações que não modificam campos mapeados; essa opção pode gerar logs muito grandes por causa de contadores e temporários internos. `--raw-blocks` salva os blocos de 6792 bytes **completos** a cada evento gravado, para futuras comparações por offset; podem conter dados privados/ocultos da partida. Ambos são opcionais e recomendados somente em sessões curtas de descoberta.

Outras opções: `--hand-cards` (opcional), `--poll-ms 100` (padrão, aceita 20 a 5000), `--duration 120`, `--pid 1234` para selecionar uma instância e `--no-hotkey` para desativar F9. Para exibir cada lote detectado em tempo real, use `--verbose`.

## Arquivos gerados

Em `scratch/traces/duel-AAAAMMDD-HHMMSS-pidNNN/`:

```text
manifest.json             # fingerprint, endereços, intervalo e limites
state-changes.jsonl        # diferenças dos campos mapeados, offsets de bytes brutos
snapshots.jsonl            # baseline e estados após cada lote registrado
markers.jsonl              # marcações F9 e console (e erros de leitura)
report.md                  # relatório gerado ao finalizar
raw-snapshots.bin          # opcional com --raw-blocks
marker-annotations.jsonl   # descrições digitadas ao vivo ou após a partida (append-only)
replay.ydr                 # opcional, anexado depois da partida
replay-commands.csv        # opcional, extraído do YDR
replay-summary.json        # opcional, extraído do YDR com ID do save omitido
```

O `scratch/` já consta no `.gitignore` do projeto. **Não comite os arquivos brutos da RAM ou YDR**. O manifest pode conter caminho local com nome de usuário. Se for compartilhar evidências no Git, revisar o conteúdo e criar amostras reduzidas, sem identificadores locais.

Cada mudança tem `elapsed_ms` (relativo ao início da monitoração), horário UTC, uma lista de campos `before`/`after`, número de bytes alterados e até 32 offsets com valores de antes/depois. O número de offsets pode ser ajustado com `--raw-sample-limit`.

Exemplo de evento (apenas ilustrativo):

```json
{
  "seq": 7,
  "elapsed_ms": 14500,
  "type": "observed_state_difference",
  "changes": [
    {"type": "life_points_changed", "side": 0, "before": 8000, "after": 6600, "delta": -1400}
  ],
  "evidence_method": "non_atomic_polling"
}
```

### Anotar depois de jogar

Se durante a partida você apertou F9 na hora de um efeito, pode acrescentar a descrição **depois**, sem alterar o evento original:

```powershell
py -3 scripts/joeytrace.py annotate --session "scratch/traces/duel-..." --marker 1 --text "Relinquished equipou monstro virado para baixo"
```

Anotações adicionais são *append-only* em `marker-annotations.jsonl`.

### Descoberta automática dos replays e views do Registro

A fonte **comprovada** no binário é `CommonDir` em
`HKLM\SOFTWARE\KONAMI\Yu-Gi-Oh! Power Of Chaos\system`.
O coletor consulta as views 32 e 64 bits pelo `winreg.KEY_WOW64_32KEY` e
`KEY_WOW64_64KEY` (não concatena `WOW6432Node` na chave HKLM), além dos
possíveis caminhos sob `HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE`
com e sem `WOW6432Node`. Também consulta opcionalmente `ReplayDir` caso exista,
mas esse valor **não foi confirmado** no Joey original.

Para diagnosticar as pastas e ver os replays localizados, sem importar nada:

```powershell
python scripts/joeytrace.py list-replays
```

São exibidas as origens, se a pasta existe e os arquivos `.ydr` encontrados
na raiz de cada diretório (ordenados por **data de modificação do arquivo**).
A data mais recente **não comprova** que o replay pertence ao duelo gravado.
O coletor não modifica o Registro.

### Anexar o replay YDR

Depois que terminar a partida e salvar o replay pelo jogo:

```powershell
# Interativo: sugere mais recente; Enter aceita, numero seleciona, caminho manual substitui
py -3 scripts/joeytrace.py import-replay --session "scratch/traces/duel-..."

# Opcional: --file continua aceito para escolha direta
py -3 scripts/joeytrace.py import-replay --session "scratch/traces/duel-..." --file "C:\caminho\replay.ydr"
```

Se encerrar uma captura digitando `stop` no terminal, o programa já oferece
importar o replay ao final da sessão; Enter aceita o candidato mais recente.
Para desativar essa pergunta, capture com `--no-replay-prompt`. Em caso de
`Ctrl+C`, não há pergunta automática, pois o leitor de console pode continuar
bloqueado em `input()`; execute `import-replay --session ...` depois.
Em modo não interativo, informe explicitamente `--file`.

O YDR original é copiado **apenas** para a pasta local ignorada pelo Git. O `replay-summary.json` omite o identificador de save, mas o `.ydr` original continua com seus bytes originais. O parser é exploratório e atualmente baseado em uma amostra documentada em `docs/29-replay-ydr.md`.

### Teste rápido sem jogo

```powershell
py -3 scripts/joeytrace.py demo --output scratch/traces/demo-manual
```

Gera duas mudanças e um marcador **sintéticos**, para conferir o pipeline e os arquivos. A demonstração não significa que a captura Win32 foi validada.

## Correspondência com o snapshot original

O layout foi transcrito sem especulação de `scripts/Get-JoeyDuelSnapshot.ps1`:

- Base virtual original: `0x00A55D64`, relativa a `ImageBase=0x00400000`.
- Leitura: `2 × 0xD44 = 6792` bytes em uma única chamada `ReadProcessMemory`.
- Cada jogador: LP `uint16` em +0, quantidade da mão em +4, flags `uint32` em `+0x6CC`.
- Monstros: zona 0 em `+0x10`, cinco registros espaçados por `0x90`; índice de carta `DWORD & 0xFFF`, occurrence `uint16` em `+4`, posição `uint16` em `+6`, flags `uint32` em `+0x8C`.
- A posição tem flag de bloqueio neste turno quando `flags & 0x20000 != 0`; significado derivado de evidências anteriores.

`card_index` **não** é `resource_id`; não rotular automaticamente as cartas pelo campo errado.

## Limitações e próximos passos

1. **Nenhum hook nativo nesta versão.** A fila de comandos `0x30`, `0x31`, `0x43`, etc. só aparece na importação do `.ydr` salvo; o coletor de RAM não captura seus comandos em tempo real.
2. O sampling não é atômico, não captura estados intermediários rápidos e pode observar transições de múltiplas ações no mesmo lote.
3. O coletor não sabe ainda cemitério, IDs de recurso na mão, deck, fase ou relação de equipamentos. Com `--hand-cards`, registra índices internos de cartas e suas palavras completas, não IDs de recurso.
4. A captura só aceita a impressão digital do executável conhecido; alterações no EXE invalidam o mapeamento até serem revalidadas.
5. O modo de investigação nativa proposto para a fase seguinte é uma pequena ponte independente com registro ordenado dos comandos do dispatcher; não recomendamos chamar callbacks diretamente nem instalar hooks sem validar ABI, thread e duração das operações.

A próxima rodada ideal é capturar uma partida real com `--hand-cards`, marcar dois ou três efeitos com Ctrl+Shift+J, conferir `state-changes.jsonl`, salvar o YDR e usar as anotações como contexto para o Codex mapear offsets adicionais.
