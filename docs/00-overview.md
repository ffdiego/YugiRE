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
