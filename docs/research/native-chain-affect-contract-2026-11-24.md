# V23/V24 — corrente e destaque contextual de vínculos

Fonte: joey-knowledge-visual-decoded.sqlite, aberto read-only,
PRAGMA quick_check=ok, SHA256 do executável:
db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Decompilações transientes existentes: bundle e589a5263234a28d0e93034d2d54abde536a784698fd592b6001ee7eef6414f7,
retry-004/005/006 (0041F810/830/870), retry-001/002 (0040E010/260).
Não se trata de nova extração ou validação por assembly; este lote
cruza as decompilações já importadas com construtores e chamadores
persistentes. O parser de 0040E260 avisa sobre jump tables; não
portar os ramos ambíguos.

## V23 — cadeia, opcode 08/09

005B9470 case 8 → 00408A40 → 0041F530 → 0041FD60
→ 0041F5C0. O contêiner recusa nova instância se o número (+14)
já existir. Argumentos lidos: bit 15 de word A (lado), word B
(zona combinada), word C (número). O objeto tem 0x1C bytes;
descritor truncado 0x8000 | ((zona & 31)<<1) | lado,
contador zero, flag de liberação zero, camada 0x120+número.
Banco compartilhado duel/chain/chain.txt, som 0x1000001E.

00408A40 incrementa o estado baixo a cada chamada e libera o
comando quando excede 4 (ou logo se criação foi recusada).
**Não espera que a animação inteira termine.**

Case 9 → 00408AB0 → 0041F550 → 0041FEA0 → 0041F800:
procura pelo número OU (se número pedido =0) pelo descritor
lado/zona combinada, seta +18=1. No estado seguinte, consulta
0041F570 que testa o virtual +4 de todos os objetos; quando
nenhum é verdadeiro, libera 0x4000. Não confundir +4 virtual
(0041F810) com o byte ativo +4. O predicado é falso quando
inativo OU (não liberado e contador==128). No quadro mantido,
o objeto continua desenhável sem bloquear esse predicado.
0041F830 incrementa +4 até 128, mantém se não liberado;
após liberação avança até 192, quando limpa o ativo.

0041F870: contador 0 não desenha; até 128 inclusive usa
chave min(t/4,25), depois max(1,15-(t-128)/4).
O desenho principal é centralizado horizontalmente, com
deslocamento Y adicional -16 em relação à âncora calculada
por 0045C140. O número, se diferente de zero, usa uma passagem
de dígitos decimais (recurso consultado com chave 0), consumidos
da direita para a esquerda via %10 e /10; geometria/intensidade
do texto depende de registradores/stack mal reconstruídos.
Chave do lookup não é automaticamente índice de frame
nem índice físico de manifesto. Não deduzir dimensão ou
cor, nem reutilizar disable.txt.

NativeChainTimeline porta contadores, predicado, seletor,
descritor e teste de correspondência de liberação. Sem
renderer integrado ou duração absoluta.

## V24 — affect.txt: destaque contextual, não um estado genérico

0044C3E0 constrói um único objeto 0040DAC0 junto ao renderer
do duelo. Usa affect.txt, camada 0x101. 0044DBC0 zera o
ponteiro de foco DAT_005F2FDC antes do hit-test, 0041E3D0
pode preenchê-lo com o objeto visual sob o cursor; outros
produtores 0040AD90 e 0040AF10 também selecionam objetos
durante comandos 19/1B. Logo a identidade de foco é da
ocorrência visual, não do resource ID nem apenas do mouse.
0040E010 soma 8 módulo 256; reseta para zero se o novo foco
for não nulo e diferente do anterior. Sem foco, a contagem
continua, mas 0040E260 não desenha.

0040E260 lê descritor via 00418B50: side=bit0,
zone=((desc>>6)&255)+((desc>>1)&31). Gate zone<11 e
ocupante low12 não zero. Enumera relações de saída/entrada
e dependências de efeitos dos dois lados; não é simplesmente
um efeito permanente sobre toda carta equipada.

Ramos recuperáveis:
- seletor 5 em relações que satisfazem 0056F5C0;
- seletor 3 em relações de campo 0056F430;
- seletor 4 no caso especial resource 0x536 (Thousand-Eyes
  Restrict), contagem 005699C0 menos a própria ocorrência;
- seletor 2 em condições de 0x148 (Dragon Capture Jar),
  classe e face-up, com gate de desativação no ramo inverso;
- varredura de vínculos de entrada: kinds 1,4,5 e 2,6
  com alvo exatamente igual a side/zone em foco. Para foco
  monstro, padrão 2; resource 0x52 → 5, 0x2D9/0x534 → 4.
  Para foco zona >=5, respectivamente 1 ou 2. IDs são
  Castle of Dark Illusions, Dark-Eyes Illusionist e Invitation
  to a Dark Sleep, conforme card_identities.
- Outros ramos da jump table têm warning; permanecem pendentes.

0040E1F0 recompõe scratch quando muda o seletor (+128h)
e chama virtual +50 com centro, zero e intensidade
trunc(((C[(t-128)&511] >> 8)*7)/8).
0040E040 consulta dois recursos/recortes, rola a superfície
e aplica passagem de canais; argumentos perdidos impedem
a reconstrução visual completa. Não assumir alpha raylib.
NativeAffectTimeline porta a fase, reset por identidade,
intensidade assinada e o ramo de vínculo de entrada. O
cliente futuramente recebe fatos resolvidos/visibilidade
do Core; não lê regras ocultas nem recalcula efetividade.

## Validação e lacunas

Testes sintéticos separados das regras: corrente espera
indefinidamente em 128, libera antes/depois da espera,
deativa em 192, seleção e predicado distintos, lookup
por número/zona; affect reinicia ao trocar/reobter foco,
32 updates por período, preserva divisão assinada e
seletores recuperáveis. Sem alteração no Core, nenhuma
rotina Draw ganhou integração; cobertura 20/34.
Para completar: conferir assembly de 0041F870, 0040E260,
0040E040 e 0040E1F0 (argumentos/lookup/jump tables).
Usar inspeção de DAT existente para chain.txt e affect.txt,
preservando ordem, parâmetros e dimensões. Não repetir
a importação do bundle de decompilações transientes.

Coleta dirigida: `scripts/pending/17-chain-affect-assembly.sh` solicita apenas assembly dos quatro pontos acima. Ainda não executada; sem alterações no banco privado nesta fatia.
