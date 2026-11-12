# Handoff S3 — auditoria independente do primeiro combat-context

Data: 2026-11-12. Branch `work/native-analysis`.
Commit da entrega: `ac5920b` — auditoria, evidência, fixtures diagnósticas e
correção de exportação do atestado. Sem alterações da trilha T.

## Objetivo e resultado

Revisar a captura real contra S1, sem alterar o original nem completar inputs
pelo resultado. Confirmados 64 pares F40 com ambos os buffers; diagnóstico Python
independente do diagnóstico do operador reproduziu **64/64 bytes de saída**,
usando atributos e predicados produzidos pelo nativo e assumindo estabilidade
das flags de jogador amostradas na entrada. Não foi execução do Joey.Core.

A classificação estrita continua **0 completos / 64 incompletos**: não há atestado
do EXE no JSONL nem observação das flags no instante de consumo. Mismatch e
Unsupported do Joey.Lab não foram avaliados; o diagnóstico tem zero divergências.
Cálculos independentes novos de atributos: **0**.

Relatório: [combat-context-pilot-2026-11-12.md](../../evidence/combat-context-pilot-2026-11-12.md).
Resumo auditável com 64 referências de seq/callId:
[.audit.json](../../evidence/combat-context-pilot-2026-11-12.audit.json).
Amostra de três casos, qualificada exclusivamente para leitura/correlação e
regressão escalar condicional:
[.fixture.json](../../evidence/combat-context-pilot-2026-11-12.fixture.json).
Não promover esses casos para `IsResolved` global.

## Evidência e fatos novos

- JSONL privado `scratch/captures/commands-20261112-015214-332209-pid10848.jsonl`.
  SHA256 verificado antes e depois da investigação:
  `424cb09a6d990dbfb0e8795cf97d0260f644f0c2349980937e0b96d14831213f`.
- 1285 eventos contíguos, overwritten=0, 22 pares de seletores, nenhum erro
  registrado. Primeiro par callId 2, seq 29..37; último callId 716, seq 958..970;
  limite seq 971. O arquivo inclui terminal seq 1285, sem garantir captura F40
  após o limite. O operador declarou “não há anomalias”; contexto declarado.
- 57 rivais ocultos fornecem estimativas 1000/1000, cabeçalho zerado e recurso
  efetivo FFFF. Não são seus stats/recurso reais. Sete rivais revelados.
- Os 64 MetalmorphCount e 72 context_count observados são zero. Todas as 45
  field_query retornam zero; por isso as phase_query estão legitimamente ausentes.
- Ausências são justificadas por caminho/curto circuito; termos não avaliados
  permanecem desconhecidos, sem preencher zero. Nenhuma exceção intrínseca F40
  coberta como atacante; índice 307 não aparece. Eventos históricos 304/319
  permanecem sem causa observada. Só 23 combinações dos buffers/postura/seed-word/
  flags de entrada, ignorando slots e consultas; 64 não são 64 regras distintas.
- 004022E0 consome ushort, embora a stack guarde o header inteiro de stats.
  O retorno semântico é low16 de EAX. Essa distinção foi acrescentada a S1.
- O bit de proteção de jogador é consumido em 005B1195/005B11C6 depois da
  amostra de entrada. Inferências a partir de chamadas posteriores não viram
  inputs diretamente observados; outputs coincidentes não provam estabilidade.
- O exportador perdia o atestado armazenado se deck tracing não fosse armado.
  `hostAttestedExeSha256` agora independe disso; sem atestado continua null.
  A correção futura não autentica retroativamente o PID 10848.

EXE local: SHA256 esperado
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`,
conferido sem execução. Não constitui atestado do executável da captura Windows.

## Comandos e testes efetivamente executados

```bash
python3 scripts/audit_combat_context.py \
  scratch/captures/commands-20261112-015214-332209-pid10848.jsonl \
  --expected-sha256 424cb09a6d990dbfb0e8795cf97d0260f644f0c2349980937e0b96d14831213f
python3 -m unittest discover -s scripts/tests
python3 -m unittest discover -s native_hooks/tests
node native_hooks/tests/test_loader.cjs
node native_hooks/tests/test_deck_trace.cjs
node native_hooks/tests/test_demo_rng_seed.cjs
```

Scripts: 14 testes OK (10 da auditoria S3), incluindo original privado presente,
sem skip. Native hooks: 87 testes OK. Os três comandos JS passaram.
Também: sha256sum da captura/EXE, objdump estático no bloco de proteção e
`git diff --check`. O relatório lista as fronteiras e proveniência.
Não executados: Joey/Windows, build/testes .NET, suíte yugilab, cálculo C# do
novo fixture, implementação/teste de callback de Goblin Attack Force.

## Lacunas e próximo experimento

Pedir log de preflight vinculado ao PID/captura e confirmação do encerramento
normal; ausência de anomalias já declarada. Não editar metadata original.
Para fechar flags, revisar um ponto passivo que observe o valor consumido em
005B1195/005B11C6, ou provar preservação de todas as chamadas intermediárias.
Isso exige investigação/revisão antes de novo hook dentro de função; não está
implementado/aprovado nesta unidade. Ver proposta no relatório.

Após revisão, próximo cenário discriminante: Goblin Attack Force revelado como
alvo (307/0540), observar postura natural antes/depois de seu ataque, com
atacante 1800/2000 se ocorrer. Armar perto do cenário, em vez de ampliar o limite
para acumular mais estimativas duplicadas. Usar o exportador corrigido.

Comando Windows, depois da revisão e no processo próprio do operador:

```powershell
.\.venv-frida\Scripts\python.exe .\patch_dual_rules.py --observe-only --pid <PID>
```

Console: `trace on 20000`, `trace ai on combat-context`, duelo curto natural,
`trace off`, `trace status`, `trace dump`, `stop`. Sem overrides ou chamadas
artificiais à engine. Outro avanço específico será observar contagem não zero
ou fase consultada; independência de atributos exige manifesto completo de
modificadores de um caso revelado suportado, não apenas stats nativos.

Parada no gate de evidência Windows. S4 não iniciou: não há observação nova que
discrimine a hipótese da carta 307. Não houve execução automática nem push desta
entrega. Nenhum JSONL bruto, EXE, DAT, save ou replay foi modificado/versionado.
