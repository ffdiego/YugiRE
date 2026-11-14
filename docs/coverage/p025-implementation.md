# Registry — baseline da implementação P025 limitada

Fonte versionada: `p025.annotations.json` (overlay v1 autossuficiente).
Baseline revisado de implementação e testes:
**12f560fc1eb270aa0ed28b8cab9982524ea2c5bc**.
Implementação em 3f4f0c5; testes em 12f560f. Use somente o novo overlay para
esse snapshot. Os três overlays históricos continuam intactos e não devem ser
passados junto, pois baselines diferentes são rejeitados.

## Revisão de estados e fontes

As nove identidades do consolidado foram preservadas. Apenas card:983/recurso307
(Beast Fangs) e card:1018/recurso301 (Sword of Dark Destruction) foram adicionadas.
Sparks continua uma vez. Os estados de carta inteira das nove anteriores foram
preservados: Legendary Sword e P045 Partial; Witch/Zombyra Unknown em implementação.

NativeAiAttributes.cs, Archive.cs, DirectDamageRules.cs e DirectDamageChecks.cs
estão byte a byte iguais ao baseline de investigação. O diff de Demo.cs preserva
os caminhos de dano P045 e modifica somente a disponibilidade de equipamentos,
o contrato/adapter, os stats e a limpeza comum. Program.cs acrescenta EquipmentChecks;
os checks antigos de Legendary Sword/P045 continuam presentes e foram executados.
Novas referências SourceInspection no baseline atestam cada estado de implementação;
fonte antiga por si só não foi usada para transferir estado ao novo snapshot.

`p025-history` pina o consolidado anterior. Os escopos antigos
`ai.stats.sword-delta` e `rules.equip.sword` foram substituídos, preservando sua
proveniência, por:

| Mecânica | Escopo | Implementação / validação |
| --- | --- | --- |
| ai.stats.p025-three-delta | EquipmentDelta(300/301/307), stats dinâmicos fornecidos; soma ATK/DEF e cauda sob contexto fechado | ImplementedInScope / SyntheticOnly |
| rules.equip.p025-three-target | Whitelist, identidade/posição/face, tipo/atributo e guarda genérica fornecidos; não implementa 0056C510 | ImplementedInScope / SyntheticOnly |
| rules.equip.p025-three-demo | Ativação da mão, revalidação, vínculo e retirada comum no sandbox | Partial / SyntheticOnly |

O ciclo depende da elegibilidade e da aritmética. A elegibilidade não calcula
deltas; a aritmética pressupõe contexto resolvido e não autoriza ativação. Essa
separação evita contar o mesmo bônus novamente e não generaliza os 41 callbacks.
O ciclo nativo completo permanece aberto mesmo com os dois escalares implementados.

Snapshot gerado: **1114 cartas, 11 anotadas, nove mecânicas, 27 vínculos**.
Identificação: 11 Identified / 1103 Unknown. Implementação e compreensão:
nove Partial / 1105 Unknown. Validação integral: **1114 Unknown**. Inventários
Open em todas as cartas. Das 41 P025, somente 300/301/307 têm anotações; as outras
38 permanecem Unknown, apesar de o helper aritmético preexistente conhecer
contribuições de algumas delas.

Os testes históricos do consolidado agora verificam os blobs no seu próprio
baseline, sem exigir que o código atual continue congelado em c93b52a. O novo
teste P025 confere as fontes atuais contra o baseline12f560f, a preservação dos
estados históricos, identidades, ausência de promoções e proveniência pinada.
Não houve alteração de schema, gerador ou regras de validação.

## Reprodução

```bash
dotnet build YugiRE.sln --no-restore -m:1
dotnet build src/Joey.Coverage --no-restore
dotnet run --project src/tests/Joey.Tests --no-build -- ../passion/data.dat ../passion/deck.ydc
dotnet run --project src/tests/Joey.Tests --no-build
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_p025_registry.py' -q
python3 scripts/generate_card_coverage.py --dat ../passion/data.dat --exe ../passion/joey_pc.exe --output /tmp/p025-implemented-registry --baseline-commit 12f560fc1eb270aa0ed28b8cab9982524ea2c5bc --annotations docs/coverage/p025.annotations.json --coverage-report --investigation-priorities
```

Os quatro testes P025 incluem geração real duas vezes, comparando todos os bytes,
preservação do inventário bruto, membros P025 não promovidos, baseline incorreto,
evidência adulterada e validação indevida. A integração exige os arquivos locais;
sem eles é SKIP explícito. Nenhum teste executa o jogo. Artefatos gerados ficam
em /tmp; o overlay revisado é a fonte reproduzível versionada.
