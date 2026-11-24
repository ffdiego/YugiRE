# C16 — Modificadores elementais de batalha

SQLite cumulativo v2, decompilation 0056DD30:
- 0x5ec (1516): se lado == bit 0 de DAT_00a577f8._2_1_ e
  máscara de fase & 0xe == 6, iVar14 = 300.
- 0x5ee (1518): mesma fase, lado != bit 0, iVar14 = 300.
- 0x5eb (1515): para lado ativo nessa fase, contador
  005699a0(1-lado,0x5eb) vezes -300. Helper 00569820
  percorre cinco slots, com flags de visibilidade/estado e
  0056b440 para supressão. Sandbox limita às fontes face-up
  não marcadas para destruição; não reproduz toda supressão.

A fase é DemoPhase.Battle, modificador calculado junto com aura,
campo/equipamento e clamp final. Sem mutação de atributos
ou segundo produtor de comandos. Defesa inalterada. O código
de combate existente usa AttackValue, logo os efeitos afetam
resultado e LP, não apenas o display. Ambas as posições face-up
são válidas. Custo e invocação permanecem na fila de C16.

1515 Soul of Purity and Light, 1516 Spirit of Flames, 1518
identificado pelo ID no pack e pelo branch nativo (nome The
Rock Spirit a confirmar no DAT). Done no escopo headless
de duelo C# depois de CI verde; sem alegação de suporte
UI/CPU, Spirit Elimination, negação ou replay nativo.
Aqua Spirit/Garuda permanecem Partial, fase Standby pendente.

Testes: tabela por fase/lado, duas fontes empilhadas, face-down,
remoção, clamp, summon real por ocorrência, Main2 e dano de
combate de ambos os lados. CI pendente.
