# Reconhecimento inicial

Data: 2026-10-29. Escopo: análise estática inicial; nenhum binário do jogo foi executado. Os originais permanecem em `../../passion/`; scripts e documentação pertencem a `YugiRE/`.

## Convenções e evidências

VA é endereço virtual preferencial, RVA é relativo à image base `0x00400000`; offsets de arquivo são explicitamente indicados. Nomes `FUN_*` são rótulos automáticos do Ghidra, não nomes originais. Nomes sugeridos existem apenas nesta documentação. Confiança alta indica observação direta; média indica interpretação com evidências; baixa indica hipótese a testar.

Hashes SHA256 e tamanhos dos cinco arquivos constam em [initial-inspection.json](evidence/initial-inspection.json). O executável analisado é `DB8FE7B7EA4B12F18A9BB1777B5F5B8EAB254011DB2A52571A0264725505B6DE`, com 2.060.288 bytes. Os demais tamanhos coincidem com os fornecidos pelo usuário.

## Visão inicial

- **Alta:** aplicação nativa PE32 x86 para Windows GUI, sem diretório CLR.
- **Média:** toolchain Microsoft Visual C++ antiga; linker declara 6.0 e há mensagem do runtime Visual C++. A versão exata do compilador não foi comprovada.
- **Alta:** dois arquivos grandes compartilham assinatura `KCEJYUGI`; seus leitores são uma boa entrada para compreender recursos.
- **Média:** há camadas de interface Win32/GDI, entrada DirectInput e áudio/mídia. A divisão entre bibliotecas e código próprio ainda precisa de referências cruzadas.

## Ferramentas

GhidraMCP foi descoberto, mas `get_program_info` retornou `BACKEND_UNAVAILABLE` em `127.0.0.1:8081`. Ghidra 12.2 headless está instalado e inicia com Temurin Java 25.0.5. O alias `python.exe` da Windows Store não executa; os scripts iniciais usam PowerShell 7/.NET.

Headless importou em projeto temporário com `-readOnly`: alterações de análise ficaram transitórias e o programa não foi salvo. Nenhum projeto existente foi aberto para gravação. Preferências/cache foram redirecionados para `scratch/` após a primeira tentativa falhar por acesso negado ao perfil do usuário. A rodada terminou com sucesso, em cerca de 115 segundos de autoanálise, e confirmou descarte da importação. Logs e [saída selecionada](evidence/ghidra-recon.txt) ficam em `evidence/`. Reprodução em [scripts/README.md](../scripts/README.md).

## Atualização de autorização

Após o reconhecimento temporário, o usuário autorizou modificações livres no banco do Ghidra, mantendo a proibição de alterar os arquivos investigados. O fluxo passou a usar o projeto persistente `../ghidra-projects/Joey.gpr` para reutilizar a análise sem depender de MCP. Consultas podem usar `-process joey_pc.exe -noanalysis`; renomeações, tipos e comentários podem ser persistidos quando fundamentados. A primeira rodada temporária descrita acima permanece como histórico.

Criação persistente concluída com `Analysis succeeded` e `Save succeeded for: /joey_pc.exe (Joey:/joey_pc.exe)`; autoanálise em aproximadamente 116 segundos. SHA256 dos cinco originais conferidos novamente, sem alteração.

## Índice

[Compra nativa](21-native-card-draw.md): mão inicial de cinco, comando 0x61,
transferência deck→mão, falta de cartas e callbacks de Pot/Jar/Skelengel.

[Agendamento](20-turn-scheduling.md): prioridade de comandos, contextos e
callbacks; duas sequências por modo de controle, sem confundir índice e fase.

[Limpeza temporária](19-temporary-state-cleanup.md): comando 0x02, flags
compartilhadas com categoria 11 e contador de associações tipo 3/12.
Produtor 0x005A8A50 emite limpeza e depois alternância de turno.

[Soul Exchange](cards/soul-exchange.md): associação tipo 3 no alvo, ator
autorizado a tributar e bit de bloqueio de combate; limpeza ligada à
alternância de turno, sem execução do original.

[Protótipo executável](../src/README.md): catálogo DAT e duelo local com compra,
combate demonstrativo e Sparks/Ookazi.

[Retirada das cópias de batalha](18-battle-snapshot-removal.md): chamador concreto
de Cyber Jar; não constitui reconstrução do combate geral.

[Rituais](cards/ritual-bindings.md): 16 pares magia/monstro e requisitos de
nível em tabela compactada do EXE, conferidos com callbacks e recursos locais.
Continuação: seleção sem materiais redundantes, pagamento e colocação pelo
modo compartilhado 5 → comando 0x70; testes do modelo sem execução nativa.

[Limpeza de equipamentos](cards/equipment-cleanup.md): retirada da origem,
remoção/compactação de referências e operações para os equipamentos quando
o monstro sai pelo caminho de destruição.

[Legendary Sword](cards/legendary-sword.md): seleção Warrior, comando de vínculo
0xB2, associação tipo 1 e recálculo de +300/+300. Limpeza comum seguida acima.

Mapa para escolher próximos casos: [catálogo completo de callbacks](17-card-callback-catalog.md),
443 registros, 269 combinações exatas e 219 callbacks principais distintos.
CSV/JSON incluem cartas, ponteiros e grupos, com lacunas de nomes explícitas.

[Royal Command](cards/royal-command.md): supressão FLIP na validação,
no callback de resposta e antes da resolução; flags de pedido, posição e globais.
Execução fica para uma etapa futura no Windows.

FLIP: [classificação e produtor condicional](cards/man-eater-flip-activation.md)
de Man-Eater Bug/Parasite, comando de face 0x80 e ligação ao pedido.
Inclui limites das verificações estáticas e primeiro teste dinâmico sem carta rara.

Custos: [Tribute to The Doomed](cards/tribute-discard-cost.md), descarte
compartilhado com quantidades 1/2/5, ordem de preparação e caso Big Shield Gardna.

Continuação: [destruição por efeito](cards/monster-destruction.md), comando
0x79 até retirada/inserção, desvio de Banisher e tratamento das quatro fichas.

Seleção de alvo: [Man-Eater Bug](cards/man-eater-bug-targeting.md), com
comparação de registros de Tribute to The Doomed. Entrada de 16 bits e
revalidação de chave na posição confirmadas estaticamente.

Comparação concluída: [Sparks e Ookazi](cards/sparks-ookazi.md), com callbacks
compartilhados e seleção de dano 200/800 por ID. Os testes com jogo aberto de
Parasite ficam em [plano dinâmico](cards/parasite-dynamic-checks.md).

**Caso concluído — análise funcional estática de
[Parasite Paracide](cards/parasite-paracide.md):** ativação, transferência e
embaralhamento, compra, colocação, perda de 1000 LP, alteração dinâmica para
Insect e limpeza ligados ao código. Não houve execução do jogo. Esse documento
consolidado substitui as dúvidas intermediárias das rodadas 10–16 para o caso.

Décima segunda rodada: [continuação de Parasite Paracide](16-parasite-continuation.md).
Estados 0x80/0x7F/0x7E, busca pela chave e emissão de comando 0x43 com valor
1000 identificados; semântica de pontos de vida ainda não comprovada.

Décima primeira rodada: [transporte da chave no pedido](14-parasite-request-key.md).
Bits 5–12 de pedido+4 preservam a chave; código 0x365F seleciona outro ramo
do callback, distinguindo a busca posterior da criação inicial.

Décima rodada: [callback e criação de Parasite Paracide](13-parasite-creation.md).
Registro de ID 762 seleciona 0x005875A0; um ramo produz associação de tipo 13
via comando 0x86. Origem da chave no pedido ainda pendente.

Nona rodada: [inserção genérica e resolução da chave](12-association-insertion.md).
Comando 0x86 e incremento de contador confirmados; criação específica do
tipo 13 de Parasite Paracide permanece aberta.

Oitava rodada: [comando 0x4D e escrita no estado](11-command-4d.md).
Fila de oito bytes e dispatcher com normalização confirmados; o caminho de
Parasite Paracide zera índice e metadados de uma posição associada.

Sétima rodada: [consulta de associação de Parasite Paracide](10-parasite-association.md)
e três chamadores investigados no Fedora. Retorno posição+1, arrays paralelos
e chave de associação confirmados; semântica dos comandos ainda aberta.

Segunda rodada concluída: layout DAT confirmado pelo Ghidra e por validação integral dos índices, sem extração em massa. Foram adicionados parser PowerShell, testes e relatórios. Ver também [referências externas](external-references.md) indicadas pelo usuário.

Terceira rodada concluída: LZSS de 0x0054A000 confirmado estaticamente e em um recurso de 23 bytes comprimidos/34 descomprimidos. Decoder auxiliar e testes adicionados, sem executar o alvo nem alterar originais. Relatório em [lzss-small-validation.json](evidence/lzss-small-validation.json).

Quarta rodada concluída: um recurso de propriedades de cartas validado e seus acessores localizados. ATK e DEF base identificados por fluxo até texto da interface, com checagem das instruções. [Relatório CARD_Prop](evidence/card-property-validation.json). IDs, estado dinâmico e efeitos continuam em investigação.

Quinta rodada concluída: dois recursos de IDs inspecionados, conversões confirmadas por 1114 roundtrips e instruções dos acessores. ID zero e índice zero são distintos; nove pares usam soma de 2000 no ID e +1 no índice. [Relatório de IDs](evidence/card-id-validation.json). Estado dinâmico, nomes, formato do deck e efeitos continuam em investigação.

Sexta rodada concluída: nomes ingleses em registros de 64 bytes e serialização do deck por IDs de recurso confirmados. Deck fornecido: listas 42/1/0, 43 associações verificadas. Os IDs específicos identificados anteriormente correspondem a Parasite Paracide e Graverobber; comportamento ainda não atribuído. [Relatório de nomes/deck](evidence/deck-name-validation.json).

[Executável](01-executable.md), [formatos](02-file-formats.md), [funções](03-functions.md), [duelo](04-duel-engine.md), [efeitos](05-card-effects.md), [IA](06-ai.md), [plano e questões](07-open-questions.md).
