# Sogen e cinco perdas de 200 LP — captura original, rodada 3

**Fonte primária do operador:** arquivo ZIP `multi-spell-night-20261117-003950.zip`, captura `round-03-capture/duels-20261117-044242-207378-pid5688.jsonl`, terceiro duelo. Os hashes e os índices do jogo devem ser rechecados contra o JSONL original antes de promover qualquer hipótese a validação. Os sumários `round-03-patterns/patterns.json` e `round-03-states/state-report.json` acompanham a mesma captura.

## Hipótese

Sogen (índice interno **490**, recurso **332**, perfil P036) é magia de campo com bônus de +200 ATK/+200 DEF para **Warrior / Beast-Warrior** na regra textual conhecida. O laboratório classificou cinco perdas de 200 LP, com lados alternados, enquanto Sogen (índice 490, ocorrência 4) permanecia no slot físico 10 do lado 0 desde seq 1257.

A associação temporal **não** demonstra nem que Sogen causou dano nem que seu bônus tenha alterado o resultado dos combates.

## Auditoria dos cinco casos no JSONL original

Em **todos** os casos, há a sequência **0x35 → 0x30 → 0x31 → 0x3C → 0x43 → 0x7C**, com `0x31` contendo 200 (`0xC8`), `0x43` valor 200 e amostra de LP −200 na entrada do comando `0x7C`. O `0x3C` e a alteração de slot de monstro são consistentes com o pipeline de combate documentado em `docs/22-battle-core.md`. Os comandos 0x30 e 0x31 **não devem ser lidos simplesmente como índices de monstro**; só conhecemos seus parâmetros brutos e o produtor/consumidor documentados.

| Seq LP | Lado que perde 200 | Seq 0x35/0x30/0x31/0x3C/0x43/0x7C | Saída observada de campo antes de aplicar LP |
|---:|---:|---|---|
| 1576 | 1 | 1571,1572,1573,1574,1575,1576 | lado1 slot2, monstro índice135, ocorrência8 |
| 2079 | 0 | 2074,2075,2076,2077,2078,2079 | lado0 slot1, monstro índice1038, ocorrência29 |
| 2261 | 1 | 2256,2257,2258,2259,2260,2261 | lado1 slot1, monstro índice982, ocorrência48 |
| 2459 | 0 | 2454,2455,2456,2457,2458,2459 | lado0 slot3, monstro índice939, ocorrência52 |
| 2562 | 1 | 2557,2558,2559,2560,2561,2562 | lado1 slot1, monstro índice169, ocorrência56 |

Nas amostras de entrada de 0x35/0x31 próximas a esses combates, Sogen está no lado0 slot10 com `flagsWord=0`. **Nenhum monstro presente nos slots 0–4 dos dois lados, nessas amostras, apresenta a referência de associação `0x0A00`**; existe ocasionalmente uma referência diferente `0x0600` em outro monstro. Sogen anteriormente gerou uma associação `referenceWord=0x0A00, metadataWord=8` no monstro índice886 do lado1 na seq1260, mas isso não mostra aplicação nos cinco combates.

**Leitura conservadora:** os cinco -200 são resultados observados na rotina de batalha, não danos diretos de Sogen. É plausível que sejam diferenças normais de atributos de batalha ou que haja modificadores não vistos. A ausência de associação observada não comprova ausência de bônus no cálculo nativo.

## Próxima medição — atributos efetivos sem novo interceptor

Análise estática existente em `docs/22-battle-core.md` aponta que **0x005B7C50** calcula dois blocos de 16 bytes em **0x00A57844/0x00A57854** antes do comando **0x31**. Novo modo opt-in `--trace-battle-attributes` lê esses blocos **na entrada do produtor de comandos**:

- `selectorWord` bruto em 0x00A57840 (não interpretar previamente os bits de lado/papel);
- por bloco: `descriptorWord` em +0; `cardIndexWord` em +2; `calculatedAttack` int32 em +4; `calculatedDefense` int32 em +8; `damageWord` uint32 em +12;
- `battleAttributesObservation.schema=joey-command-entry-battle-attributes-v1`, somente em `opcodeMasked=0x0031`;
- inclui erro por evento sem derrubar o processo. Nenhum novo `Interceptor.attach`; leitura aprovada apenas para o mesmo executável PE32/SHA256 que já passa no preflight.

**A primeira execução no Joey real ainda precisa validar se esses blocos estão preenchidos *neste instante* e se correspondem às identidades do confronto.** Se retornarem blocos vazios, inválidos ou defasados, não inferir alterações de atributos e reexaminar o ponto de amostragem. Não alterar hook do produtor normal em busca de dados.

## Piloto Windows (branch)

Rodar no Windows, com o jogo original fechado:

```powershell
git switch feature/native-battle-attributes-sogen-v1
git pull --ff-only

python -m unittest discover -s yugilab/tests -p test_cli.py -v
node .\native_hooks\tests\test_native_battle_attributes.cjs
node .\native_hooks\tests\test_loader.cjs

.\native_hooks\.venv-frida\Scripts\python.exe -m yugilab `
  --exe ..\passion\joey_pc.exe `
  --duels 3 --workers 1 --trace-state --trace-battle-attributes `
  --deck-0 .\scratch\multi-spell-decks-v1\spell-round-03-A.json `
  --deck-1 .\scratch\multi-spell-decks-v1\spell-round-03-B.json `
  --seed 12345 --speed-value 600 `
  --output-dir .\scratch\native-battle-sogen-r03-v1
```

Validação mínima: `complete=true`, `overwritten=0`, `stateTrace.readErrors=0`; confirmar `battleAttributesTrace.samples>0`, `battleAttributesTrace.readErrors=0`, e se `cardIndexWord` bate com identidades observadas. Anexar JSONL e rodar comparação de blocos com Sogen em campo, associações `0x0A00` e atributos-base extraídos em `scratch/coverage-current/attributes.json`.

**Controle futuro:** criar deck sem Sogen, mantendo 40 cartas e até 3 cópias por índice, de preferência substituindo as duas cópias por monstros normais revisados. Executar com seed e configuração documentadas. Os duelos podem divergir após a alteração do deck; não tratar como uma comparação contrafactual perfeita. O único controle forte é comparar mesmos monstros em estados equiparáveis e/ou validar a escrita do modificador.
