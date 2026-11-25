# Auditoria dos opcodes visuais — V12, V20–V25 (25/11/2026)

## Evidência e procedimento

SQLite cumulativo v3 consultado read-only, quick_check=ok, SHA256
9953a4eb3af861b034f91863aa99adfcaa83d7d34c671eb8f20315cfa37948ed.
Coleta 29 (ZIP SHA256 2c8f9bea150a4de82d7b76e324ee5977337525e65904f785a6d6546f43586844),
fingerprint EXE db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Manifest declara 5/5 funções e memória completa. Não versionar ZIP, tabelas
binárias ou decompilações. Nenhuma coleta nova é necessária para estes mapeamentos.

Em 005B9470, opcode = wordA & 0xFFF. Domínio 1..256; byte
em 005B9AEC + opcode - 1 é índice do switch; dword LE em
005B9838 + índice*4 é endereço do stub. O rótulo case do
Ghidra é o **índice**, não o opcode. Foram lidos os 256 bytes
e os 173 destinos (692 bytes) do roteiro 29, e cruzados os
stubs abaixo com refs de chamadas do dispatcher no SQLite v3.
Todos os mapeamentos da tabela abaixo têm uma única entrada
bruta no domínio. Isso confirma despacho, não uma execução
real nem semântica de todos os argumentos.

| Opcode bruto (hex) | Índice (hex) | Stub | Handler | Família |
|---|---|---|---|---|
| 0A | 08 | 005B94C6 | 00408A40 | V23 criação de corrente |
| 0B | 09 | 005B94CB | 00408AB0 | V23 liberação de corrente |
| 1E | 18 | 005B9516 | 0040AA40 | V25 ativação por busca de ocorrência/fallback |
| 1F | 19 | 005B951B | 0040AD90 | V24 seleção contextual |
| 21 | 1B | 005B9525 | 0040AF10 | V24 seleção contextual |
| 72 | 48 | 005B9636 | 00406B20 | V25 ativação normal/alternativa |
| 73 | 49 | 005B963B | 00406F70 | V25 alternativa |
| 79 | 4F | 005B966D | 00404B40 | V22 break |
| 7A | 50 | 005B9672 | 00405620 | explosão |
| 7C | 52 | 005B967C | 00405190 | explosão (cópia de combate) |
| B1 | 72 | 005B9708 | 00408730 | V12 indicador disable |
| E0 | 8E | 005B9794 | 0040A7C0 | V20 coin |
| E1 | 8F | 005B9799 | 0040A900 | V20 coin |
| E2 | 90 | 005B979E | 0040A690 -> 0040A6A0 | V21 dice |
| E3 | 91 | 005B97A3 | 0040A690 -> 0040A6A0 | V21 dice |
| E4 | 92 | 005B97A8 | 0040A6A0 | V21 dice |

## Consequências para os produtores

- V22: 00577CF0 emite 7C para slots 0..4 do combate; 00578030
  também emite 79, para uma rota baseada no snapshot de combate
  com região FF. 00579330 emite 79 para regiões B/C/D; 00576B30
  seleciona 79 em ramos de flags/identidade específicos. Logo nem
  todo caso de combate é necessariamente explosão, nem todo
  movimento ao cemitério é break. A rota 7A chama o mesmo
  construtor 004511D0 em seu subestado 1, mas consulta uma
  ocorrência visual por 0041CEE0 no subestado 0; 7C consulta
  A57864/A578F4. Não foi localizado produtor literal 7A
  nas chamadas diretas indexadas de 005B9170/005B9370.
  A investigação de normalização radial e produtores de 79/7C
  permanece no PR #160; não duplicar suas mudanças.
- V20/V21: produtores de 8E/8F/90/91/92 não provam lançamento
  de moeda/dado. O despacho real destes cinco valores vai para
  00408EE0/00409240/00409700/00408030/00573C30.
  Em particular 8E/8F têm movimentação de cartas via 0040CDB0.
  Não há produtor literal E0..E4 nas chamadas diretas indexadas
  de 005B9170/005B9370 nesta consulta; produtores indiretos,
  comandos remotos e dados de replay ainda precisam ser ligados.
  Conservar argumentos brutos até a identificação da origem.
- V23: 005BA230 confirma produção dos opcodes 0A e 0B.
  Com param_2=0, enfileira um 0A por elo (número i+1).
  Com param_2!=0, enfileira 0B com número e descritor da
  ocorrência. 08 bruto tem outro handler (00404460);
  09 bruto vai ao default AC. Nenhuma alteração nos clocks.
- V12: 00583510, 00588D90, 0059CCF0 e 005BB720 produzem B1
  com lado/zona e argumentos 1,0. 00588D90 é callback
  compartilhado (Magic Jammer, Seven Tools of the Bandit,
  Call of the Grave, Maryokutai, Riryoku Field, entre outros).
  Seus ramos de resource IDs 405/406/5F5/5FB e 3FD
  emitem B1 nas condições do callback; 3FD exige alvo
  Monster Reborn (3F0) adversário. 005BB720 case 4 também
  emite B1 quando as condições de supressão do elo são
  satisfeitas; 0059CCF0 varre slots S/T ocupados e face-up
  com categoria 15h. Não generalizar a toda negação.
  Handler 00408730 escreve bit 5 por 00570710(side,zone,5,C);
  cria indicador somente se C != 0 e D == 0, zona 0..10
  ocupada, subestado zero e sem objeto anterior bloqueante.
  00578390/005783C0 emitem 72, que é ativação V25, não B1.
- V25: 00578390/005783C0 produzem 72, 00578400 produz 73;
  0059CBB0/0059CD70/0059D040 e outros emitem 1E com
  identidade da carta. Isso reforça o contrato de seleção
  de ocorrência do handler, não define uma regra nova.

## Implementação e fronteira

NativeEffectCommandRouting.Family agora usa os 12 bits baixos,
classifica 79, 7A/7C, E0/E1 e E2/E3/E4; os índices antigos
deixam de ser reconhecidos como esses bancos. Explosion é
família distinta; BankAnchorDescriptor continua exclusivo dos
bancos coin/dice. Teste percorre os 4096 valores de opcode
e os 16 valores dos bits altos, além de validar as âncoras e
rejeições. Classificação de família não substitui um comando
tipado nem a seleção da instância de apresentação em A03.

Este lote não muda o scheduler, o Core, renderers, a métrica
VISUALS ou estado Verified. C# requer CI (SDK indisponível no
ambiente desta sessão); comparação desktop e produtores
indiretos permanecem pendentes. Manter 160 separado até
reconciliar seus detalhes de V22.
