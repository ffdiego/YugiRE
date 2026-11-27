# V22 — quebra em campo: passes, gate e revisão (25/11/2026)

Consulta read-only no SQLite cumulativo v7 com `quick_check=ok`.
O fingerprint EXE é `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
O ZIP 29 tem SHA256 `2c8f9bea150a4de82d7b76e324ee5977337525e65904f785a6d6546f43586844`.
A reconciliação incorpora a projeção de #160 e a revisão de #168
sobre o roteamento bruto já integrado por #167.

`00559110..0055911B` executa FILD e FSQRT e chama o auxiliar
`005C3E40`, que usa o modo x87 de truncamento e restaura a palavra
de controle. `00559121..00559139` normaliza cada componente com
divisão assinada quando o divisor é diferente de zero. No cliente,
`NativeBreakScatter.RadialDivisor` usa a raiz inteira e
`NativeBreakFieldReview` chama a mesma projeção que os testes de
geometria. Em 50×72, com origem (100,200), o primeiro fragmento
em parâmetro 128 começa em (94,192).

## O objeto não bloqueia enquanto estiver meramente visível

Os 16 bytes da vtable 005DB314 confirmam slots 0/4/8/C:
00450E60 / 00450E50 / 00450FB0 / 00450FD0.
00450E50..00450E5B: CMP [obj+10h],40h; SETL AL.
00450FB0 soma 16 e mantém ativo enquanto t<256.
Logo, +4 é verdadeiro nos contadores 0,16,32,48 e falso
nos demais contadores visíveis 64..240. O estado 4 de
00404B40 consulta +4 dos objetos do contêiner 005F3280+132C/1330,
não o byte ativo. O objeto de break sozinho deixa de impedir
a liberação após quatro Updates, embora continue desenhável por
mais doze. Outros objetos/estados do comando ainda podem impedir
a liberação. Não trocar isto por um gate global de 4 Updates.
NativeBreakTimeline.BlocksRelease é uma consulta pura.

## Draw e DAT

00451042..004510CD itera cinco vezes. O manifesto original
j/duel/effect/break.txt tem uma linha shine.bmp. O inventário DAT
v3 (visual-dat-20261124) confirma largura 74, altura 73.
0045105E..00451071 modifica somente DL: OR DL,FFh; SUB DL,AL
(t é múltiplo de 16). O valor empilhado a +AC é
((floor(i*512/5) & ~255) | (255-t)). As três primeiras
passagens têm o mesmo byte baixo das duas últimas, mas as duas
últimas retêm 0x100. Não reduzir a palavra inteira a uma
opacidade comprovada. Preservar RawCompositorArgument; a
tint do review usa só o byte baixo como aproximação explícita.

0045111E usa 0041F130 na região 11; 00451156 usa 0041F2E0
nas demais. Este último usa o banco mini e as variantes
0/90/180/270. A fórmula do argumento é
90*(positionBit+2*(side XOR A54E5C.bit0)).
0045C280 normaliza região+slot quando a soma é <11;
a revisão aceita todas essas representações de campo, e o snapshot de combate descrito abaixo.
Não usar NativeDisabledChainTarget para validar Break:
seu produtor usa apenas regiões canônicas 0/5/10.
Os centros de monstros e S/T foram conferidos nos blocos
`005DB560` e `005DB510` da coleta 33. Para FIELD, a coleta 20
registra os dois pares em `005DB5E0` dentro do bloco `005DB5B0`.

0045115B..004511BB mede a superfície JÁ orientada e chama
00558FB0 com parâmetro 256-(C(t/2-128)>>8), modo 0, 255, 0.
A tabela de cosseno capturada no v3 confirma a sequência:
256,231,207,182,159,136,114,94,75,59,44,31,20,12,5,2.
No primeiro contador há blit direto; depois os fragmentos
4x2 são gerados em ordem linha/coluna, respeitando bordas.
A revisão transforma os recortes orientados de volta em
coordenadas da miniatura não rotacionada para DrawTexturePro.
Não rotacionar o plano de dispersão depois de particionar
a miniatura: em defesa a quantidade de blocos muda.
50x72: 468 blocos em 0/180; 72x50: 450 em 90/270.
Uma referência Python independente percorreu todos os pixels
para 50x72 e 51x73 em quatro ângulos, sem lacunas/duplicações,
e conferiu os 16 parâmetros contra os 512 valores capturados.

## Executar revisão isolada

```powershell
dotnet run --project src/Joey.App -- ..\passion\data.dat --review-break=250,8080 --review-perspective=0
dotnet run --project src/Joey.App -- ..\passion\data.dat --review-break=0,4080 --review-perspective=0
```

250 é exemplo de resource ID existente; use um ID real.
Descritor hexadecimal sem 0x. Bit 8000 = identidade revelada,
bit 4000 = posição; oculto requer ID 0 e carrega apenas o verso.
O campo deve ser região+slot 0..10 ou região monstro 0..4 com slot FF. F7 avança um Update,
F8 reinicia, F12 salva PNG e JSON com partículas, recortes,
destinos, contador e predicado de liberação. Escape encerra.
Não há som automático, gate real, mutação de estado ou segunda fila.
A convenção de perspectiva reutiliza as revisões existentes
(1-perspectiva para o bit A54E5C); conferir o original no desktop.

Limites: compositor +AC/+C, chave verde, tint, orientação de
textura Raylib e comparação pixel a pixel ainda não verificados.
A mão (0041F130) e posições de deck/cemitério não são aceitas;
a geometria viva dessas zonas exige contrato próprio. O snapshot
FF aceita uma ocorrência explícita no review, mas sua leitura antes
da mutação ainda depende de A03. A03 fornece o snapshot autorizado antes da
mutação e integra apresentação sem recalcular regras.
Sem nova Draw no duelo: V22 continua Pending no inventário.


## Contrato do produtor: exemplos e correção do slot FF

O B de 0x79 é composto por **região no byte alto, slot no baixo**,
como 00404B40 lê A550A0>>24 e (A550A0>>16)&FF.
005B9170 não normaliza B para opcode 79.
00576B30 emite B=zone, ou seja, região 0, slot=zone.
00579330 emite B=(region<<8)|slot (regiões B/C/D).
00578030 emite B=(monsterZone<<8)|FF: **FF é slot, não
região**. 00570230, 005700F0, 005703A0 e 00570420
selecionam a cópia em A57864 ou A578F4 quando região+slot
não cabe no campo e não é região especial B..F. A escolha
usa lado e zona de A57840. O descritor resultante preserva
a zona de monstro como região e FF como slot. 0045C280,
nesse caso, não normaliza região+slot; cases 0..4 usam
apenas região para o centro. NativeBreakFieldTarget porta
esta âncora. Não consultar a carta atual do campo para
reconstruir a identidade/posição do snapshot.

Exemplo estático confirmado: Man-Eater Bug, Mystical Space
Typhoon, Dream Clown, Driving Snow, Gust, Michizure,
Thousand Knives e Tribute to the Doomed compartilham
primary 00585C80 (card_callback_slots). Após validação de
alvo e gates (incluindo 00583510 para IDs 3FF e 4BB),
esse callback chama 00577880, que gera máscara de lado/zona,
chama 00576940, que chama 00576960 com dest E e flags 2.
00576960 conserva bit 1 nas flags enviadas a 00576B30
e este emite 79 para a remoção admitida. O caminho
confirma Break para a remoção bem-sucedida desses efeitos
no recorte analisado; imunidades, seleção inválida e
ramos especiais continuam possíveis.

Tributos: 005ABD00 emite 57 para os slots escolhidos e
00577B30 chama 00576960 com dest E e flags 1000h.
00576B30 seleciona 95 nesse ramo, não 79. 005999D0
(callback de rituais) também usa 00577B30. Isso distingue
a apresentação de custo daquela de destruição por efeito.
Não atribuir a 95 uma animação específica sem seu Draw.
Nobleman of Crossout/Extermination: callback 0058C080
chama 00577950 com flags 0 (dest F); caminho ordinário
8F, não break obrigatório (exceções de ID/flags mantidas).
Não confundir "enviado ao cemitério" com 79.

Raigeki, Dark Hole, Mirror Force, Heavy Storm, Harpie's
Feather Duster, Trap Hole e Fissure: o v3 tem catálogo
de callbacks mas não funções/decompilações/Xrefs dos
respectivos endereços primary. Ainda sem confirmação por
essa fonte. Não preencher lacuna por semelhança de regras.
ACTIONS deve selecionar a rota antes da apresentação e
entregar snapshot de lado/posição/identidade autorizada.

## Reconciliação com a main de 27/11

O review de #186 foi reaplicado sobre a main após V04/V05/V11/V31,
sem substituir Program.cs e DatGraphics.cs pelos arquivos antigos.
Os modos de revisão existentes foram preservados. O fluxo vivo ainda
não escolhe Break: remover monstros por efeitos e por combate exige
proveniência explícita do produtor e snapshot anterior à mutação.
O roteiro 56 registra a validação gráfica pendente. CI novo obrigatório.

## V22.2 — integração inicial de Break no duelo (histórico; política substituída em V22.3)

Primeiro recorte vivo: remoção de monstros por Man-Eater Bug (500),
Dream Clown (119), Tribute to the Doomed (1023) e Thousand Knives (1211),
callbacks confirmados em 00585C80. DestroyCommand guarda somente a
proveniência opcional da carta de efeito, e RemoveMonster a transporta
no snapshot já capturado antes de ClearMonsterSlot. Nenhuma regra,
ordem de comandos ou alteração de RNG. Sem origem confirmada, ou sem
banco/miniatura, preservar a explosão atual. Alvos ocultos usam verso.
A fase bloqueante pertence ao comando por 4 updates; a cauda visual
é um objeto independente por mais 12, não FIFO lógico e não bloqueia
MotionBusy. F6/F7 usam o mesmo tempo de apresentação. A contagem visual
do cemitério deixa de ocultar a remoção quando libera a ação; sua
cadência exata ainda precisa comparação. O App mantém orientação do
campo para o snapshot vivo e usa a composição GPU compartilhada com
review; a equivalência da orientação do banco nativo, ordenação de
camadas sobre outros objetos, som 0x10000008 e +AC/+C permanecem
pendentes. Sem atribuir Break a todo efeito de destruição. S/T,
combate FF, outros produtores e teste real com DAT separados.
Badge sobe para Partial somente pela integração viva; não Verified.

## V22.3 — causa da remoção independente de destino

A hipótese de trabalho aprovada é que destruição por efeito usa Break
(79), e destruição normal em combate usa Explosion (7C). Os produtores
00576960/00576B30 e 00577CF0 sustentam a distinção; 7A e exceções de
flags/IDs ainda não foram classificados exaustivamente. Não alegar que
toda carta foi validada no original. Removida a lista de quatro IDs do
renderer. Console Destroy também segue o padrão de destruição por efeito.

Core expõe `RemovalCause` e `ICardRemovalCommand.Cause` nas operações
relevantes. Destroy, snapshot/finish de batalha, Tribute, SendToGraveyard,
Banish, Discard e retornos preservam seu tipo e destino. Polymerization
marca os pagamentos de material enviados ao GY como FusionMaterial;
Fusion Gate usa a mesma causa nos BanishCommand. O gasto da magia não
é material. Ritual marca TributeCommand como RitualMaterial, enquanto
invocação normal conserva Tribute. Limpezas dependentes de equipamento
e cascatas explicitamente reservadas de Destiny Board são LinkedCleanup.
A separação não adiciona comandos, muda regras ou reclassifica eventos
de fusão como destruição. Os snapshots de MonsterDestroyed distinguem
BattleDestruction e EffectDestruction inclusive Man-Eater durante combate.
MonsterTributed informa Tribute ou RitualMaterial sem trocar seu renderer.

S/T: o executor emite SpellTrapDestroyed depois de remover diretamente
um alvo reservado de efeito, usando carta, lado, zona e visibilidade
capturados antes da mutação. Break aceita os onze centros de campo e
usa verso quando oculto. Não introduz evento para custos, reposição de
FIELD, limpeza dependente, retorno, banimento ou material. Mantém os
4 updates de bloqueio e a cauda independente por 12. No recorte de
monstros sem banco/miniatura, conserva o fallback antigo de explosão;
S/T sem recursos mantém o comportamento anterior sem animação.

Testes de contrato exercitam Man-Eater durante batalha, MST, causa de
materiais de Fusion Gate/Polymerization e tributos de ritual. As
rotinas de fusão e sacrifício existentes continuam independentes.
Ainda não é um evento único de todas as remoções: mão, deck, custos
especializados e todas as rotas excepcionais exigem inventário próprio.
Comparação nativa de duração/compositor/ordem e som Break pendente.
