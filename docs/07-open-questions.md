# Prioridades e questões abertas

## Próximas investigações pequenas

1. **Leitor compartilhado de arquivos:** xrefs de `KCEJYUGI`, `*.dat` e APIs de leitura; no máximo algumas funções por rodada. Entrega: layout mínimo de cabeçalho e dois registros comprovados, com endereços e limites.
2. **Amostra de recurso:** validar offsets/tamanhos contra os dois arquivos e ler apenas um payload pequeno. Entrega: distinguir transformação de nomes e compressão de conteúdo; extrator somente após validação.
3. **Deck e IDs:** xrefs de `deck.ydc`, leitura/escrita e contador; comparar com dados de cartas. Entrega: explicar os 100 bytes sem presumir campos desconhecidos.
4. **Startup e subsistemas:** separar CRT, wrappers de bibliotecas e inicialização própria; seguir janela, entrada e áudio para estabelecer fronteiras. Confirmar renderizador por chamadas reais.
5. **Estado do duelo e efeitos:** a partir dos IDs confirmados, localizar consumidores e dispatch; testar as hipóteses de funções/tabelas/interpretador com poucas cartas. Este é o objetivo central após infraestrutura mínima.
6. **IA:** investigar só quando estado e ações estiverem compreendidos.

Cada rodada deve produzir bytes de entrada, endereços/xrefs, pseudocódigo selecionado, conclusão com confiança e um teste que possa refutar a hipótese. Não é necessário decompilar em massa. Análise dinâmica exigirá autorização separada.

## Limites atuais

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
