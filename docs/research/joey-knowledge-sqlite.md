# Joey knowledge SQLite: como consultar antes de reabrir o Ghidra

## Origem e escopo

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
db = sqlite3.connect("file:joey-knowledge.sqlite?mode=ro", uri=True)

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

ACTIONS, CARDS e VISUALS consultam a mesma versão privada mais recente do banco, preferencialmente em modo somente leitura. A investigação estática deve enriquecer o acervo quando possível, evitando exportações repetidas do mesmo endereço. Os importadores existentes, descritos em [enriquecimento visual](sqlite-visual-vtable-enrichment.md), cobrem formatos específicos; um importador genérico de bundles ainda deve ser implementado/testado quando necessário.

- Manter o snapshot original intacto e gerar uma nova cópia cumulativa, por um único escritor. Validar `metadata.executable_sha256`, identidade das funções/endereços, proveniência (bundle, ferramenta, data e modo de extração), contagens/deltas e `PRAGMA integrity_check`. Não substituir evidência válida por registros ausentes ou ambíguos; registrar conflitos explicitamente.
- Priorizar funções/decompilações/disassembly, referências, ponteiros, vtables e tabelas recuperadas. Dados de execução podem ser indexados quando houver schema apropriado; imagens, traces brutos e dumps grandes continuam privados, com metadados/referências, sem forçar sua conversão para SQL.
- Identificar snapshot de entrada e saída, relatório de importação e lacunas; compartilhar o SQLite fora do Git (arquivo privado em `scratch/` ou anexo). Versionar apenas scripts, testes sintéticos, documentação e resumos permitidos. As contagens acima são o inventário histórico do arquivo de 21/11, não o estado de todas as cópias enriquecidas.
- Se a informação exigir Windows/Fedora indisponível, registrar uma tarefa direcionada em [scripts/pending](../../scripts/pending/README.md), com ID da trilha. Coleta e importação são estados distintos; não elevar cartas ou visuais a Verified somente por ampliar o banco.

## Política de trabalho

1. Identificar a função/efeito pelo inventário nativo e consultar
   **primeiro o SQLite global**: função, decompilação, chamadas,
   referências, ponteiros, strings e callbacks.
2. Cruzar com as evidências Ghidra transitórias já anexadas, quando
   a VA não estiver registrada em `functions`.
3. Investigar o `data.dat` local e observar a janela do executável
   somente para lacunas concretas de composição/recursos/timing.
4. Solicitar nova consulta Ghidra apenas se a base e os lotes
   existentes não resolverem a questão.

A meta é reproduzir **comportamento observável** e visual nativo,
não recriar classes internas, ABI nem execução do binário.
