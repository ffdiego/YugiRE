# Limite da mão ao encerrar o turno

Análise estática do EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Endereços VA, image base `0x00400000`. Nenhuma execução do original nesta etapa.

## Comprovado no código

No estado 100 de `0x005A5120`, o fluxo em `0x005A58BC` consulta
`0x005699C0(0x593)`. O ID 1427/0x593 corresponde a **Infinite Cards** nas
tabelas do DAT. Se a consulta retorna zero e o byte de contagem da mão do
jogador ativo é maior que seis, solicita **contagem − 6** escolhas por
`0x005BCDC0(lado, quantidade, 0, 0)` e retorna sem avançar esse estado.
Ao retomar o handler, a condição é novamente avaliada.

`0x005BCDC0` prepara o descritor compartilhado com **tipo 1** via
`0x005BCC70`. O processador `0x005BC9B0` encaminha esse tipo a `0x005A90E0`.
Não confundir com tipo 2/`0x005A91F0`, usado no custo de Tribute to the Doomed.
O caminho de seleção escolhe cartas da mão; o movimento chama `0x00575970`,
que prepara informações por `0x005700F0` e usa o movimentador `0x005759A0`.
O descarte básico do demo apenas transfere a carta escolhida ao cemitério.

A exceção de Infinite Cards consulta os **dois jogadores**: `0x005699C0`
soma `0x00569820(1, ID, -1)` e `0x00569820(0, ID, -1)`. Para essa classe,
o helper percorre as zonas 5–10, exige o ID correspondente, bit de face aberta
e ausência de determinados bits de supressão; também exige retorno zero de
`0x0056B440`. Não foi encerrado aqui o significado de cada fonte de supressão.

## Reimplementação e limites

`DemoDuel.EndTurn` entra em `DemoPhase.End` quando há mais de seis cartas.
O botão Use card passa a **Discard**; o jogador escolhe na mão e confirma.
Outras ações ficam bloqueadas e pressionar End turn novamente não pula essa
etapa. Cada escolha vai ao cemitério do jogador que encerra o turno. A última
escolha necessária alterna o jogador.

A regra de seis é comprovada. O estado compacto End, o botão, a transferência
direta ao cemitério e a alternância imediata são orquestração do demo, sem
fila nativa, respostas, efeitos disparados ou exceções de destino.
Infinite Cards agora pode ser usada por Play no demo, ocupando uma das cinco
zonas de magia e permanecendo aberta no campo. O limite é consultado pelo
estado atual das zonas dos dois jogadores, em vez de uma flag permanente de
ativação. Equipamentos usam essas mesmas zonas, evitando sobreposição.
Ativação imediata sem correntes, ausência de negação e permanência até reset
(não há destruição de magias implementada) são limites da demonstração.
Não estamos reconstruindo todos os efeitos de fim de turno.

## Validação e comparação no Windows

`python scripts/validate_hand_limit.py` verifica o fingerprint, cinco trechos
de instruções e três destinos de CALL. [Resultado](evidence/hand-limit-validation.json).
As verificações C# cobrem oito cartas, descarte múltiplo, índices inválidos,
bloqueio de outras ações, impedimento de pular a etapa e o caso de seis cartas.
O teste com deck importado também chega a sete cartas por compras normais.
Total desta etapa: **120 verificações C#**, build sem avisos/erros.

Para conferir sem depender de uma carta específica: no original, passe turnos
sem jogar cartas até terminar um turno com sete na mão. Compare a necessidade
de escolher uma carta e ficar com seis. No demo com deck importado, compre
uma vez em cada turno e não invoque: a segunda compra do jogador 1 leva sua
mão de seis a sete. End turn deve pedir um descarte e só então alternar o lado.
Apresentação e timing permanecem pendentes de comparação visual.

Evidências: [handler](evidence/end-turn-hand-limit.txt),
[instruções](evidence/end-turn-hand-limit-instructions.txt),
[pedido e exceção](evidence/end-turn-discard-dispatch.txt),
[descritor e filtros](evidence/end-turn-discard-selector.txt),
[seleção](evidence/end-turn-discard-movement.txt),
[movimento](evidence/end-turn-discard-application.txt).

Validação acumulada após Infinite Cards: **143 verificações C#**. A cobertura
inclui ativação sem envio ao cemitério, aplicação ao adversário, retorno do
limite após uma remoção montada em fixture, campo de magias cheio e divisão
das cinco zonas entre a magia contínua e Legendary Sword.
