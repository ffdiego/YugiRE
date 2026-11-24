# C07 — Contrato de chegada ao cemitério e causa de descarte

## Evidência estática consultada

SQLite privado joey-knowledge-visual-decoded.sqlite, read-only.
PRAGMA quick_check = ok. SHA-256 do executável:
db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Tabelas card_callbacks, card_identities, card_callback_slots, decompilations,
refs. Não há descrições DAT disponíveis no banco.

| Resource ID | Nome | Perfil | Callback primário | Decompilado? |
| --- | --- | --- | --- | --- |
| 462 | Electric Snake | P066 | 005859C0 | não |
| 533 | Minar | P044 | 00584E60 | sim |
| 478 | Cockroach Knight | P068 | 00585AE0 | não |
| 1042 | Sword of Deep-Seated | P030 | 00584610 | não |
| 1242 | Beast of Gilfer | P219 | 0058E580 | não |

00575AF0 emite 0x8E; depois consulta os IDs 0x1CE/0x215
(Electric Snake/Minar), 0x1DE/0x412/0x4DA (Cockroach/Sword/Gilfer).
O primeiro grupo exige (param_3 & 1) != 0 e igualdade de param_1
com o bit 12 da palavra da ocorrência. O segundo não exige esses dois
testes neste produtor. O pedido é encaminhado a 005BA490.
Se a consulta 005699C0(0x453) for positiva, usa 00575A80 e retorna
antes desses pedidos. A região 11 é mão, 14 cemitério, 15 banimento,
conforme [contrato de descarte existente](tribute-discard-cost.md).

00575A10 chama 005759A0 explicitamente com último argumento zero
(caminho de custo). 005759A0 normaliza seu quarto argumento e chama
00575AF0. Já 00575970 aparece nos ramos 005A8E10, 005A90E0 e
005A92D0, mas seu quarto argumento na decompilação de 005759A0 está
ausente. Não inferir a ABI, o valor da flag ou sua equivalência a
"efeito adversário" sem disassembly. 00578860 também usa o quarto
argumento 1 num caminho de remoção de carta de classe 0x16; isso é
evidência de reutilização, não de causa universal.

Cockroach/Sword/Gilfer também aparecem nos produtores 00578BB0
(opcode 0x62), 00578D00 (0x8E com regiões distintas), 00579330
(0x79), 00576F40 e 00577CF0 (0x7C). A seleção do pedido depende
do caminho e de gates. A presença nesses produtores não prova
todas as resoluções nem a ordem entre múltiplas ocorrências.
00584E60 confirma o caso 0x215, dano 1000 via 005784E0,
com gate do bit 2 do registro de efeito. Não confundir o registro de
efeito com a palavra da ocorrência.

## Contrato de domínio provisório, sem alterar DemoDuel

GraveyardArrivalRules produz somente uma intenção após o movimento
completado. A entrada preserva dono, lado da mão de origem quando
aplicável, controlador do efeito responsável (não necessariamente o
jogador ativo), causa, chegada efetiva ao cemitério do dono e estado
de término do duelo. O adapter deve conservar identidade da ocorrência.

- Electric Snake / Minar: descarte da própria mão por efeito adversário
  propõe respectivamente duas compras próprias ou 1000 de dano ao rival.
  Custo, limite de mão, descarte próprio, origem em mão alheia ou destino
  redirecionado não ativam o recorte.
- Cockroach Knight / Sword of Deep-Seated: chegada ao cemitério propõe
  devolver a mesma ocorrência ao topo do deck do dono; inclui custo,
  tributo, material, destruição e mill no modelo de domínio. A resolução
  deve revalidar a ocorrência; não buscar outra cópia do mesmo ID.
- Gilfer fica fora do modelo: requer seleção e vínculo de equipamento.
- GameOver bloqueia novas intenções por política sandbox, não prova nativa.

As descrições históricas de Electric Snake e Minar eram mais amplas
("sent directly") que erratas posteriores de descarte. Assim, envio
direto da mão por efeito adversário produz ReviewDirectHandSend
(não executável), em vez de concluir sem evidência que ativa ou não.
Conferir DAT original e os argumentos do produtor; ver roteiro 12.

Referências de domínio, não evidência de Joey:
[histórico de erratas](https://ygorganization.com/itsnotjustdsf),
[Electric Snake](https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4469&ope=2&request_locale=en),
[Minar](https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4540&ope=2),
[Cockroach Knight](https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4485&ope=2),
[Sword](https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4873&ope=2&request_locale=en).

## Integração após ACTIONS

A02 (#67) ainda está em branch separada. Não introduzir uma segunda
fila, nem ligar este classificador a mutações legadas. O executor deve
emitir uma chegada observável uma única vez por movimento aplicado, não
por admissão; descartar comandos rejeitados ou obsoletos sem gatilho.
Transportar a causa semântica no contexto da ação, sem confundir
ActionOrigin.Effect (produtor) com o controlador do efeito original.
Resolver cada compra por DrawCommand, o dano por DamageCommand e o
retorno ao topo por novo comando de movimentação que preserve a ocorrência.
A ordem de múltiplas cartas deve ser validada antes da integração.
Não executar ReviewDirectHandSend. Para o custo, preservar o bloqueio
até terminar a resolução e respeitar a janela de gatilhos.

Testes C# registrados: dois lados, tipos de causa, gates de origem/destino,
identidade como contrato do adapter, ausência de efeito para Gilfer e
caso ambíguo. SDK .NET não está disponível no ambiente desta investigação;
não foram executados. Sem mudança no manifest/badge e sem promoção Done.
C07 parcial. Roteiro 12 é coleta direcionada opcional para fechar as
lacunas de callbacks/flags, não bloqueia outras famílias triviais.
