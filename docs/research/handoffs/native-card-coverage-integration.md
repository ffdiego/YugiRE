# Handoff — preparação de integração Card Coverage / P045 / P025 / piloto

Branch `feature/sparks-sword-differential-pilot`, partida727081c.
Unidade documental: commit `docs: summarize reviewed card coverage integration`;
localizar com `git log --oneline -- docs/coverage/integration-report.md`.
PR direcionado à main, sem merge automático; URL fornecida na entrega.

`origin/main=e0ae044` atualizada por fetch, ancestral da branch (behind0).
Main local265f132 também ancestral, mas atrasada; comparação feita contra a
referência remota. Os 25 commits até727081c não incluem commits de merge ou
integração de outra branch experimental. Revisão do diff: 63 arquivos anteriores
mais as quatro atualizações documentais desta unidade. A captura recente de
Sparks não foi lida, versionada nem usada para promover cobertura.

README principal agora expõe1.114/11/9/8/0, diferencia cartas/mecânicas e testes
sintéticos/paridade nativa. src/README descreve cinco P045 e três P025 com
quantidades/restrições corretas, ativação set versus mão, contexto fechado,
limpeza e limites. Infinite Cards legado não aumenta a contagem dos pilotos.
[Relatório versionado](../../coverage/integration-report.md) reúne resultado,
baseline, geração completa, prioridades estruturais, comandos e pendências.

Registry mantém baseline12f560fc1eb270aa0ed28b8cab9982524ea2c5bc e schema v1;
overlays e evidências anteriores intactos. Nove mecânicas/27 vínculos não são
nove/27 cartas completas. Todas as1.114 cartas Open/validação integral Unknown.
Verificação real com DAT/EXE:11 anotadas, nove Partial, zero integralmente validadas.
Geração repetida nos testes e comparação independente de relatórios passaram.

Validação executada (comandos completos no relatório): builds solução/ponte sem
avisos/erros;1051 checks .NET com DAT/deck,853 offline;58 testes Python (35coverage,
14registry, um inventário P025, oito callbacks), sem SKIP. Joey.App --check-data e
--check-graphics passaram (793BMPs/1114mapeamentos). Opção --standard-schema não
executada: pacote jsonschema ausente; validação própria passou sem mudar dependências.
Smoke gráfico pendente por falta de display/autenticação; execução normal exit2.
Diagnóstico X11 bloqueado foi encerrado, sem deixar processo do teste ativo.
Não executar Joey original/hooks/capturas nesta etapa.

Auditoria de arquivos: nenhum EXE/DAT/DLL/save/replay/ZIP/binário ou trace privado
novo no conjunto do PR. JSONs parseáveis; fixtures revisadas são mínimas/sintéticas;
metadados/evidências históricas conservam suas classificações. Logs e relatórios
completos em /tmp/integration-*, fora do Git. Verificações de links locais,
whitespace e staged diff antes do envio. Nenhum arquivo de outra worktree incluído.

Ponto de parada: documentação commitada/enviada e PR aberto para revisão.
Não realizar merge, iniciar outra família ou incorporar captura recente. Próximos
passos do revisor: ler limites/validação do PR e executar smoke no desktop conforme
src/README. Captura nativa de Sparks permanece entrega separada.

Dependência de integração: preservar o histórico dos commits pinados (incluindo
7e5ca13 e 12f560f), pois a proveniência usa git show. Squash/rebase sem manter
refs requer revisão antes de afirmar reprodução em clone novo. Nenhum merge
foi realizado nem configuração do repositório alterada.
