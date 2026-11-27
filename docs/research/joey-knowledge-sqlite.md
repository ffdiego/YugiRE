# Joey knowledge SQLite: como consultar antes de reabrir o Ghidra

## Complemento cumulativo v10 — saída do banner de resultado (27/11/2026)

O ZIP privado `scratch/coord-evidence/joey-knowledge-visual-cumulative-2026-11-24-v10.zip`
contém o SQLite cumulativo e as auditorias da v9 e da v10. A coleta
36/V16 acrescenta o Draw `00444490`, o predicado `00483C40`,
a vtable `005DBC48` e o bloco da referência ao manifesto em
`005EBD80`. O fingerprint do executável é o mesmo da v9.

| Validação | Valor |
| --- | --- |
| SHA-256 do SQLite | `5e2c4b7e9f5ae66a6fc6278c9d0519d4fedc63b0f1ff4c8788c7d7a4fa5a7fd9` |
| SHA-256 do ZIP v10 | `96ac30f2561aa3f4bd97e546752eaff1c1d1732d696496c345ebd6e947205e00` |
| SHA-256 do bundle 36 | `b74125599c0162c1e6349a20d0eb66d39225f165c3eb5a5d0a6dac334b33f4f9` |
| `PRAGMA integrity_check` | ok |
| `PRAGMA foreign_key_check` | sem violações |

Todas as linhas da v9 foram preservadas. A v10 tem 37 bundles,
241 relatórios, 373 registros de funções (302 VAs distintas),
229.750 instruções indexadas e 43 blocos de memória.
[Relato V16](../plans/VISUALS.md#v16).

## Complemento cumulativo v9 — Draw dos bustos (27/11/2026)

O ZIP privado `scratch/coord-evidence/joey-knowledge-visual-cumulative-2026-11-24-v9.zip`
contém o SQLite cumulativo, a auditoria atual e a auditoria v8. A
coleta 35/V16/V31 acrescenta oito rotinas dos objetos de busto, todas
com assembly. As vtables e o Update compartilhado `00456730` já
constavam da base. O fingerprint do executável é o mesmo da v8.

| Validação | Valor |
| --- | --- |
| SHA-256 do SQLite | `b7829cff3c01da8c07fff7df3536975411b1a53f3f906e25ee90db5c2d7658bb` |
| SHA-256 do ZIP v9 | `6fd6f866da43a79e8603d1ca2ed605f5225b5b8dc9f3c9b776eeebc5951508a1` |
| SHA-256 do bundle 35 | `aa3d72274ce74517de52e6ee4946003372ccb9135230994021a03f96b1475106` |
| `PRAGMA integrity_check` | ok |
| `PRAGMA foreign_key_check` | sem violações |

A comparação por conteúdo confirmou a preservação de todas as linhas
da v8. A v9 tem 36 bundles, 237 relatórios, 371 registros de funções
(300 VAs distintas), 229.350 instruções indexadas e 41 blocos de memória.
[Relato V31](../plans/VISUALS.md#v31).

## Complemento cumulativo v8 — conversão x87 (27/11/2026)

O ZIP privado `scratch/coord-evidence/joey-knowledge-visual-cumulative-2026-11-24-v8.zip`
contém o SQLite cumulativo, a auditoria atual e a auditoria v7. A coleta
32/V04 acrescenta a função `005C3E40`, sua janela de assembly e os dois
blocos de memória de `005DA708` e `005DA728`. O fingerprint do EXE é
o mesmo da v7. [Contrato V04](native-attack-life-routing-2026-11-24.md#v044--constantes-e-conversão-x87-2711).

| Validação | Valor |
| --- | --- |
| SHA-256 do SQLite | `3f294ed7a69d9b1cf4da72709facfb531a93572a4ae9a428aeb72fe1f107f120` |
| SHA-256 do ZIP v8 | `4bc13b9730e5ff7a0f0a16ce9983906a1a09b0cf1598c49e0c30dede35526ba1` |
| `PRAGMA integrity_check` | ok |
| `PRAGMA foreign_key_check` | sem violações |

A comparação por conteúdo confirmou a preservação das linhas das 27
tabelas da v7. As contagens afetadas são 35 bundles, 228 relatórios,
363 registros de funções (292 VAs distintas), 221.158 instruções
indexadas e 41 blocos de memória.

## Complemento cumulativo v7 — centros do campo (25/11/2026)

Depois de identificar a base v6, use a cópia privada
`scratch/coord-evidence/joey-knowledge-visual-cumulative-2026-11-24-v7.zip`
para consultar a coleta 33 de V02/V05/V06. O ZIP inclui o banco
`joey-knowledge-visual-cumulative.sqlite`, a auditoria v7 em
`visual-evidence-import-audit.json` e a auditoria anterior em
`visual-evidence-import-audit-v6.json`.

| Validação | Valor |
| --- | --- |
| SHA-256 do SQLite | `a210880f69a0997492d81dd00e405e1e306d6cc4201f4948d7b8855e4236d379` |
| SHA-256 do ZIP v7 | `e54e84aea26d6152ea4b55068a120cc85c089151290d9f67c303d6715b42b192` |
| SHA-256 do EXE | `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de` |
| `PRAGMA integrity_check` | ok |
| `PRAGMA foreign_key_check` | sem violações |

A comparação por conteúdo confirmou a preservação das linhas v6 nas
27 tabelas. A v7 acrescenta o bundle 33, dois relatórios, uma coleta da
função `0045C280` e os blocos de 80 bytes em `005DB510` e `005DB560`.
As contagens que mudaram são 34 bundles, 226 relatórios, 362 registros de
funções (291 VAs distintas) e 39 blocos de memória. As demais contagens da
v6 continuam válidas. [Contrato dos centros do campo](native-field-attributes-2026-11-22.md#v052--coleta-33-recebida-e-consolidada-25112026).

## Snapshot de referência v6 — 25/11/2026

Para as três trilhas, usar o arquivo privado
`joey-knowledge-visual-cumulative-2026-11-24-v6.zip`,
preparado localmente em `scratch/coord-evidence/` para uso privado. Em sessões ChatGPT, procurar por esse nome exato
nas fontes do projeto ou na Biblioteca antes de solicitar novo upload.
No ambiente local, extrair sob `scratch/`. O ZIP contém
`joey-knowledge-visual-cumulative.sqlite` e
`visual-evidence-import-audit.json` (v6) e `visual-evidence-import-audit-v5.json` (histórico).

| Validação | Valor |
| --- | --- |
| SHA-256 do SQLite | `7b65daf2d88898f7627ce3e27a181a5aec732604fa3da3795e357affdb184c52` |
| SHA-256 do ZIP v6 | `c853501cca4c8a918d3808de825a062f412e6cb1cb42f472dc449e027590e272` |
| SHA-256 do EXE | `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de` |
| PRAGMA integrity_check | ok |
| PRAGMA foreign_key_check | sem violações |

Contagens conferidas no SQLite **v6**, iguais à auditoria:

| Tabela | Registros |
| --- | ---: |
| functions | 6.518 |
| visual_transient_decompilations | 120 |
| visual_evidence_bundles | 33 |
| visual_evidence_reports | 224 |
| visual_evidence_functions | 361 (291 VAs distintas) |
| visual_evidence_instructions | 221.094 |
| visual_evidence_memory | 37 |
| visual_runtime_cosine | 512 |
| visual_dat_manifests | 80 |
| visual_dat_images | 1.078 |
| visual_voice_metadata | 131 |
| visual_runtime_reports | 9 |
| visual_runtime_sources | 3 |

A v6 preserva as linhas da v5 e acrescenta o bundle 32 com os dois alvos de Final Destiny, `00589970` e `005813D0`. A v5 preserva todas as linhas da v4, que havia preservado as da v3 e acrescentado os bundles 31/A06 (7/7 funções) e 17/V23–V24 (4/4 funções). A v5 acrescenta 9 bundles, 67 registros de funções e 13 blocos de memória. A v3 preservou as contagens estáticas da v2. A amostra de cosseno em `00A52EE8` coincide em todas as 512 entradas com a geração x16.16 por truncamento em direção a zero. Os metadados gráficos registram três erros. O smoke de voz registra um WAV válido, um recurso acima do limite da aplicação e 129 rejeições por cabeçalho RIFF truncado. Os três exemplos selecionados não chegaram à reprodução.

O arquivo anterior `joey-knowledge-visual-cumulative.zip` tem
19 bundles e 179 registros de funções, embora sua auditoria
informe 21/281. A v2 foi reempacotada a partir do SQLite completo
e validada após extração; substitui aquele ZIP como referência.
O snapshot `joey-knowledge-visual-decoded.zip` é histórico.

Inclui as coletas Ghidra 01–13, 16–21, 23–27 e 29–32, e dois bundles visuais históricos. Os números 16 e 17 têm roteiros distintos por trilha. O roteiro 25 foi coletado em duas partes. Os roteiros 14, 15, 22 e 28 são execuções especiais. Consulte o [histórico de coletas](../../scripts/completed/README.md) e as [regressões](../../scripts/regression/README.md).
A aquisição não implica implementação ou Verified.

## Consultas à evidência cumulativa

Abra o arquivo correto em modo somente leitura:

```python
from pathlib import Path
import sqlite3

path = Path("scratch/joey-knowledge-visual-cumulative.sqlite").resolve()
db = sqlite3.connect(path.as_uri() + "?mode=ro", uri=True)
va = "004142C0"

# Todas as versões novas de decompilação, com proveniência
rows = db.execute("""
    SELECT address, provenance, bundle_sha256, source_entry, code
    FROM visual_evidence_functions WHERE address = ?
""", (va,)).fetchall()

# Assembly indexado: selecionar um bundle/janela específico
asm = db.execute("""
    SELECT bundle_sha256, source_entry, ordinal, address, instruction
    FROM visual_evidence_instructions WHERE window_address = ?
    ORDER BY bundle_sha256, source_entry, ordinal
""", (va,)).fetchall()

# Tabelas de memória estática (por endereço inicial do chunk)
memory = db.execute("""
    SELECT bundle_sha256, source_entry, address, hex(bytes)
    FROM visual_evidence_memory WHERE address = ?
""", ("005DB5B0",)).fetchall()

# Decompilação transitória anterior, sem registro em functions
legacy = db.execute("""
    SELECT address, code, bundle_sha256, source_entry
    FROM visual_transient_decompilations WHERE address = ?
""", (va,)).fetchall()
```

Consultar também `visual_evidence_reports` para contexto integral,
Xrefs e relatórios históricos sem janelas de assembly indexáveis.
Não confundir contagem de instruções coletadas (inclui sobreposição
de janelas) com número de instruções distintas do executável.

## Origem e escopo histórico

Arquivo fornecido pelo usuário em 21/11/2026, sob o nome
`joey-knowledge(1).zip`, contendo `joey-knowledge.sqlite`
(**30.392.320 bytes descompactado**).

O `metadata.executable_sha256` vale
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`,
igual ao `joey_pc.exe` analisado no projeto; `index_imported=yes`.

Tabela | Linhas
--- | ---:
`functions` | 6.518
`decompilations` | 6.518 (status `ok`)
`refs` | 159.821
`pointers` | 848
`symbols` | 55.638
`strings` | 703
`card_callbacks` | 443
`card_callback_slots` | 1.023
`card_identities` | 442
`metadata` | 2

**Não versionar o SQLite, EXE ou DAT no GitHub.** O arquivo de dados
permanece como anexo do usuário: este documento contém apenas um
inventário e exemplos de consultas, não substitui o banco completo.

## Consultas sugeridas

Abrir somente leitura usando Python padrão (sem pacotes de terceiros):

```python
import sqlite3
db = sqlite3.connect("file:scratch/joey-knowledge-visual-cumulative.sqlite?mode=ro", uri=True)

# Código decompilado de uma função
row = db.execute("""
    SELECT f.address, f.name, f.signature, d.status, d.code
    FROM functions f JOIN decompilations d USING(address)
    WHERE f.address = ?
""", ("0044EFD0",)).fetchone()

# Funções chamadas por ela
calls = db.execute("""
    SELECT DISTINCT target_function
    FROM refs
    WHERE source_function = ? AND target_function IS NOT NULL
    ORDER BY target_function
""", ("0044EFD0",)).fetchall()

# Referências a um entrypoint
callers = db.execute("""
    SELECT source_function, from_address, reference_type
    FROM refs WHERE to_address = ?
""", ("0044EFD0",)).fetchall()

# Slots e identidades de callbacks conhecidos
callbacks = db.execute("""
    SELECT c.resource_id, c.profile, s.slot, s.callback_address
    FROM card_callbacks c
    JOIN card_callback_slots s ON s.resource_id = c.resource_id
    WHERE c.resource_id = ?
""", (15,)).fetchall()
```

Para encontrar uma VA que caia **dentro** de uma função, consulte
`functions.address` e `functions.size`, calculando o intervalo:
o banco contém funções catalogadas, não necessariamente cada endereço
de entrada virtualmente despachado por vtables.

## Exemplo concreto: Set

- `0044EFD0` está presente com decompilação e inicialização das oito
  origens das partículas.
- `00402690` classifica o tipo de carta que controla a família
  de sprites.
- `0055C140` contém o gerador Mersenne Twister compartilhado.
- `0044F370` e `0044F390` **não** estão presentes como funções
  independentes; seus pseudocódigos estão no ZIP de exportação visual
  de 21/11/2026.
- O SQLite não contém os pixels do DAT nem captura de frames do jogo.
  Não chamar inferências decompiladas de fidelidade validada.

## Atualização cumulativa compartilhada

A cada coleta, `export_ghidra_visual_bundle.py` cria um diretório exclusivo
em `scratch/`. O manifesto só contabiliza funções solicitadas com decompilação
concluída em relatórios cujo fingerprint foi aceito. Saídas reprovadas ficam
no diretório local para diagnóstico. O ZIP inclui os motivos de rejeição em
`manifest.json` e preserva a evidência aceita de uma coleta parcial.

ACTIONS, CARDS e VISUALS consultam a mesma versão privada mais recente do banco, preferencialmente em modo somente leitura. A investigação estática deve enriquecer o acervo quando possível, evitando exportações repetidas do mesmo endereço. O importador cumulativo `scripts/import_visual_evidence.py` foi desenvolvido na PR #74 (consultar a branch se ainda não integrada); os importadores históricos descritos em [enriquecimento visual](sqlite-visual-vtable-enrichment.md) cobrem formatos específicos. Registrar a versão real de cada fonte; novos bundles entram em uma cópia cumulativa validada.

- Manter o snapshot original intacto e gerar uma nova cópia cumulativa, por um único escritor. Validar `metadata.executable_sha256`, identidade das funções/endereços, proveniência (bundle, ferramenta, data e modo de extração), contagens/deltas e `PRAGMA integrity_check`. Não substituir evidência válida por registros ausentes ou ambíguos; registrar conflitos explicitamente.
- Priorizar funções/decompilações/disassembly, referências, ponteiros, vtables e tabelas recuperadas. Dados de execução podem ser indexados quando houver schema apropriado; imagens, traces brutos e dumps grandes continuam privados, com metadados/referências, sem forçar sua conversão para SQL.
- Identificar snapshot de entrada e saída, relatório de importação e lacunas; compartilhar o SQLite fora do Git (arquivo privado em `scratch/` ou anexo). Versionar apenas scripts, testes sintéticos, documentação e resumos permitidos. As contagens acima são o inventário histórico do arquivo de 21/11, não o estado de todas as cópias enriquecidas.
- Se a informação exigir Windows/Fedora indisponível, registrar uma tarefa direcionada em [scripts/pending](../../scripts/pending/README.md), com ID da trilha. Coleta e importação são estados distintos; não elevar cartas ou visuais a Verified somente por ampliar o banco.

## Política de trabalho

1. Identificar a função/efeito pelo inventário nativo e consultar
   **primeiro o snapshot cumulativo atual**: função, decompilação, chamadas,
   referências, ponteiros, strings e callbacks.
2. Cruzar com as evidências Ghidra transitórias já anexadas, quando
   a VA não estiver registrada em `functions`.
3. Investigar o `data.dat` local e observar a janela do executável
   somente para lacunas concretas de composição/recursos/timing.
4. Solicitar nova consulta Ghidra apenas se a base e os lotes
   existentes não resolverem a questão.

A meta é reproduzir **comportamento observável** e visual nativo,
não recriar classes internas, ABI nem execução do binário.
