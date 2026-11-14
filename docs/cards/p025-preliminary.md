# P025 — investigação preliminar e escopo para revisão

Investigação estática no baseline c93b52a. EXE local SHA256 confirmado:
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Sem execução do jogo, hooks ou capturas. Nenhuma carta nova implementada.
Revisão adicional somente de leitura: GPT-6.1 Sol, raciocínio Medium.

## Evidência e fronteira da conclusão

Reutilizamos [Legendary Sword](legendary-sword.md) e
[limpeza de equipamentos](equipment-cleanup.md), com os dumps versionados
abaixo. São decompilação estática e instruções selecionadas, não observação
de execução. As partições são hipóteses de comportamento **parcial** apoiadas
em ramos por ID; não representam classes de equivalência da carta inteira.

| Ref | Evidência versionada em c93b52a | Alcance |
| --- | --- | --- |
| I | [catálogo](../evidence/card-callback-catalog.json), records/profile=P025 | Identidade e cinco ponteiros, 41 registros |
| F | [filtro](../evidence/legendary-sword-link-and-filter.txt), linhas 100–259, FUN_00579CE0 | Monstro ocupado, posição<5, face-up; ramos por ID e tipo/atributo dinâmicos |
| B | [bônus](../evidence/legendary-sword-bonus-table.txt), linhas 20–185, FUN_0056FD40 | Predicados e contribuições de 31 membros |
| S | [estado/stats](../evidence/legendary-sword-state-and-stats.txt), linhas 409–499, FUN_0056DD30 | Guardas comuns e seis caminhos contextuais |
| V | [callbacks](../evidence/legendary-sword-callbacks.txt), FUN_00584500 | Origem ainda válida, alvo com chave, revalidação antes de vincular |
| L | [associações](../evidence/legendary-sword-associations.txt) e [consumidor](../evidence/legendary-sword-link-consumer.txt) | 00574990/B0 → comando B2 → 0040A230 → 0056AB60, tipo1 |
| X | [limpeza](../evidence/equipment-cleanup-entrypoints.txt) e [compactação](../evidence/equipment-cleanup-compaction.txt) | Remoção comum e alinhamento posição/metadados; contador delimita entradas ativas |

P025: primary=00584500, candidate=0057A470, availability/preparation=zero,
target_preparation=00593A20. O agrupamento somente pelo primary tem **44**
registros, portanto não é o mesmo conjunto de 41. Ponteiro zero significa
apenas slot vazio nessa tabela. Metalmorph tem class_code=21; os outros 40,
22 no catálogo. Isso também impede inferir ativação uniforme dos 41.

## Tabela completa, organizada por hipótese

T=tipo dinâmico; A=atributo dinâmico; R=ID de recurso do monstro após o helper
004022E0 nos caminhos examinados; c=(flagsEquip & 0x3C)>>2. Esse contador não
é atestado como número de turnos. Índice, recurso e registro são domínios distintos.
Todos os predicados de contribuição abaixo pressupõem as guardas do chamador:
equipamento ocupado/face-up, máscara 0x20 ausente e bloqueios globais/proteção
satisfeitos. Falha de um predicado de bônus é zero **somente nesse cálculo com
contexto conhecido**, não ausência de efeitos da carta.

A: 15 contribuições +300/+300 por tipo; B: seis +400/−200 por atributo;
C: dez outras contribuições no helper; D: seis cálculos contextuais diretos;
E: quatro lacunas locais. Fontes A/B/C: I+F+B+S; D: I+F+S; E: I, busca
limitada em F/B/S. A ausência de um ramo de bônus não demonstra falta de efeitos.

| Grupo | Registro | Índice interno | Recurso | Carta | Regra parcial / lacuna |
| --- | ---: | ---: | ---: | --- | --- |
| A | 24 | 614 | 300 | Legendary Sword | T=15: +300/+300 |
| A | 26 | 1009 | 302 | Dark Energy | T=3: +300/+300 |
| A | 28 | 1077 | 304 | Laser Cannon Armor | T=10: +300/+300 |
| A | 31 | 983 | 307 | Beast Fangs | T=11: +300/+300 |
| A | 33 | 881 | 309 | Vile Germs | T=13: +300/+300 |
| A | 35 | 231 | 311 | Silver Bow and Arrow | T=17: +300/+300 |
| A | 38 | 652 | 314 | Dragon Treasure | T=1: +300/+300 |
| A | 39 | 608 | 315 | Electro-Whip | T=19: +300/+300 |
| A | 42 | 897 | 318 | Mystical Moon | T=12: +300/+300 |
| A | 45 | 955 | 321 | Violet Crystal | T=2: +300/+300 |
| A | 46 | 745 | 322 | Book of Secret Arts | T=18: +300/+300 |
| A | 48 | 192 | 324 | Machine Conversion Factory | T=7: +300/+300 |
| A | 49 | 524 | 325 | Raise Body Heat | T=9: +300/+300 |
| A | 50 | 785 | 326 | Follow Wind | T=16: +300/+300 |
| A | 51 | 852 | 327 | Power of Kaishin | T=14: +300/+300 |
| B | 25 | 1018 | 301 | Sword of Dark Destruction | A=2: +400/−200 |
| B | 30 | 99 | 306 | Elf's Light | A=1: +400/−200 |
| B | 32 | 708 | 308 | Steel Shell | A=3: +400/−200 |
| B | 47 | 149 | 323 | Invigoration | A=5: +400/−200 |
| B | 177 | 390 | 1012 | Burning Spear | A=4: +400/−200 |
| B | 178 | 637 | 1013 | Gust Fan | A=6: +400/−200 |
| C | 29 | 148 | 305 | Insect Armor with Laser Cannon | T=10: +700/0 |
| C | 40 | 321 | 316 | Cyber Shield | R em {61,62,1249}: +500/0 |
| C | 112 | 340 | 653 | Salamandra | A=4: +700/0 |
| C | 115 | 974 | 657 | Metalmorph | +300/+300 neste helper; restante aberto |
| C | 121 | 386 | 667 | Bright Castle | A=1: +700/0 |
| C | 215 | 417 | 1058 | Ring of Magnetism | −500/−500; seleção exige mesmo lado |
| C | 300 | 1065 | 1182 | Sword of Dragon's Soul | T=15: +700/0 |
| C | 372 | 213 | 1420 | Mask of Brutality | +1000/−1000 |
| C | 374 | 54 | 1422 | Lightning Blade | T=15: +800/0 |
| C | 428 | 317 | 1540 | Cyclon Laser | R=1339: +300/0 |
| D | 114 | 216 | 656 | Megamorph | LP do lado do equipamento: menor => 2×ATK base; maior => HalfNative(base); empate sem substituição neste ramo |
| D | 208 | 314 | 1046 | Germ Infection | T diferente de 7: ATK −300×c |
| D | 217 | 627 | 1060 | Stim-Pack | ATK +700−200×c |
| D | 397 | 543 | 1449 | United We Stand | +800/+800 × 00569D90(ladoEquip,1,0); contador não fechado |
| D | 398 | 910 | 1450 | Mage Power | +500/+500 × 0056A320(ladoEquip,0,0,1); contador não fechado |
| D | 438 | 644 | 1550 | Dragonic Attack | 0056F6F0(alvo,auxEquip)=15: +500/+500 |
| E | 209 | 378 | 1047 | Paralyzing Potion | Contribuição ATK/DEF não localizada; efeitos permanecem abertos |
| E | 338 | 978 | 1313 | Fairy Meteor Crush | Contribuição ATK/DEF não localizada; efeitos permanecem abertos |
| E | 371 | 403 | 1419 | Mask of the Accursed | Contribuição ATK/DEF não localizada; efeitos permanecem abertos |
| E | 436 | 92 | 1548 | Ekibyo Drakmord | Contribuição ATK/DEF não localizada; efeitos permanecem abertos |

A/B e os condicionais C têm os mesmos predicados específicos em seleção e
contribuição nos trechos examinados. Cyber Shield aceita exatamente os três
recursos listados em ambos; não expandir esse conjunto com aliases inferidos.
Ring of Magnetism é contraexemplo útil: seleção exige lado próprio (F:257),
mas contribuição não repete isso. Metalmorph/Mask of Brutality não exigem
outro tipo/atributo no filtro; seus demais comportamentos não foram fechados.
Não usar as contribuições C como implementação integral dessas cartas.

## Três candidatas: fatos, inferências e lacunas

| Candidata | Restrição confirmada | Efeito confirmado no caminho comum | Evidência precisa |
| --- | --- | --- | --- |
| Legendary Sword, índice614/recurso300/registro24 | Tipo dinâmico15; ambos os lados; face-up | +300 ATK/+300 DEF | F ramo300, B ramo300; instruções 0057A148, 0056FD83, 0056FF13 e soma00570066; estudo integrado Legendary Sword |
| Sword of Dark Destruction, índice1018/recurso301/registro25 | **Atributo dinâmico2**, não tipo | +400 ATK/−200 DEF | F:124–125, case0x12D; B:89–91 verifica 0x0200 e B:157–161/LAB_0056FFDA |
| Beast Fangs, índice983/recurso307/registro31 | **Tipo dinâmico11/0x0B**, não atributo | +300 ATK/+300 DEF | F:137–138, case0x133; B:109–111 e B:175–178/LAB_0056FF13 |

Warrior é o nome de tipo15 documentado no estudo de Legendary Sword. Os nomes
DARK para atributo2 e Beast para tipo11 são a interpretação semântica usual;
a evidência operacional aqui são os códigos e seus dois predicados, sem depender
de uma descrição de carta externa. Para as três, o ramo específico não restringe
o alvo ao lado do equipamento. A guarda comum 0057A470 → 0056C510 continua
obrigatória; inclui exceção de recurso0x2E4 ainda não expandida.

O tipo vem de 0056F6F0 e pode receber a palavra auxiliar da origem; atributo,
de 0056F9E0. Não substituir automaticamente pelos campos estáticos do catálogo.
A resolução 00584500 confirma origem, resolve alvo via 00583470 e sua chave,
repete o filtro e só então cria o vínculo. Coordenada persistida no vínculo
não equivale à chave usada no pedido. Falhas marcam pedido+4|8; significado
geral da flag, timing e ramos do seletor continuam abertos.

As três são candidatas a um caminho comum de vínculo tipo1, com base na
infraestrutura estática compartilhada e nos ramos individuais auditados. Isso
não atesta sequência dinâmica. Permanecem desconhecidos: remapeamento posterior
de tipo/atributo, supressão/proteção/global, ordenação de modificadores, troca
de controle, outros movimentos e sincronização de retirada simultânea.
O estudo X fecha retirada comum/compactação e dependências na destruição,
não todos esses eventos. Remover equipamento deve apagar referência para evitar
que novo ocupante do slot herde o bônus; entradas além do contador são inativas.

## Reutilização e proposta incremental — aguarda revisão

Proposta: **Legendary Sword como regressão + Beast Fangs e Sword of Dark
Destruction como duas extensões**. A primeira mantém o ciclo já existente;
a segunda exercita outro tipo; a terceira exige atributo e ATK/DEF assimétricos.
Dark Energy é alternativa posterior do mesmo grupo A, sem necessidade de
incluí-la neste incremento. Nenhum dos outros 38 registros entra por callback.

| Componente | Reutilizar | Limite e mudança futura proposta |
| --- | --- | --- |
| Elegibilidade | Seleção do demo e APIs com Unsupported | Whitelist explícita {300,301,307}; predicados T15/A2/T11; separar ocupação/face/lado e contexto resolvido de regra de contribuição. Dados dinâmicos ausentes => Unsupported/null, sem admitir alvo por default. |
| Vínculo | DemoDuel.Equipment, dono/zona fonte/alvo | Preservar os campos separados e a ocupação de zona; validar alvo novamente ao resolver. Não criar fila nativa nem generalizar metadados1 para outros tipos. |
| ATK/DEF | NativeAiAttributes.EquipmentDelta | Já contém **os três deltas** (type300Both e attribute400Minus200). Reutilizar; não criar tabela duplicada. Demo.Bonus atualmente conta todos os equipamentos ×300: separar Attack/Defense e consumir o delta, sob manifesto explícito. |
| Revalidação | Predicado comum de elegibilidade | Revalidar resolução e contribuição com contexto atual conhecido; não afirmar que recálculo zero exige retirar carta. A política de desvincular ao mudar tipo ainda precisa de evidência. |
| Limpeza | DemoDuel.RemoveMonster e Equipment.Owner | Reutilizar destruição comum por dono; especificar retirada isolada do equipamento e teste de reutilização de slot. Sem implementar troca de controle/Banisher ou gatilhos sem contrato. |
| Interface | Seleção existente em Joey.App/DuelScreen.cs | Hoje fixa ID300/Warrior; futuro texto e seleção apenas para whitelist. Não decidir suporte de carta por descrição. |

NativeAiAttributes.FromPlainCard já extrai atributo estático de Properties>>29;
só serve para contexto declarado sem remapeamento. FromResolvedContext continua
fechado a monstros77/531; não abrir para todos os monstros porque um equipamento
é suportado. EquipmentDelta é primitiva de contribuição, não verificador da
ativação. Os testes existentes contra Python são sintéticos/portabilidade.

Sequência proposta para a próxima sessão, após revisão do usuário:

1. Fechar contrato pequeno de elegibilidade com tipo/atributo **resolvidos**,
   ocupação, face e guardas genéricas explicitamente satisfeitas ou desconhecidas.
   Expor somente os três IDs. Não inventar implementação completa de 0056C510.
2. Reutilizar EquipmentDelta para ATK e DEF separados; validar positivos e negativos:
   T11 admite Beast Fangs, T15 admite Sword, A2 admite Dark Destruction; A≠2 rejeita
   mesmo se T adequado; contextos faltantes Unsupported. Testar DEF reduzida,
   clamp no estágio correto e coexistência de contribuições no cenário declarado.
3. Estender o vínculo do demo para duas cartas novas mantendo origem/alvo,
   revalidação, zona livre, alvos nos dois lados e retirada comum. Cobrir falha sem
   consumir mão/zona, equipamento removido, monstro destruído e slot reutilizado.
4. Registrar apenas os escopos testados como SyntheticOnly/Partial no novo baseline.
   Sem ValidatedInScope até observação nativa independente; nenhum dos demais P025
   promovido. Não introduzir engine genérica de equipamentos, eventos ou callbacks.

Regras seguras para implementação limitada: predicados e deltas explícitos com
contexto fornecido, vínculo/limpeza no sandbox com política declarada. Dependem de
contexto nativo ainda não observado: legalidade integral, semântica das flags,
resolução de remapeamentos/supressão, temporização, movimentos alternativos,
efeitos adicionais C/E e contadores D. Os seis ramos D têm código estático visível,
mas contratos de entrada/ciclo ainda incompletos; não preencher contadores com zero.

## Reprodução sem executar o jogo

```bash
sha256sum ../passion/joey_pc.exe
python3 -m unittest discover -s tests -p 'test_p025_inventory.py' -q
rg -n 'case 300:|case 0x12d:|case 0x133:|LAB_0056ffda|LAB_0056ff13' docs/evidence/legendary-sword-link-and-filter.txt docs/evidence/legendary-sword-bonus-table.txt
rg -n 'case 1:|0x416|0x290|0x424|0x5a9|0x5aa|0x60e' docs/evidence/legendary-sword-state-and-stats.txt
```

O teste confere que esta tabela contém exatamente os 41 registros do catálogo,
sem perda, duplicação ou troca de índice/recurso/nome. Isso não testa comportamento
nativo. Não rodamos os validadores históricos que regravam relatórios de evidência.
**Ponto de parada: proposta pronta para revisão; P025 não implementado.**
