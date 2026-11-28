# scripts/pending — coletas de evidência nativa em aberto

Os roteiros executados estão em [completed](../completed/README.md) e os verificadores reutilizáveis estão em [regression](../regression/README.md). Preserve os números e distinga os roteiros 16 e 17 pelo nome completo. A referência privada das três trilhas é o [SQLite cumulativo](../../docs/research/joey-knowledge-sqlite.md).

## Protocolo de novas coletas — todas as trilhas

Fila única para ACTIONS (`A##`), CARDS (`C##`) e VISUALS (`V##`), incluindo Windows (execução/Frida/DAT) e Fedora (Ghidra/projeto local). Preservar números e histórico dos scripts 01–10; novos roteiros recebem o próximo número livre, nome descritivo e ID(s) da trilha na ficha. Cada novo roteiro deve especificar hipótese/pergunta, endereços quando aplicáveis, ambiente e dependências, comando reproduzível, artefato privado e critério de encerramento. Antes de preparar coleta, consultar o SQLite mais recente e os bundles já existentes. Enquanto a coleta estiver bloqueada, avançar em outra tarefa.
O exportador valida SHA-256, usa Ghidra -readOnly com
rollback e grava sob scratch/. Depois de consumir um bundle,
registrar conclusões em docs/research e mover o roteiro para [completed](../completed/README.md) ou
[regression](../regression/README.md). Preferir integrar evidências recuperáveis em nova cópia cumulativa do SQLite, com proveniência, fingerprint, auditoria e verificação de integridade; usar ou ampliar importadores existentes conforme o formato. Capturas/frames incompatíveis permanecem em artefatos privados referenciados. Registrar snapshot de origem e destino, importação concluída ou pendente, sem confundir coleta com implementação. Consultar [guia SQLite](../../docs/research/joey-knowledge-sqlite.md). Novas pendências devem corresponder a evidências específicas, não reexecutar coletas completas.

Ficha mínima para novos scripts: `número | ID da trilha | ambiente | pré-requisitos | comando | saída | pergunta | estado da coleta/importação | referência de conclusão`. Não executar automaticamente.

## V21 — Comando e cadência do dado em duelo (69)

Use o [Joey original em Proton isolado](../../docs/research/joey-proton-validation.md)
e GDB com Python. Anexe antes de resolver Graceful Dice com um monstro
próprio revelado ou Skull Dice com um monstro adversário revelado.

```bash
timeout --signal=INT 100s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/pending/69-wine-dice-card.gdb > scratch/dice-card.log 2>&1
```

O roteiro espera `E2` ou `E3` em `0058CAF1` e lê o resultado, a
região, o centro resolvido, os contadores e a liberação em `00456B84`.
Confira se o resultado de 1 a 6 chega ao Draw e se o objeto encerra
em 768, com 128 Updates previstos. Estado: coleta positiva pendente.
[Evidência estática](../../docs/plans/VISUALS.md#v21).

## V18 — smoke de áudio Windows (28)

A execução anterior registrou 129 falhas no cabeçalho RIFF. Com o adaptador para a variante PCM e o limite de inspeção para entradas armazenadas sem compressão, o cliente atual validou as 131 entradas no Linux. A captura Windows anterior está no SQLite cumulativo.

Próxima ação em Windows com o cliente atualizado: validar a inspeção com `-SkipPlayback`, ouvir as amostras do roteiro e observar a troca de música do menu para o duelo no App. Registre os resultados no relatório privado do roteiro. [Diagnóstico](../../docs/research/native-joey-reaction-audio-2026-11-24.md).

```powershell
powershell -ExecutionPolicy Bypass -File scripts/pending/28-voice-audio-windows.ps1 -SkipPlayback
powershell -ExecutionPolicy Bypass -File scripts/pending/28-voice-audio-windows.ps1
```

## V01/V04/V11/V12/V16/V17/V31 — 34-attack-draw-runtime.py (aguardando Windows)

Com Frida na venv e um duelo aberto no executável original, indique o
PID do jogo. Durante os 20 segundos de captura, execute um ataque.
Use saídas diferentes para ataque a monstro e ataque direto, dos dois
lados quando possível. O roteiro anexa ao processo indicado e desanexa
no fim. O relatório fica em `scratch/`.

```powershell
python scripts/pending/34-attack-draw-runtime.py --pid <PID> --exe ..\passion\joey_pc.exe --output scratch/visual-review/attack-targeted.json
```

O frame é ordinal da entrada em `0044DD00` durante a captura.
`00414110` e `004142C0` registram estado e contador antes de
Update e Draw. Os retornos `004146EA`, `00414777`, `00414805`
e `004148FA` identificam respectivamente voo, impacto, lâmina
parcial e mira nas entradas de `00422840` ou `00422C00`.
A chave vem do chamador e do manifesto `attack.txt`.
X/Y são o centro passado ao blitter. O índice de ângulo, as escalas
16.16 e a intensidade bruta vêm da pilha na entrada. O log registra
pedidos de blit antes de clipping, formato, cor-chave ou descarte
por escala zero. O ordinal da cena também não é um flip do buffer.

Saída 0 significa que houve amostras dos quatro passes e o limite
de eventos não descartou registros. Saída 1 preserva o traço
incompleto para diagnóstico. Antes de encerrar a coleta, conferir a
ordem dos passes e os contadores de um mesmo ataque e manter os
traces privados. O recorte cobre a espada e o impacto. Elegibilidade,
reflexo, rasterização, demais famílias e cadência sem instrumentação
têm validação própria.

### V01/V11/V17 — fundo da seleção no roteiro 34

No mesmo ambiente, abra uma seleção de cemitério, deck ou fusão.
Deixe a janela chegar ao estado estável durante a captura. Grave
um relatório por família e por variante com e sem scroll.

```powershell
python scripts/pending/34-attack-draw-runtime.py --mode selection --pid <PID> --exe ..\passion\joey_pc.exe --output scratch/visual-review/selection-grave.json
```

O ponto `0043B47E` registra a chamada `+4C`, as vtables de
destino e origem, o destino virtual resolvido e a chave de manifesto
em EDX. Para a vtable `005DC6EC`, inclui os formatos, o flag
`+78h`, as chaves em `+64h` e `+70h` e a origem da amostra.
O evento `selection-composition` registra o ramo alcançado por
`0041AB40`, o código de retorno do compositor e as leituras
dos getters de chave para a mesma superfície de origem.
Saída 0 exige que a captura observe o compositor de um fundo
estável sem perder eventos. Confronte o ramo, os formatos e a
chave lida com a imagem de referência e as variantes do DAT.
O modo aguarda execução no Windows.

### V01/V17 — cadência da cena no roteiro 34

Com o duelo aberto no Windows, grave um cenário estável em cada
inicialização do jogo, padrão e com `-speedy`. Use saídas distintas
e registre se o jogo está em janela ou tela cheia. A notificação de
sucesso indica pelo menos dois pares instrumentados em um mesmo
thread e nenhuma perda de eventos.

```powershell
python scripts/pending/34-attack-draw-runtime.py --mode cadence --pid <PID> --exe ..\passion\joey_pc.exe --seconds 20 --output scratch/visual-review/cadence-standard.json
```

As entradas de `0044DD00`, `0045D040` e `0045D170` recebem
timestamps de `QueryPerformanceCounter`. Os endereços de retorno
`0044DD47/0044DD62` e `0044DD8A/0044DD92` restringem a
observação aos dois ramos da cena. O relatório guarda a máscara
de cada dispatcher, a frequência do relógio e o valor nominal de
`005EB618`. Resume a ordem dos dispatchers e os intervalos entre
entradas da cena por thread.

As chamadas medem despacho sob instrumentação. Um frame pode entrar
na cena sem despachar objetos, e o último frame pode ficar incompleto
se a coleta terminar durante o despacho. Os timestamps ainda não
cobrem o flip do buffer ou a latência de exibição. Para concluir a
coleta, compare os intervalos, a máscara e as sequências dos dois
presets e preserve os relatórios privados.

### V01/V31 — reação no roteiro 34

No Windows, abra um duelo com as vozes ativas e provoque uma reação
de dano ou de invocação. Deixe a reação terminar antes do fim da coleta.
Use outro arquivo para uma reação com segunda fala ou com áudio
desabilitado.

```powershell
python scripts/pending/34-attack-draw-runtime.py --mode reaction --pid <PID> --exe ..\passion\joey_pc.exe --output scratch/visual-review/reaction-damage.json
```

O relatório registra o contador e os dois IDs de som antes/depois do
Draw, o estado ativo e o sinalizador de superfície temporária. Vincula
a chamada de `005203C0` no retorno `00416D4C` ao mesmo Draw e registra
os quadriláteros e os formatos cuja vtable é reconhecida. Uma coleta
completa inclui Update inicial, Draw que desativa o mesmo objeto e
passagem base nesse Draw, sem perda de eventos. Confronte os pontos
com `bustUpReactionGeometry` no inspetor do DAT e a sequência com
frames equivalentes. O modo aguarda a execução no Windows.

### V16 — saída do resultado no roteiro 34

No Windows, inicie a coleta antes de sair da tela de resultado. Em
partidas com vários duelos, use um arquivo por saída. O ordinal
neste modo é a entrada em `00447000`, a rotina da cena de resultado.
O traço registra fase/subfase, contador antes/depois de `00444490`,
desativação e argumentos de desenho em `0044451F`. Saída 0 exige
uma sequência de 16 Draws, das quais 15 pedem o blit esperado,
sem descarte de eventos. O modo aguarda execução no Windows.

```powershell
python scripts/pending/34-attack-draw-runtime.py --mode result --pid <PID> --exe ..\passion\joey_pc.exe --output scratch/visual-review/result-fade.json
```

### V12 — produtor e execução de B1 no roteiro 34

Em Windows, use Frida na venv e um duelo aberto no original. Inicie
a coleta antes de colocar Jinzo em campo com uma armadilha contínua
já revelada. Repita em arquivo separado com a armadilha baixada,
como controle. A saída esperada para o controle sem B1 é 1.

```powershell
python scripts/pending/34-attack-draw-runtime.py --mode disabled-chain --pid <PID> --exe ..\passion\joey_pc.exe --output scratch/visual-review/disabled-chain-jinzo.json
```

O relatório preserva lado, zona, argumentos e endereço de retorno
do pedido B1 em `005B9170`. Para cada execução de `00408730` registra
os argumentos do dispatcher, o subestado e as chamadas observadas
ao setter do bit 5 e ao construtor do indicador. Saída 0 exige um
pedido da varredura `0059CCF0` seguido de uma execução compatível
no mesmo lado e zona, com o descritor esperado e sem perda de eventos.
Os eventos são vinculados por ordem e argumentos. Pedidos
idênticos no mesmo trecho podem ter mais de uma correspondência.
As saídas ficam em `scratch/` e a coleta aguarda Windows.

## V31 — revisão gráfica de reaction07 (55, aguardando DAT local)

**Ambiente:** Fedora/Linux com display (ou Xvfb) ou Windows, .NET 10 e
`data.dat` original. Não é nova coleta Ghidra. O renderer isolado já
está implementado. O objetivo é validar parser, geometria, camadas e
lifecycle do cliente antes de ligar a reação a eventos reais.

```bash
bash scripts/pending/55-reaction07-review.sh /path/to/data.dat
bash scripts/pending/55-reaction07-review.sh /path/to/data.dat --reaction-wait-audio
```

Em Windows, o comando equivalente é
`dotnet run --project src/Joey.App -- C:\\passion\\data.dat --review-reaction=07 --no-audio`.

F12 captura PNG/JSON em `scratch/visual-review/` no estado inicial;
F7 avança um Update/Draw. Capture os estados 8, 16, 24 e 32 (variantes
ou omissões dos grupos), os contadores 248 e 256 e o reset F8. No modo
com espera, ultrapasse 256, confirme que o objeto continua ativo, F5
sinaliza conclusão simulada e F7 encerra. Para comparação pareada
com o original, alinhe contador e estado de cena do roteiro 44; a
captura do cliente representa a fronteira pós-step. Não compare
estados divergentes nem assuma transparência/pixels iguais.

**Saída privada:** pares PNG/JSON, log de execução, observações da
composição e de discrepâncias. **Encerramento:** DAT aceito, coordenadas,
variantes, predicado e retorno ao tabuleiro conferidos; composição
comparada ou divergências registradas. Não incluir recursos do jogo
no Git. Roteiros 43–45 e 51 já executados no original; não repeti-los
sem uma pergunta específica. Áudio físico e integração A03 separados.
[Contrato V31.6](../../docs/research/native-joey-reaction-audio-2026-11-24.md#v316--renderer-isolado-de-reaction07).

## V22 — revisão gráfica de Break (56, aguardando DAT e comparação)

**Ambiente:** Fedora/Linux ou Windows, .NET 10, display e DAT local.
**Pergunta:** verificar partículas, orientação, fragmentos e fronteiras
4/16 updates do review de campo `00450FD0`, antes do vínculo A03.

```bash
bash scripts/pending/56-break-field-review.sh /path/to/data.dat 250,8080 0
bash scripts/pending/56-break-field-review.sh /path/to/data.dat 0,4080 0
```

Em Windows: `dotnet run --project src/Joey.App -- C:\\passion\\data.dat --review-break=250,8080 --review-perspective=0 --no-audio`.
ID 250 é exemplo, use um resource ID válido; para ocultas usar ID 0.
F12 nos contadores 0/16/48/64/128/240/256; F7 avança, F8 reinicia.
Repetir nos dois lados, ataque e defesa, e com slot FF de snapshot.
Os contadores 64..240 devem manter o efeito visível, mas sem
`blocksRelease`. A captura final deve mostrar somente o tabuleiro.

**Saída privada:** PNG/JSON em scratch/visual-review, log e comparação
com original quando disponível. Sem exportar pixels para Git.
**Encerramento:** aceitar DAT real, confirmar geometria e fronteiras,
registrar divergências de +AC/+C e compositor. O review anterior no
PR #186 aceitou DAT real; este roteiro valida a versão reconciliada e
comparação gráfica, não repete Ghidra 29. Integração A03 separada.
[Contrato](../../docs/research/native-break-field-review-2026-11-25.md).

### V22.3 — complemento vivo do roteiro 56

Com a mesma versão do cliente, observar em duelo real/console:
Man-Eater Bug ao ser atacado (efeito vs baixa de combate), Raigeki/Dark
Hole (múltiplos alvos), MST sobre S/T Set e FIELD, tributo normal,
ritual, Polymerization e Fusion Gate. Confirmar que Break se libera
após quatro updates, desenha a cauda por mais doze mesmo com a ação
seguinte, oculta identidade de Set e não dispara para material ou
limpeza dependente. Comparar com o EXE, registrar diferenças, em
especial 7A e flags excepcionais, sem assumir equivalência universal.
Saída privada: log e capturas de estados equivalentes; nenhuma coleta
estática nova obrigatória.

## V26 — identificar produtor de exclusão (34, modo exclusion)

No original, executar um banimento de monstro no campo (Nobleman of
Crossout como primeiro cenário; Dimensional Warrior como comparação),
com tempo suficiente para terminar a apresentação. Repetir separadamente
para materiais de Fusion Gate e banimento a partir do cemitério como
controles. Usar nomes diferentes, saída privada em scratch/.

```powershell
python scripts/pending/34-attack-draw-runtime.py --mode exclusion --pid <PID> --exe ..\\passion\\joey_pc.exe --seconds 45 --output scratch/visual-review/exclusion-nobleman.json
```

Saída 0: construtor observado com retorno dentro da imagem, t64,
24 Draws até t256, nenhuma perda, nenhuma consulta de gate verdadeira.
Saída 1 pode conter transferência mas nenhum objeto de exclusão; NÃO
converter isso em prova de inexistência. Comparar returnRva, argumentos
de 79/7A/7C/8E/8F/95 e descritores de transferência; contexto não é
vínculo causal. Não disparar V26 apenas pelo destino de movimento.
O evento `exclusion-zone-arrival` registra o receptor e o endereço de
retorno de `0043D440`. O resumo `zoneArrivalCalls` conta apenas o retorno
`0041E82C` para a zona `0x0F`. Compare a chegada e a criação no mesmo
thread e frame antes de atribuir o disparo à movimentação.
No cliente, `--review-exclusion=250,8080` (ID real) ou `0,0080`
(oculto), F5 reprodução/pausa, F7/F9 passo, F8 reset, F12 captura.
Confirma geometria e predicado; não substitui a captura original.

## V32 — contrato da viagem de equipamento (57)

```bash
bash scripts/pending/57-equipment-travel-contract.sh
```
Confirme vtable 005DB3E4 slots +4/+8/+C e índice/stub de B2.
Saída scratch/ghidra-equipment-travel-contract.zip (privada).
No duelo compare equipamento comum da mão/Set, destino próprio e
adversário. Verificar crescimento estático 0..128, viagem 128..192,
Draw terminal, momento de alteração ATK/DEF e som 1000000A.
A coleta não bloqueia o desenvolvimento da apresentação já recuperada.

## V24 — tabelas de seletores e produtores de restrição (58)

```bash
bash scripts/pending/58-affect-link-selectors.sh
```
Apenas Ghidra, sem executar o jogo. Exporta 72 bytes em 0040E918
(três tabelas de seis destinos), primário/candidate/target_preparation
 de Spellbinding Circle e callbacks de Dark-Eyes Illusionist,
Invitation, Thousand-Eyes Restrict e Dragon Capture Jar. Resolver
kind de vínculo efetivamente emitido e compará-lo aos destinos de
0040E594/627/7A6. Não coletar novamente toda 0040E260. Importar
bundle cumulativo quando recebido; nenhum recurso binário no Git.

## V31 — invocação característica de Red-Eyes (74)

A primeira tentativa em Proton 9.0 deixou a demonstração automática
rodar por 95 segundos. Nenhuma colocação de 0x51 ou 0x2E8 chegou ao
seletor. A próxima coleta precisa de um replay que contenha uma
invocação característica do lado do Joey. Em Proton com GDB/Python,
anexe à cópia isolada antes de carregar esse replay. Repita com
`-nosound` para separar o limiar de consulta da duração da fala.

```bash
timeout --signal=INT 95s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/pending/74-wine-signature-summon.gdb > scratch/signature-summon.log 2>&1
```

A linha `signature-route` registra lado, tipo, orientação e modo.
O roteiro também registra a variante do construtor em `00414A20`
e os contadores dos pares Update/Draw até `0x10060`. A coleta fica
em aberto até uma amostra positiva confirmar a criação e a liberação.
Compare com o controle no lado do jogador e guarde PNGs dos
quadros de máscara e do letreiro em `scratch/`.
[Evidência estática](../../docs/research/native-joey-reaction-audio-2026-11-24.md#v3116--invocação-característica).
