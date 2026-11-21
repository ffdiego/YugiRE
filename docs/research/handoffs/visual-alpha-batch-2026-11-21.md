# Handoff para novo chat — YugiRE (21/11/2026)

## Referência atual

Repositório: https://github.com/ffdiego/YugiRE, branch main.
Leia primeiro AGENTS.md, PLANS.md e:
- docs/architecture/native-renderer-inventory.md
- docs/architecture/native-card-rendering.md
- docs/architecture/native-visual-contract.md
- docs/architecture/visual-fidelity-goal.md
- docs/research/native-action-scheduler.md
- docs/architecture/VISUAL_COVERAGE.md

Objetivo: Alpha jogável em C#/.NET + raylib, com fidelidade visual em pixels,
assets originais carregados do data.dat em runtime (cache permitido), contadores
e trajetórias nativos quando conhecidos e UI de debug desligável. Não é
necessária equivalência de código/assembly do executável. Joey.Core deve ser
independente de delays e do renderizador.

O inventário de renderização distingue recursos já usados pelo cliente dos
efeitos mapeados mas pendentes. Confirmar o HEAD da main antes de programar:
o Codex realizou várias alterações recentes e alguns checkboxes de PLANS.md
podem estar defasados. O renderer de explosão já foi portado para sete YGAs;
a espada usa variantes de contato e impacto; o painel de fases e distribuição
das mãos também receberam alterações recentes. Não repetir essas implementações.

## Exportar um lote grande de funções nativas no Fedora

Em ~/prj/joeyre/yugire:

    git pull --ff-only
    python3 scripts/export_ghidra_visual_bundle.py --list-only
    python3 scripts/export_ghidra_visual_bundle.py --retry-missing

O gerador consulta de forma sequencial os entrypoints Update/Draw presentes
no inventário e pontos extras de scheduler/transformação, divididos em grupos
de até 10 funções para respeitar o limite de 12 da QueryArchive.java.

Resultado: scratch/ghidra-visual-batch.zip. O ZIP inclui manifest.json,
relatórios decompilados por grupo, logs e endereços inconclusivos. O programa
não tenta executar o joey_pc.exe nem inclui EXE, DAT, saves ou assets. É uma
extração estática. Não versionar o ZIP no repositório; anexá-lo ao próximo
chat apenas se desejar analisar essas funções em detalhe.

Opcional: --max-functions 60 para limitar a primeira rodada, --batch-size 8
para lotes menores, ou --output scratch/lote-personalizado.zip.

Ao receber o ZIP, analisar primeiro manifest.json e o inventário nativo,
cruzando evidências com código atual. Decompilações geradas por Ghidra podem
ter protótipos/argumentos falsos; conferir assembly em pontos críticos.
Não repetir análises já documentadas sem necessidade.

## Badges

README exibe apenas a porcentagem total de cartas jogáveis da trilogia e
porcentagem de rotinas Draw nativas com implementação C# (inclusive Partial).
A badge visual **não** indica fidelidade visual validada. O relatório
docs/architecture/VISUAL_COVERAGE.md e o manifest
docs/architecture/visual-function-coverage.json documentam exatamente
quais funções contam e por quê. Ao implementar novo renderer, atualizar
inventário e manifest e rodar scripts/publish_visual_coverage.py.

## Primeiras prioridades úteis

1. Confirmar que a Alpha joga um duelo completo com UI e CPU e observar a
   regressão das animações por captura comparada ao original.
2. Selecionar 1-2 famílias ainda Pending no inventário (ex.: set/summon e
   LP/dano) e recuperar sprites, ângulos, sequência de update/draw e gates.
3. Aproximar o cliente da renderização fiel para esses efeitos, com testes
   determinísticos de frames/variantes e sem alterar regras do Core.

Descrever no próximo chat a mudança concreta desejada e anexar o ZIP
se houver uma pergunta de RE. Evitar prometer porcentagem de fidelidade
em pixels sem comparações de capturas equivalentes.
