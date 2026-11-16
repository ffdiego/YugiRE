# Handoff — implementação limitada a três equipamentos P025

Branch `feature/p025-three-equipment`, criada a partir de 6f183e3.
Branch `investigate/p025-registry` preservada em 6f183e3; P045 em c93b52a.
Nenhum push realizado.

Commits:

- **3f4f0c5** — implementação de contrato/adapter, vínculo/stats/limpeza e seleção UI.
- **12f560f** — testes C# e verificação histórica do consolidado; baseline revisado
  de implementação e testes: 12f560fc1eb270aa0ed28b8cab9982524ea2c5bc.
- Registry e este handoff: commit `docs: register bounded P025 implementation and validation`,
  localizável com `git log --oneline -- docs/coverage/p025.annotations.json`.

## Escopo entregue

Somente recursos300/307/301 equipáveis: Legendary Sword T15 +300/+300,
Beast Fangs T11 +300/+300 e Sword of Dark Destruction A2 +400/−200.
Os outros 38 P025 e variantes+2000 são rejeitados. EquipmentRules separa
identidade/coordenadas/face/tipo/atributo/guarda genérica, desconhecidos Unsupported.
Não implementa a guarda genérica nem descoberta de propriedades dinâmicas.
NativeAiAttributes.EquipmentDelta e ApplyResolvedTail reutilizados sem alteração.

Demo soma ATK/DEF separados antes do clamp, revalida alvo no momento de Equip,
não consome carta/zona em falha e mantém dono/fonte/alvo distintos. Retirada isolada
RemoveEquipment e destruição do monstro apagam referências; reutilização de slots
não herda bônus. Referência de objeto é proteção do sandbox, não chave nativa.
UI seleciona os três IDs. Operação de equipamento setado com alvo não foi ampliada.

Políticas de demo versus fatos estáticos: docs/cards/p025-implementation.md.
O contexto do demo normal declara propriedades estáticas e ausência de bloqueios;
hidratação parcial mantém equipamento desconhecido por padrão e exige opt-in
explícito para testes com esse contexto fechado. Não se afirma paridade nativa.

Registry autossuficiente p025.annotations.json mantém as nove cartas e adiciona
somente duas: 11 identidades anotadas, nove mecânicas/27 vínculos; 1114 cartas
com inventários Open e validação integral Unknown. Aritmética/elegibilidade
ImplementedInScope/SyntheticOnly; ciclo Partial/SyntheticOnly. Escopos antigos
da espada ampliados sem duplicar bônus, histórico pinado. P045 continua com os
mesmos estados; sua implementação foi reatestada no novo baseline.

## Testes efetivamente executados

```bash
sha256sum ../passion/joey_pc.exe
dotnet build YugiRE.sln --no-restore -m:1
dotnet build src/Joey.Coverage --no-restore
dotnet run --project src/tests/Joey.Tests --no-build -- ../passion/data.dat ../passion/deck.ydc
dotnet run --project src/tests/Joey.Tests --no-build
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_*coverage*.py' -q
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_p045_registry.py' -q
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_consolidated_registry.py' -q
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_p025_registry.py' -q
python3 -m unittest discover -s tests -p 'test_p025_inventory.py' -q
python3 -m unittest discover -s tests -p 'test_card_callback_catalog.py' -q
git diff --check
```

SHA esperado db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Builds sem avisos/erros. **1035 checks .NET com DAT/deck; 837 offline**.
Incluem 259 novos checks P025; regressão antiga da espada, 94 checks P045 e
327680 combinações sintéticas de flags/alvo preservados. Python: **58 testes PASS**
(35 cobertura + cinco P045 + cinco consolidado + quatro novo registry + um inventário
P025 + oito callbacks), sem SKIP com artefatos configurados.

A primeira tentativa do novo registry foi rejeitada corretamente: referências
P045 de implementação ainda eram do baseline anterior. Após revisão dos arquivos
inalterados, acrescentada evidência SourceInspection do novo baseline; os quatro
testes e geração passaram. Não houve relaxamento do validador/schema.

Logs: /tmp/p025-build.log, p025-coverage-build.log, p025-tests.log,
p025-tests-offline.log; registry em /tmp/p025-implemented-registry.
Comando exato de geração: docs/coverage/p025-implementation.md.
Determinismo byte a byte e independência dos artefatos brutos verificados pela
integração real. Não foram executados GUI interativo, hooks, capturas ou testes
legados sem mudanças nesses componentes. Não foram modificados EXE/DAT/save/DLL.

## Ganho e parada

Um → três equipamentos no demo; dois novos ciclos limitados e nenhuma tabela
aritmética duplicada. DEF assimétrica e clamp após soma; 9 → 11 cartas anotadas
sem promoção integral. Novos testes são sintéticos, não um oráculo nativo.

Trabalho autorizado concluído; parar após esses três IDs. Próxima investigação
somente com novo escopo autorizado. Dependências para alegar equivalência nativa:
observação independente de contexto/guarda/propriedades, alvo/chave/origem,
comando/vínculo/ATK/DEF e retirada correlacionados. Timing, remapeamento,
supressão/proteção/globais, troca de controle e demais movimentos seguem fora
do contrato. Não preparar ou executar captura automaticamente.
