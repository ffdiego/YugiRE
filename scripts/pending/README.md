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

A execução registrou 129 falhas no cabeçalho RIFF. A correção da variante PCM em #134 aceita 106 de 131 entradas no DAT local. Restam 24 entradas com frame estéreo incompleto e uma acima do limite. A captura existente está no SQLite cumulativo.

Próxima ação em Windows com o cliente atualizado: validar a decodificação com `-SkipPlayback` e executar escuta dos exemplos aceitos. Registre os resultados no relatório privado do roteiro. [Diagnóstico](../../docs/research/native-joey-reaction-audio-2026-11-24.md).

```powershell
powershell -ExecutionPolicy Bypass -File scripts/pending/28-voice-audio-windows.ps1 -SkipPlayback
powershell -ExecutionPolicy Bypass -File scripts/pending/28-voice-audio-windows.ps1 -Samples joey_01.wav
```

## V04 — 32-native-sword-angle-ghidra.sh (aguardando Fedora)

SQLite v3 verificado: os três doubles 005DA708/005DA728/005DA730
não estão cobertos por nenhum bloco de visual_evidence_memory;
005C3E40 só tem decompilação identificada como __ftol, sem assembly.
Não repetir 21 nem a geometria dos blitters já recuperada.

    bash scripts/pending/32-native-sword-angle-ghidra.sh

Ambiente: Fedora/Ghidra local, projeto e EXE atestados pelo exportador.
Saída privada: scratch/ghidra-native-sword-angle-contract.zip.
Critério: três doubles de 8 bytes (blocos 8+16), helper completo,
identificar modo de arredondamento/restauração de control word e importar
em cópia cumulativa com fingerprint, integridade e auditoria.
Se a instrução encaminhar a outro helper, consultar SQLite antes de ampliar.
Não inferir constantes pelo nome ou por atan2. [Contrato V04](../../docs/research/native-attack-life-routing-2026-11-24.md).
