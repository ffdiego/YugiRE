# Evidência nativa e roteiros de extração

> SQLite compartilhado e ciclo de vida de coletas. Carregue sob demanda.

## Fonte privada compartilhada de engenharia reversa

**Consultar primeiro o snapshot cumulativo privado mais recente compatível, identificado no [guia de consultas](../../docs/research/joey-knowledge-sqlite.md), que é a fonte de verdade para nome, versão, hash e auditoria.**
Em conversas ChatGPT, procurar o arquivo nas fontes do projeto YugiRE
ou na Biblioteca privada do mantenedor pelo nome exato; em máquinas locais,
usar a cópia em `scratch/`. Não pedir reenvio sem tentar localizar a fonte.
Validar o hash do SQLite e o fingerprint do EXE conforme o
[guia de consultas](../../docs/research/joey-knowledge-sqlite.md).
Consultar o guia para localizar cópias locais ou versões anteriores validadas se a mais recente não estiver disponível; declarar a versão efetivamente consultada.
Antes de considerar uma VA ausente, consultar `decompilations`,
`visual_transient_decompilations`, `visual_evidence_functions`,
`visual_evidence_instructions` e, quando pertinente,
`visual_evidence_memory` e relatórios brutos. Não repetir coletas
já presentes, inclusive quando o endereço não existe em `functions`.
Ao publicar uma nova versão privada, atualizar o guia com nome, hash,
auditoria, contagens e lacunas; manter as versões anteriores somente como histórico.
Nunca versionar o SQLite ou os ZIPs no GitHub.

## Extrações nativas que dependem de outra máquina

A pasta [`scripts/pending/`](../../scripts/pending/README.md) contém **tarefas de extração ainda não encerradas**, para Ghidra/Windows e outras evidências indisponíveis neste ambiente; resultados já validados devem sair da fila conforme o ciclo de vida abaixo. Ler seu `README.md` antes de executar, atualizar o estado após obter um ZIP e nunca marcar um item como concluído só porque o script foi escrito. Não executar automaticamente as tarefas pendentes durante builds, CI ou testes. Esta política vale para **ACTIONS, CARDS, VISUALS e AI**. Se qualquer frente depender do Windows (Frida/executável/DAT) ou Fedora (Ghidra/projeto local) indisponível, adicionar um script direcionado, indicar ID da trilha, ambiente, pré-requisitos, evidência esperada e saída em `scratch/`, registrar no README e avançar em uma tarefa independente.

**Ciclo de vida obrigatório dos roteiros (todas as trilhas):**

- `scripts/pending/`: fila ativa de coletas ainda não executadas, incompletas, falhas ou aguardando validação/importação. Manter o README como lista acionável, com estado, próxima ação e ambiente. Uma execução com falha não encerra a pendência: registrar a causa e não pedir nova execução idêntica sem necessidade.
- `scripts/completed/`: após receber e validar o resultado, registrar as conclusões e incorporar a evidência ao SQLite cumulativo quando compatível (ou referenciar o artefato privado quando não for), mover o roteiro de pending para completed no mesmo marco de consolidação. Manter `scripts/completed/README.md` com ID, nome, data, resultado, referência à evidência e snapshot/importação, quando aplicável. Não confundir coleta concluída com implementação ou comportamento Verified.
- `scripts/regression/`: mover testes/smokes reutilizáveis que já cumpriram o objetivo de coleta e devem continuar disponíveis para regressões opcionais. Não misturá-los com a fila de coletas inéditas.
- Preservar nomes, numeração e histórico; não excluir roteiros reprodutíveis. Atualizar referências ativas, comandos e READMEs, revisar caminhos relativos e testes afetados. Se uma nova lacuna exigir coleta, criar roteiro direcionado com novo número; não recolocar uma coleta concluída na fila sem justificativa.
- Antes de solicitar execução ao mantenedor, reconciliar a fila com os bundles recebidos, o SQLite mais recente e PRs ativos. Não repetir coletas já disponíveis. Em trabalho paralelo, o coordenador serializa a reorganização de caminhos para evitar conflitos.

**SQLite primeiro, enriquecimento preferencial:** antes de pedir outra coleta, consultar a versão mais recente do banco, bundles e docs existentes (efeitos triviais de cartas continuam dispensando RE exaustiva). Quando possível, importar novas evidências estáticas em uma cópia cumulativa do SQLite compartilhado, validando fingerprint, origem, integridade e preservação dos dados anteriores; usar/estender importadores testados conforme o formato. Dados dinâmicos/frames que não se ajustem ao banco ficam em artefatos privados com referências. Uma sessão escritora integra o snapshot, demais agentes consultam somente leitura. Registrar a versão e auditoria; não versionar o banco nem dumps. Ver [guia SQLite](../../docs/research/joey-knowledge-sqlite.md).

## Capturas Windows

Para capturas simples, preferir `python scripts/duelos.py` (ou `--battle-attributes`). O modo `--trace-state` documentado limita a 3 duelos/1 worker; conferir o CLI atual antes de executar. Para lotes maiores, usar `--no-state` sem fingir equivalência de cobertura. Preservar preflight, gerenciamento do PID próprio e integridade das capturas.
