# scripts/pending — coletas de evidência nativa em aberto

Os roteiros executados estão em [completed](../completed/README.md) e os verificadores reutilizáveis estão em [regression](../regression/README.md). Preserve os números e distinga os roteiros 16 e 17 pelo nome completo. A referência privada das três trilhas é o [SQLite cumulativo](../../docs/research/joey-knowledge-sqlite.md).

## Protocolo de novas coletas — todas as trilhas

Fila única para ACTIONS (`A##`), CARDS (`C##`) e VISUALS (`V##`), incluindo Windows (execução/Frida/DAT) e Fedora (Ghidra/projeto local). Preservar números e histórico dos scripts 01–10; novos roteiros recebem o próximo número livre, nome descritivo e ID(s) da trilha na ficha. Cada novo roteiro deve especificar hipótese/pergunta, endereços quando aplicáveis, ambiente e dependências, comando reproduzível, artefato privado e critério de encerramento. Antes de preparar coleta, consultar o SQLite mais recente e os bundles já existentes. Enquanto a coleta estiver bloqueada, avançar em outra tarefa.
O exportador valida SHA-256, usa Ghidra -readOnly com
rollback e grava sob scratch/. Depois de consumir um bundle,
registrar conclusões em docs/research e mover o roteiro para [completed](../completed/README.md) ou
[regression](../regression/README.md). Preferir integrar evidências recuperáveis em nova cópia cumulativa do SQLite, com proveniência, fingerprint, auditoria e verificação de integridade; usar ou ampliar importadores existentes conforme o formato. Capturas/frames incompatíveis permanecem em artefatos privados referenciados. Registrar snapshot de origem e destino, importação concluída ou pendente, sem confundir coleta com implementação. Consultar [guia SQLite](../../docs/research/joey-knowledge-sqlite.md). Novas pendências devem corresponder a evidências específicas, não reexecutar coletas completas.

Ficha mínima para novos scripts: `número | ID da trilha | ambiente | pré-requisitos | comando | saída | pergunta | estado da coleta/importação | referência de conclusão`. Não executar automaticamente.

## V18 — smoke de áudio Windows (28)

A execução anterior registrou 129 falhas no cabeçalho RIFF. Com o adaptador para a variante PCM e o limite de inspeção para entradas armazenadas sem compressão, o cliente atual validou as 131 entradas no Linux. A captura Windows anterior está no SQLite cumulativo.

Próxima ação em Windows com o cliente atualizado: validar a inspeção com `-SkipPlayback`, ouvir as amostras do roteiro e observar a troca de música do menu para o duelo no App. Registre os resultados no relatório privado do roteiro. [Diagnóstico](../../docs/research/native-joey-reaction-audio-2026-11-24.md).

```powershell
powershell -ExecutionPolicy Bypass -File scripts/pending/28-voice-audio-windows.ps1 -SkipPlayback
powershell -ExecutionPolicy Bypass -File scripts/pending/28-voice-audio-windows.ps1
```

## V01/V04/V11/V17/V31 — 34-attack-draw-runtime.py (aguardando Windows)

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

### V01/V11 — fundo da seleção no roteiro 34

No mesmo ambiente, abra uma seleção de cemitério, deck ou fusão.
Deixe a janela chegar ao estado estável durante a captura. Grave
um relatório por família e por variante com e sem scroll.

```powershell
python scripts/pending/34-attack-draw-runtime.py --mode selection --pid <PID> --exe ..\passion\joey_pc.exe --output scratch/visual-review/selection-grave.json
```

O ponto `0043B47E` registra a chamada `+4C`, as vtables de
destino e origem, o destino virtual resolvido e chave de manifesto
em EDX. Para a origem `005DC6EC`, inclui o formato, o flag
`+78`, a chave empacotada e os parâmetros que selecionam a
chave por coordenada ou COLORREF. A captura limita o acesso aos
campos dessa vtable e grava somente metadados. Saída 0 significa
que houve pelo menos um pedido de fundo estável sem eventos
perdidos. Para resolver a lacuna, confronte o alvo e os campos da
chave com a imagem de referência e as variantes do DAT. O modo
ainda aguarda execução no Windows.

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
