# Handoff — preparação P025, sem implementação

Branch `investigate/p025-registry`, origem c93b52a.
Consolidação anterior: **be98145** (baseline revisado c93b52a).
Unidade deste commit: `docs: investigate P025 equipment scope before implementation`;
localizar por `git log --oneline -- docs/cards/p025-preliminary.md`.
P045 permanece em c93b52a. Nenhum push ou integração de outra branch.

## Entrega e evidência

Tabela completa em docs/cards/p025-preliminary.md, organizada em 15 regras de tipo,
seis de atributo, dez outras contribuições, seis caminhos contextuais e quatro
lacunas de contribuição. Registro/índice/recurso/nome conferidos com catálogo;
31 contribuições não significam 31 cartas completas. Exceção de seleção concreta:
Ring of Magnetism exige lado próprio, condição não repetida pelo helper de bônus.
Metalmorph tem class_code21 enquanto os outros 40 têm22.

Três candidatas auditadas individualmente: Legendary Sword T15 +300/+300;
Beast Fangs T11 +300/+300; Sword of Dark Destruction A2 +400/−200.
Reutilizados os estudos Legendary Sword/equipment-cleanup e dumps versionados.
Tabela de fontes e localizadores exatos no relatório; proveniência desses blobs
pode ser recuperada em c93b52a. SHA local do EXE conferido e igual ao esperado:
db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Não foram feitas novas decompilações nem executados validadores históricos que
reescrevem relatórios. Revisão complementar somente leitura por GPT-6.1 Sol Medium.

Proposta para revisão: Legendary Sword como regressão e **duas novas cartas**,
Beast Fangs e Sword of Dark Destruction. Reutilizar EquipmentDelta (já contém
os três deltas), estrutura Equipment e limpeza comum do demo; separar ATK/DEF,
elegibilidade e revalidação por whitelist explícita. Sem engine genérica,
sem promover outras cartas por callback. Unknown/Unsupported quando o contexto
necessário não estiver resolvido. Atributos estáticos só sob contexto declarado.

## Verificação e reprodução

```bash
python3 -m unittest discover -s tests -p 'test_p025_inventory.py' -q
sha256sum ../passion/joey_pc.exe
git diff --check
git diff c93b52a -- src native_hooks yugilab
```

Teste documental: um PASS, 41 registros únicos, identidades exatas e grupos
15/6/10/6/4. O teste não afirma semântica nem paridade. Build/testes da consolidação
são os registrados em native-registry-consolidation.md: 776 checks .NET,
35 cobertura + cinco P045 + cinco consolidação. Comandos de geração do registry
em docs/coverage/consolidation.md; artefatos em /tmp/p025-consolidated-registry.

Nenhuma alteração em src/native_hooks/yugilab; nenhum teste de hooks ou captura
executado. Arquivos EXE/DAT/DLL/save não modificados. Schema v1 preservado.

## Lacunas e parada

Aguardamos **revisão do escopo pelo usuário antes de qualquer implementação P025**.
Legalidade integral e filtro 0056C510, palavra auxiliar do tipo dinâmico,
supressão/proteção/global, flags e timing, mudanças posteriores de tipo/atributo,
movimento/troca de controle e composição de efeitos exigem contratos/observação
adicionais. Retirada comum estática não fecha todos os eventos.
Os ramos D não autorizam adivinhar contadores; E não significa ausência de efeito.
Não declarar paridade sem entradas e saídas observadas independentemente.

Próximo trabalho, se aprovado: contrato pequeno de elegibilidade/contexto das três
cartas e testes limitados, conforme os quatro passos propostos no relatório.
Não iniciar automaticamente; esta sessão termina na investigação.
