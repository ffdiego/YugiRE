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

## V01/V04 — 34-attack-draw-runtime.py (aguardando Windows)

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
