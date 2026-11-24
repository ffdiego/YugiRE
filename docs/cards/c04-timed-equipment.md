# C04.3 — Contadores de Stim-Pack e Germ Infection

Fonte: SQLite privado joey-knowledge-visual-decoded.sqlite, read-only,
PRAGMA quick_check ok; SHA256 EXE db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.

| Resource | Perfil | Primário | Filtro 00579CE0 | 0056DD30 |
| --- | --- | --- | --- | --- |
| 1060 (0x424) Stim-Pack | P025 | 00584500 | comum, sem restrição específica | ATK += 700 - 200*c |
| 1046 (0x416) Germ Infection | P025 | 00584500 | tipo dinâmico != 7 | ATK += -300*c se tipo != 7 |
  
Ambos usam c=(byte da ocorrência fonte & 0x3C)>>2, portanto 0..15.
Não é contador do alvo, não é DEF e não é valor base modificado.
A guarda do chamador inclui fonte ocupada/face-up e bits de supressão.
00584500 resolve/revalida e chama 00574990 para criar vínculo; o trecho
decompilado não identifica avanço de turno para esses IDs.
0040B270 é consumidor de 0x60 em 005B9470: soma valor do comando ao
campo de quatro bits, satura em 15 e preserva outros bits. Não foi
demonstrado que seja o caminho de incremento das duas cartas.
005749B0 emite 0x8A antes de B2 sob param_4==1 e contador !=0;
não concluir reset do contador a partir do número do opcode.
0059C8D0/0059CA20 emitem 0x8B em outros contextos (o segundo testa
0x62), não provam avanço temporal de P025. 00574480 revalida vínculos,
incluindo 0x416, e pode solicitar sua remoção. O catálogo P025 compartilha
0057A470/00584500/00593A20; não inferir timing a partir de perfil.

TimedEquipmentRules encapsula somente aritmética com contador explícito.
Não adiciona whitelist de ativação ou contador mutável ao motor.
A07 aguarda roteiro 11 (drivers de fase), já existente. Depois do retorno,
procurar produtor que decide lado/fase para 0x416/0x424 e o comando de
atualização de ocorrência. Se ausente, uma coleta dirigida de escrita
ao campo da fonte durante duelo controlado deve comparar contagens
após ativação e em cada fase dos dois lados. Não repetir bundles gerais.

Sem paridade temporal, sem promoção de cobertura. Testes cobrem os 16
valores, Machine e limites; CI pendente.
