# Prioridades e questões abertas

## Checkpoint de retomada — 2026-10-30

A investigação estática retomou depois da entrega do protótipo. Estado atual:

- [Rituais](cards/ritual-bindings.md): critério de materiais, restrições,
  pagamento, modo 5 de colocação e continuação do callback seguidos.
- [Soul Exchange](cards/soul-exchange.md) e [limpeza](19-temporary-state-cleanup.md):
  associação tipo 3, ator autorizado, bit de combate e produtor 0x02 → 0x03.
- [Agendamento](20-turn-scheduling.md): prioridade da fila/contextos sobre
  callbacks e índices de sequência distintos dos bits de fase.
- [Compra](21-native-card-draw.md): mão inicial de cinco, transferência por
  0x61, falta de cartas e família Pot/Jar/Skelengel.
- [Batalha](22-battle-core.md): núcleo ATK/DEF, empate zero, cópia por 0x3C,
  dano por 0x43 e destino por 0x7C; exceções específicas ainda parciais.
- [Encerramento](23-terminal-conditions.md): Exodia central fora da tabela de
  callbacks, Destiny Board e relação com falta de cartas/LP.

Validação desta rodada: 25 testes Python, 55 verificações C#, compilação e
sete validadores de bytes/cadeias passaram. Hashes do EXE e data.dat conferidos.
Não houve execução do original nem validação visual do protótipo. Só a regra
de empate zero foi corrigida na aplicação, por evidência estática e regressão;
os demais limites de demonstração continuam em src/README.md.

O usuário pretende testar as telas em ambiente gráfico no Windows antes dos
próximos ajustes visuais. Até lá, manter investigação estática em incrementos,
sem expandir catálogo em massa. Próximas trilhas pequenas possíveis: modo 1
em 0x005ABD00 (confirmar regras de invocação/nível antes de nomear o modo) ou
uma resposta de batalha específica em 0x005A2990. Não reabrir dúvidas já
resolvidas apenas porque constam nos checkpoints históricos abaixo.

## Plano inicial (histórico)

1. **Leitor compartilhado de arquivos:** xrefs de `KCEJYUGI`, `*.dat` e APIs de leitura; no máximo algumas funções por rodada. Entrega: layout mínimo de cabeçalho e dois registros comprovados, com endereços e limites.
2. **Amostra de recurso:** validar offsets/tamanhos contra os dois arquivos e ler apenas um payload pequeno. Entrega: distinguir transformação de nomes e compressão de conteúdo; extrator somente após validação.
3. **Deck e IDs:** xrefs de `deck.ydc`, leitura/escrita e contador; comparar com dados de cartas. Entrega: explicar os 100 bytes sem presumir campos desconhecidos.
4. **Startup e subsistemas:** separar CRT, wrappers de bibliotecas e inicialização própria; seguir janela, entrada e áudio para estabelecer fronteiras. Confirmar renderizador por chamadas reais.
5. **Estado do duelo e efeitos:** a partir dos IDs confirmados, localizar consumidores e dispatch; testar as hipóteses de funções/tabelas/interpretador com poucas cartas. Este é o objetivo central após infraestrutura mínima.
6. **IA:** investigar só quando estado e ações estiverem compreendidos.

Cada rodada deve produzir bytes de entrada, endereços/xrefs, pseudocódigo selecionado, conclusão com confiança e um teste que possa refutar a hipótese. Não é necessário decompilar em massa. Análise dinâmica exigirá autorização separada.

## Checkpoints anteriores (histórico)

**Tabela de Rituais concluída:** [mapeamento](cards/ritual-bindings.md) cobre
os 16 pares P269 e cruza requisitos com níveis base. Próxima trilha estática:
0x00599140/0x00599570 para seleção dos materiais, depois pagamento e colocação.
Nenhum jogo foi executado. Timing permanece para o Windows.

**Limpeza comum de equipamentos seguida:** [continuação](cards/equipment-cleanup.md)
liga retirada da origem, remoção de referências, compactação e dependências
do monstro. Pendentes: outros movimentos, invalidar equipamento por mudança de
tipo/orientação e ordem de efeitos simultâneos. Ritual é a próxima amostra
independente sugerida pelo catálogo; execução permanece futura no Windows.

**Equipamento seguido até o cálculo:** [Legendary Sword](cards/legendary-sword.md)
liga seleção de tipo dinâmico, revalidação, associação no monstro e contribuição
de 300/300. A retirada comum e a limpeza foram seguidas na continuação acima.
Ritual permanece como próximo mecanismo independente sugerido
pelo catálogo. Timing e valores exibidos ficam para confirmação futura no Windows.

**Catálogo concluído:** [tabela inteira](17-card-callback-catalog.md) exportada
em CSV/JSON e agrupada por principal e pelos cinco slots. Contagem de registros
não é cobertura de regras. Priorizar representantes das combinações: Legendary
Sword (P025), Black Luster Ritual (P269), Giant Rat (P164). Restam um ID sem
CARD_ID correspondente e três registros com nomes vazios, mantidos sem hipótese
de identidade. Este inventário passa a orientar a escolha das próximas cartas.

**Diretriz atual:** manter a investigação estática; dúvidas de timing e
comportamento facilmente conferíveis por execução ficam para futura etapa
no Windows. Não instalar Wine nem configurar depuração nesta etapa.

**Royal Command identificada e seguida:** [estudo](cards/royal-command.md)
liga ID 0x5FA, validação, marcação de outro pedido e desvio da resolução FLIP.
0xB1 também define estado por posição; a recomputação usa flags globais.
Pendentes: janelas de resposta e interações completas. Próxima trilha estática
útil: 0x0056B440, condição compartilhada por contadores e ativação.

**FLIP — produtor condicional identificado:**
[estudo](cards/man-eater-flip-activation.md) liga classificação de IDs,
0x00574F70, comando 0x80, escrita de face e criação de pedido. Corrige o
alcance da atribuição anterior de 0x00597E90 à ativação de Parasite.
Pendentes: chamadores por ação de interface/batalha e timing. A supressão por
ID 0x5FA foi seguida acima. Para validação dinâmica inicial, um duelo qualquer já permite
conferir LP/mão; não é necessário montar uma interação rara.

**Custo de Tribute seguido:** [estudo](cards/tribute-discard-cost.md) liga
pré-condição, descritor tipo 2, seleção, comando 0x8E e compactação da mão.
O callback também parametriza custos de Magic Jammer, Final Destiny e
Darkness Approaches. 0x00583510 foi identificado como ramo de Big Shield
Gardna. Pendentes: execução assistida da ordem de pagamento/negação e ramos
especiais de descarte. O produtor condicional de FLIP foi seguido acima.

**Destruição seguida até o estado:** [continuação](cards/monster-destruction.md)
confirma o consumidor de 0x79, retirada da origem, regiões 14/15, desvio de
Banisher e exclusão das fichas das listas de destino. Preparação e emissão
de operações vinculadas identificadas, sem matriz completa de interações.
O custo/condição extra de Tribute foi seguido no estudo acima. Para execução
assistida, conferir Banisher
marcada na própria operação, fichas e carta sob controle do outro lado.

**Seleção de alvo concluída estaticamente:**
[Man-Eater Bug](cards/man-eater-bug-targeting.md) liga filtro +8, preparação
+20, entrada de 16 bits e revalidação de ocorrência na posição.
O caminho de 0x00576960 e os callbacks adicionais de Tribute foram seguidos
nas continuações acima.
Testes com mudança de campo ficam para execução assistida pelo usuário.

**Comparação posterior:** [Sparks/Ookazi](cards/sparks-ookazi.md) confirma
callbacks compartilhados com seleção por ID e constantes 200/800. As incertezas
de execução de Parasite foram separadas em [experimentos](cards/parasite-dynamic-checks.md).
O contraste com uma carta de alvo de campo foi realizado no estudo acima.

**Atualização de conclusão:** [Parasite Paracide — caso consolidado](cards/parasite-paracide.md)
fecha a análise funcional estática da carta. O consumidor de 0x43, o transporte
inicial, o embaralhamento, a colocação, o tipo dinâmico e o callback de limpeza
foram seguidos. As pendências listadas nas rodadas abaixo são históricas.
Permanecem fora desse fechamento: execução dinâmica, matriz de interações,
reimplementação equivalente e detalhes completos dos helpers compartilhados.
Próximo trabalho independente: comparar uma carta mais simples ou executar
o [plano de integração do REA](15-rea-integration-todo.md).

Décima segunda rodada concluída: [segundo ramo](16-parasite-continuation.md)
localiza palavra de estado pela chave e pode preparar colocação, em três
estados. Emite metadados tipo 13 na lista de uma posição e comando 0x43 com
1000. Próximo passo: confirmar consumidor de 0x43, destinatário e operação;
não rotular como dano antes disso. Colocação final e evento inicial pendentes.

Décima primeira rodada concluída: [chave do pedido](14-parasite-request-key.md).
0x005BA310 grava o argumento-chave em bits 5–12 de +4 de registros de
20 bytes; transferência e seleção para o callback identificadas. O chamador
0x00578981 fornece a mesma chave de palavra de estado usada na busca, mas
código 0x365F seleciona o ramo 0x0058766E, não o de criação já examinado.
Próximo passo: esse segundo ramo; produtor inicial do primeiro ainda pendente.

Décima rodada concluída: [produtor de tipo 13](13-parasite-creation.md)
ligado ao ID 762 via busca 0x0059DBE0 e callback 0x0059DEB0. Registro 150
da tabela de passo 24 aponta para 0x005875A0, que em um ramo emite
inserção em categoria 11 do bloco oposto com tipo 13 e chave de `pedido+4`.
Próximo passo: seguir o preenchimento desse campo e comprovar sua relação
com a chave extraída da palavra de estado pela busca 0x00570BE0.

Nona rodada concluída: [inserção e chave](12-association-insertion.md).
0x0056AB60 insere índice/metadados e incrementa o contador; opcode 0x86
chega a esse helper. 0x005687D0 usa a mesma chave para procurar palavras
de estado em várias regiões. Os produtores amostrados de categoria 11
usam tipo 3; a criação de tipo 13 para Parasite Paracide não foi provada.
Próximo passo: identificar escritor explícito de `13 | (chave << 8)`.

Oitava rodada concluída: [comando 0x4D](11-command-4d.md) seguido até escrita
de estado. O dispatcher 0x005B9470 usa tabela de normalização: opcode 0x4D
→ índice 0x2C → 0x00571BF0 → 0x0056B1C0. No caminho de Parasite Paracide,
zera índice/metadados da associação, sem compactar o array nem reduzir seu
contador nesse handler. Próximo passo: escritores de entradas com nibble 13
para estabelecer criação e significado do vínculo. O dispatcher identificado
é de comandos; não comprova interpretador geral de efeitos.

Sétima rodada concluída: [associação de Parasite Paracide](10-parasite-association.md).
0x00570BE0 retorna posição+1 de uma entrada de ID 762 com metadados de
nibble 13 e chave derivada da palavra de estado. Três chamadores confirmam
uso como exclusão de contagem e caminho especial de comandos. Próximo passo:
seguir o comando 0x4D emitido por 0x00574960/0x005B9170 ou escritores dos
metadados; significado da lista e do vínculo ainda não confirmado.

Sexta rodada concluída: nomes e domínio dos IDs no deck confirmados. O leitor converte IDs de recurso para índices de propriedades e o escritor faz o inverso. Parser estrutural testado; amostra de 100 bytes consumida exatamente, listas 42/1/0, 43 IDs validados. Próxima prioridade: seguir callers e registros de **0x00570BE0**, associado a Parasite Paracide, para descobrir seu papel real e se participa de validação/efeitos. Manter escopo em poucas funções. Permanecem abertas: cabeçalho YDC, significado da segunda/terceira lista, semântica de +2000, diferença do limite 1115 e ausência de evidência de interpretador de efeitos.

Quinta rodada concluída: CARD_ID e CARD_IntID inspecionados em memória e suas relações validadas em todos os elementos presentes: 1114 roundtrips, 1105 IDs base e nove pares acrescidos de 2000. Domínios documentados sem atribuir números oficiais. Limite de CARD_ID confirmado nas instruções e diferença física continua aberta. Próximo incremento recomendado: correlacionar um ID com nome de carta e com deck.ydc, verificando os respectivos acessores/leitores. Depois escolher um dos consumidores de ID constante (0x00570BE0 ou 0x00570C70) para seguir chamadas e descobrir seu papel. Não investigar indiscriminadamente todos os callers da conversão.

Quarta rodada concluída: CARD_Prop.bin validado (3872 → 4460 bytes, 1115 palavras, sem YPK1). Acessores de ATK/DEF identificados por máscaras e uso da string ATK/DEF, com ordem de argumentos conferida em instruções. Demais campos mantêm rótulos provisórios. Próximo incremento: inspecionar CARD_ID e CARD_IntID, conferir relações entre seus valores e índices válidos e só depois correlacionar com deck.ydc ou nome de carta. Investigar diferença entre o limite 1116 no acessor CARD_ID, 1115 registros no recurso Prop e a máscara 0x7FF. Não assumir que índice é ID oficial nem que a contagem representa todas as cartas jogáveis.

Terceira rodada concluída: 0x0054A000 é compatível com a variante LZSS de dicionário 4096/posição 0xFEE e passou no teste único `j\\deck_c\\line.txt` (23 bytes → 34 bytes, entrada toda consumida). Decoder próprio e testes em `scripts/PocLzss.psm1` e `tests/Test-PocLzss.ps1`. Próximo incremento recomendado: selecionar um recurso de dados de cartas pequeno, verificar eventual YPK1 e relacionar seus campos aos consumidores no executável. Não expandir automaticamente para todos os 205 recursos comprimidos; não interpretar caminhos de animações `duel/effect` como efeitos de regras.

Atualização da segunda rodada: etapas de layout mínimo e validação de dois registros concluídas; todos os registros dos dois índices passaram na validação física. Leitor principal 0x005428D0, decoder de nomes 0x004A5EA0 e carregador 0x00543290 identificados. Ver [formato confirmado](02-file-formats.md) e [referências externas](external-references.md). Próximo incremento: investigar 0x0054A000 e confrontar a hipótese LZSS com um payload pequeno. Não é necessário repetir a análise inicial do PE.

- Toolchain exata e procedência do executável desconhecidas.
- Ausência de proteção não provada.
- Campos do índice DAT e variante LZSS confirmados; abrangência da compressão em outros payloads, YGA e dados internos de cartas ainda pendentes.
- A análise automática pode confundir dados e código; avisos de acesso a endereços inválidos precisam de revisão nas funções selecionadas.
- Autorização atualizada pelo usuário: o banco do Ghidra pode ser modificado livremente; a preservação obrigatória se aplica aos arquivos investigados. Projeto persistente em `../ghidra-projects/Joey.gpr`, excluído do controle de versão. Nomes inferidos continuarão identificados como sugestões na documentação.
