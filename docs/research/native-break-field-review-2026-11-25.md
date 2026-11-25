# V22 — quebra em campo: passes, gate e revisão (25/11/2026)

Fonte: SQLite cumulativo v3 SHA256 9953a4eb3af861b034f91863aa99adfcaa83d7d34c671eb8f20315cfa37948ed,
quick_check ok, aberto read-only. ZIP 29 SHA256
2c8f9bea150a4de82d7b76e324ee5977337525e65904f785a6d6546f43586844,
fingerprint EXE confere. Sem nova coleta ou publicação de dados privados.
A projeção radial FSQRT + __ftol já pertence ao PR #160;
a auditoria dos opcodes brutos pertence ao #167 (CI 45550784369 verde).
Esta fatia usa a sobrecarga de divisor explícito comum à main e #160.

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
a revisão aceita todas essas representações de campo.
Não usar NativeDisabledChainTarget para validar Break:
seu produtor usa apenas regiões canônicas 0/5/10.

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
O campo deve ser região+slot 0..10. F7 avança um Update,
F8 reinicia, F12 salva PNG e JSON com partículas, recortes,
destinos, contador e predicado de liberação. Escape encerra.
Não há som automático, gate real, mutação de estado ou segunda fila.
A convenção de perspectiva reutiliza as revisões existentes
(1-perspectiva para o bit A54E5C); conferir o original no desktop.

Limites: compositor +AC/+C, chave verde, tint, orientação de
textura Raylib e comparação pixel a pixel ainda não verificados.
A mão (0041F130), posições de deck/cemitério e snapshot FF
não são aceitos neste review; a geometria viva dessas zonas exige
contrato próprio. A03 fornece o snapshot autorizado antes da
mutação e integra apresentação sem recalcular regras.
Sem nova Draw no duelo: V22 continua Pending no inventário.
