# Catálogo inicial de funções

## Preparação de descarte e condição de resolução

Endereços VA; nomes descritivos somente na documentação. Ver
[custo de Tribute](cards/tribute-discard-cost.md).

| VA | Papel confirmado no caminho examinado |
|---|---|
| 0x00580D20 | Consulta disponibilidade de uma entrada elegível da mão para Tribute |
| 0x0057D7C0 | Seleciona quantidade de descarte 1/2/5 por ID e agenda tipo 2 |
| 0x005BCE00 | Prepara flags e encaminha solicitação de descarte |
| 0x005A91F0 | Processa a solicitação tipo 2 e contador de escolhas |
| 0x00575AF0 | Emite movimentação por chave da mão, com desvio de Banisher |
| 0x00408EE0 | Consome 0x8E e chama remoção/inserção após localizar a origem |
| 0x00583510 | Ramo específico de Big Shield Gardna que pode impedir destruição |

## Destruição por efeito e listas de destino

Endereços VA; nomes descritivos somente na documentação. Ver
[fluxo e limites](cards/monster-destruction.md).

| VA | Papel confirmado no caminho examinado |
|---|---|
| 0x00576960 | Filtra máscara, prepara dependências e coordena movimentação |
| 0x00576B30 | Emite operação por posição; caso de Man-Eater Bug usa 0x79 |
| 0x00577280 | Pode emitir operações para posições vinculadas |
| 0x00404B40 | Consome 0x79; estado 2 retira origem e insere destino |
| 0x0056C590 | Acrescenta palavra à lista da região 14, excluindo quatro IDs de ficha |
| 0x0056C640 | Acrescenta palavra à lista da região 15, com a mesma exclusão |

## Décima rodada: seleção de callback e criação de vínculo

Nomes sugeridos somente na documentação; nenhum aplicado ao banco.

| VA | Sugestão | Comportamento confirmado |
|---|---|---|
| 0x0059DBE0 | buscar_registro_por_indice_carta | Converte por CARD_ID e busca tabela de passo 24 em 0x005ED0A8 |
| 0x0059DEB0 | chamar_callback_primario_carta | Usa primeiro uint16 do pedido e chama ponteiro em registro+4 |
| 0x005875A0 | callback_parasite_paracide | Registrado para ID 762; um ramo produz associação de tipo 13 no outro bloco |

Evidências, restrições dos ramos e assinatura ainda parcial do pedido em
[criação de Parasite Paracide](13-parasite-creation.md).

Endereços VA, image base `0x00400000`. Nenhum nome ou tipo persistente foi alterado no Ghidra.

| Endereço | Nome original | Nome sugerido na documentação | Evidência/comportamento | Xrefs | Confiança |
|---|---|---|---|---|---|
| 0x005C5E58 | Desconhecido; `entry` automático | startup CRT candidato | Consulta versão, prepara command line/STARTUPINFO, chama 0x00411B50 e caminho de término | Chamada a 0x00411B50 no pseudocódigo | Alta para fluxo; média para classificação CRT |
| 0x00411B50 | Desconhecido | WinMain candidato | Recebe módulo, zero, command line preparada e show mode do startup | Chamado pela entrada 0x005C5E58 | Média; corpo ainda não examinado |
| 0x0040F510 | Desconhecido; FUN_0040f510 automático | carregar configuração Region (provisório) | Prepara nome Region.dat; chama 0x00543170; se retorno zero obtém ponteiro por 0x00542220, desloca primeiro byte 4 bits; chama 0x005BE140 com resultado módulo 6 | String VA 0x005EB640 referenciada em 0x0040F538, 0x0040F548, 0x0040F57D, 0x0040F585 | Alta para fluxo; média para interpretação de leitura |
| 0x004A64B0 | Desconhecido; FUN_004a64b0 automático | inicializador DirectInput candidato | Referencia dinput.dll; corpo não descompilado nesta rodada | String VA 0x005ECA20, xref 0x004A64EF | Média/baixa para papel específico |

Não atribuir a entrada diretamente a WinMain. APIs de leitura e strings de recursos são âncoras, não funções do motor já identificadas. Evidência: [saída Ghidra](evidence/ghidra-recon.txt). Foram exportadas apenas duas funções descompiladas. O Ghidra detectou 6718 funções automaticamente; isso não implica 6718 funções próprias do jogo nem identificação correta de todos os limites. Seu compiler spec `windows` não identifica a versão do compilador.

## Segunda rodada: arquivos KCEJYUGI

Todos os nomes abaixo são automáticos; sugestões não foram aplicadas ao banco. VA do alvo com SHA256 registrado na visão geral.

| VA / nome automático | Sugestão | Comportamento e evidência | Referências | Confiança |
|---|---|---|---|---|
| 0x004A5010 / FUN_004a5010 | construtor de enumerador de recursos | Inicializa campos e vtable; referencia bytes da assinatura durante cópia de string adjacente, não é por si uma validação de arquivo | Chamado em 0x00470758 e 0x004A58F6 | Média |
| 0x004A5540 / FUN_004a5540 | enumerar arquivos e membros de arquivos | FindFirstFileA/FindNextFileA; padrão *.dat; CreateFileA/ReadFile; reconhece assinatura, lê contador e registros 0x10C, decodifica nomes | *.dat VA 0x005EC9F4 em 0x004A59B9; assinatura VA 0x005EC9E8 em 0x004A5A84/0x004A5A8F | Alta |
| 0x005428D0 / FUN_005428d0 | abrir recurso com fallback para arquivo | Tenta arquivo direto; fallback para arquivo agrupado, cache de índices, busca binária pelo nome e seek do payload | Assinatura em 0x00542C67/0x00542C77; chamadas de decoder em 0x00542E1A; campos +0x100/+0x104/+0x108 no pseudocódigo | Alta para layout; média para detalhes da política de fallback |
| 0x004A5EA0 / FUN_004a5ea0 | trocar nibbles do nome | Loop in-place até NUL, byte = byte >> 4 OR byte << 4 | Chamado em 0x004A55F6 e 0x00542E1A | Alta |
| 0x00543170 / FUN_00543170 | carregar recurso com caminhos alternativos | Chama 0x00543290, tenta caminhos adicionais quando falha | Chamado por Region em 0x0040F5A8 | Média |
| 0x00543290 / FUN_00543290 | carregar e transformar conteúdo de recurso | Abre via 0x005428D0; aloca tamanho em memória; lê direto se tamanhos iguais, ou transforma via 0x0054A000; caminho YPK1 | Chamado em 0x00543193/0x005431EE | Alta para comparação de tamanhos; média para ABI e detalhes devido à recuperação de pilha |
| 0x0054A000 / FUN_0054a000 | descomprimir LZSS | Dicionário zerado 4096 bytes, escrita inicial 0xFEE; flags LSB; referência de posição 12 bits/comprimento 3–18; encerra por tamanho esperado | Callers em 0x00543404 (FUN_00543290) e 0x004974D7 (FUN_00497360) | Alta, confirmada na terceira rodada com uma amostra real |

Evidências: [reader](evidence/archive-reader-ghidra.txt), [helpers](evidence/archive-reader-helpers.txt), [load](evidence/archive-resource-load.txt). Nenhuma função de efeito ou regra de duelo identificada por esta rodada.

Terceira rodada: [descompilação de 0x0054A000](evidence/lzss-ghidra.txt) e [experimento de 23 para 34 bytes](evidence/lzss-small-validation.json). Assinatura automática `__thiscall` ainda provisória; não se executou a função original. Não foi aplicado nome sugerido ao banco.

## Quarta rodada: propriedades de cartas

Nomes originais desconhecidos; todos os nomes sugeridos abaixo existem apenas na documentação.

| VA | Nome sugerido | Comportamento | Xref/evidência | Confiança |
|---|---|---|---|---|
| 0x00402340 | construir recurso de dados carregado | Inicializa objeto, prepara caminho e chama 0x00543170 | Chamado pelos acessores ID/IntID/Prop | Alta para fluxo, média para papel |
| 0x004022E0 | consultar CARD_ID | Carga única; acesso uint16 para índice <1116; fora retorna 0xFFFF | String 0x005EB4F4 em 0x004022EE | Alta para acesso; significado do ID pendente |
| 0x00402460 | consultar CARD_IntID | Carga única; índice %2000; soma 1 se entrada >=2000, aceita <4000 | String 0x005EB50C em 0x0040246E | Alta para cálculo; domínio pendente |
| 0x004025F0 | consultar palavra CARD_Prop | Carga única; uint32[(índice & 0x7FF)] | String 0x005EB540 em 0x004025FE | Alta |
| 0x004025D0 | consultar classe de propriedade | Bits 20–24 | Chamado pelos getters de stats/categorias | Alta para máscara; média para classe |
| 0x00402650 | consultar campo alto de propriedade | Bits 29–31; exceções classe21→9/classe22→8 | Pseudocódigo de consumidores | Alta para fluxo; atributo é hipótese |
| 0x004026C0 | consultar campo de quatro bits | Bits 25–28; classe21–23→0/classe24→10 | Pseudocódigo de consumidores | Alta para fluxo; nível é hipótese |
| 0x00402700 | consultar subtipo de classes especiais | Bits17–19 somente para classe21/22; senão zero | Pseudocódigo de consumidores | Alta para fluxo |
| 0x00402740 | consultar categoria derivada | Bits18–19 com exceções por classe e CARD_ID 0x776–0x778 | Chama 0x004022E0 | Alta para fluxo; categoria pendente |
| 0x004027B0 | consultar ATK base | Bits9–17 ×10, com exceções por classe | Call 0x0041B48E; string ATK/DEF | Alta |
| 0x00402800 | consultar DEF base | Bits0–8 ×10, com exceções por classe | Call 0x0041B47F; string ATK/DEF | Alta |
| 0x00418B10 | preencher dados numéricos de objeto | Guarda índice em +0x10, ATK em +0x4C e DEF em +0x50 | Calls 0x00418B29 e 0x00418B37 | Alta para offsets; objeto desconhecido |
| 0x0041B3E0 | preparar texto de detalhes da carta | Formata ATK/DEF e outros trechos para apresentação | Formato VA 0x005EB914, call 0x0041B4A2 | Alta para ATK/DEF; média para papel geral |

Evidências: [loader](evidence/card-resource-loader.txt), [getters](evidence/card-property-consumers.txt), [interface](evidence/card-property-ui.txt), [ordem dos argumentos](evidence/card-property-arguments.txt). O retorno fora do domínio de CARD_IntID tem artefatos de registrador no pseudocódigo; não tratar sua parte alta como contrato sem revisar instruções.

## Quinta rodada: direção das conversões de IDs

- **0x004022E0**, sugestão `indice_para_id_recurso`: CARD_ID uint16, F(i). Instruções 0x00402315/0x0040231A aceitam i<=1115; 0x00402332 lê o elemento. O arquivo físico só vai até 1114. Confiança alta nos fatos, causa da diferença aberta.
- **0x00402460**, sugestão `id_recurso_para_indice`: CARD_IntID uint16, R(d)=tabela[d%2000]+(d>=2000), aceita d<4000. AX=0 para d>=4000 confirmado em 0x0040249C. Não assumir EAX inteiro significativo. Confiança alta, validada por 1114 roundtrips das tabelas reais.
- **0x00570BE0**, nome sugerido apenas `buscar_entrada_por_id_0x2FA` (provisório): converte constante 0x2FA (762), percorre registros de dois bytes em região global selecionada por argumento&1 e compara um uint16 com o índice convertido; há condições adicionais não interpretadas. Chamada de R em **0x00570C34**. Confiança alta no fluxo, baixa no papel funcional.
- **0x00570C70**, sugestão `buscar_entrada_por_id_0x443` (provisório): estrutura semelhante, converte 0x443 (1091) em **0x00570CB0**, compara uint16 e um byte associado. Confiança alta no fluxo, baixa na semântica.

Os dois últimos são apenas exemplos de consumidores da conversão; na quinta rodada ainda não havia associação a nomes. [Instruções e pseudocódigo](evidence/card-id-instructions.txt), [roundtrips](evidence/card-id-validation.json). Nomes sugeridos não foram persistidos no banco.

## Sexta rodada: nomes e deck

| VA | Nome sugerido | Comportamento/evidência | Confiança |
|---|---|---|---|
| 0x00402290 | consultar nome por índice | Buffer global 0x005F25A8 + (índice & 0xFFFF)*64; utilizado na interface em 0x0041B62A | Alta |
| 0x004022B0 | consultar texto por offset de índice | Usa buffer de offsets e segundo buffer; descrição é interpretação pelo contexto, payload não examinado aqui | Alta para cálculo, média para papel |
| 0x00401F20 | carregar recursos de texto localizados (provisório) | Trecho sem função reconhecida no projeto inicial; prologue prepara objetos globais e loop usa tabela 0x005EB444; prefixo CARD_Name é primeira entrada; chamada virtual de carga em 0x00402220 | Alta para ligações observadas; média para função inteira |
| 0x0044AA00 | carregar deck e verificar contadores | Chama 0x0044A920(0); verifica 40–80 e limites 15 dos demais contadores | Alta para fluxo |
| 0x0044A920 | coordenar leitura/escrita do deck | Abre via 0x0055DA10 e escolhe 0x0044A550 ou 0x0044A690 | Alta para fluxo |
| 0x0044A550 | ler cabeçalho e listas do deck | Consome oito bytes, valida opcionalmente via 0x0045CD20 e chama 0x00449EF0 | Alta |
| 0x00449EF0 | serializar três listas de IDs | Leitura converte cada uint16 por CARD_IntID; escrita converte índices por CARD_ID; capacidades80/15/15 | Alta |
| 0x0045CD20 | comparar campos do cabeçalho | Compara primeiro byte e DWORD+4 com objeto chamador; significado desconhecido | Alta para comparação |

Evidências: [refs](evidence/deck-name-entrypoints.txt), [caminho](evidence/deck-reader-name-context.txt), [leitura](evidence/deck-payload-reader.txt), [codec](evidence/deck-id-codec.txt), [prologue](evidence/card-name-loader-prologue.txt). Foi feita desassemblagem transitória em 0x00401F20, sem criar nome de função nem persistir alterações; o log confirmou descarte no modo readOnly. A tentativa anterior de inspecionar 0x00401F40 não exportou instruções porque a região ainda não estava definida; o relatório do prologue resolve essa lacuna.

Os IDs constantes de 0x00570BE0/0x00570C70 agora estão associados a **Parasite Paracide** (762/índice197) e **Graverobber** (1091/índice703) pela tabela de nomes local. A associação não identifica a responsabilidade exata das funções.
