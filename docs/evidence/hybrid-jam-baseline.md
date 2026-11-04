# Jam Breeding Machine — baseline real sem substituição

Data de captura: 2026-11-04 UTC. Evidência obtida de arquivo ZIP enviado pelo operador. **Os arquivos brutos de RAM, replay e decks não são versionados**, pois podem revelar informações locais e cartas ocultas.

## Ambiente e qualidade da captura

- EXE original: SHA256 \`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de\`.
- JoeyTrace em modo \`--card-events --verbose\`, leitura externa de RAM, sem patch nem hook de comandos nativos.
- Duração de 906,5 s (15m06,5s), 9.065 leituras, 605 lotes de alterações, **zero erros de leitura**.
- Um reset heurístico do duelo foi detectado no início; as observações principais estão no segmento 1.
- Replay YDR anexado: **1.358 comandos de 8 bytes**, 61 inícios de turno inferidos. O arquivo não possui timestamps de comandos, logo não é possível alinhá-los diretamente com os eventos de RAM.
- O deck do lado 1 contém duas cópias do ID de recurso **1426** (Jam Breeding Machine).

## Observações da carta e dos monstros gerados

| Instante relativo | Observação no lado 1 |
|---:|---|
| 165,313 s | Jam Breeding Machine (índice interno 457) aparece no campo, em slot \`other_field=8\` e ocorrência 22 |
| 186,094 s | Primeiro monstro de índice interno 1114 entra na zona de monstro 2, ocorrência 24 |
| 197,391 s | Marcador #1: operador anota 'primeiro token criado' (11,297 s após a entrada já observada) |
| 220,907 s | Outro índice 1114 surge na zona 3, ocorrência 26 |
| 239,000 s | Outro índice 1114 surge na zona 4, ocorrência 29; os cinco slots de monstro ficam ocupados pela primeira vez |
| 827,000 s | Um novo índice 1114 aparece na zona 3, ocorrência 96 |
| 859,203 s | Marcador #2: operador anota que Jam Breeding Machine não cria mais tokens porque as cinco zonas de monstro estão cheias |
| 880,094 s | Última mudança de estado registrada; Jam Breeding Machine permanece no campo |

Ao longo da sessão, o coletor observou **20 aparições distintas** do índice de monstro **1114**, cada uma com palavra de ocorrência diferente. Alguns desses monstros desaparecem durante a partida e novos surgem após ficarem zonas livres. Há uma forte associação contextual com os tokens descritos pelo operador, mas **o trace externo, sozinho, não prova qual função ou evento acionou cada criação**.

No marcador #2 e nos snapshots posteriores, o lado 1 tinha as cinco zonas ocupadas. As três zonas com índice 1114 tinham ocorrências 80, 96 e 92; as outras zonas continham índices 261 e 471. O último índice 1114 registrado entrou antes do marcador e não foi observada outra criação depois. É compatível com a descrição do operador, mas não há marcação de Standby Phase nem chamadas ao callback principal para provar a regra de limitação de capacidade.

## Interpretação e limites

O conjunto é uma boa referência **observacional** para testes A/B. Não confundir 20 novas ocorrências de índice 1114 com 20 ativações de callback: nenhuma chamada de callback foi medida nesta captura. O slot \`other_field=8\` não tem classificação semântica definitiva no parser. As amostras são não atômicas, com intervalo de ~100 ms; estados intermediários rápidos podem não aparecer. O YDR armazena comandos, não uma linha do tempo sincronizada da RAM.

## Próxima comparação

O teste seguinte implementa uma substituição experimental somente do **ponteiro primário** do registro de recurso 1426, cujo alvo original \`0x00583F30\` contém \`xor ax,ax; ret\`. A substituição também retorna zero, mas incrementa um contador; sua função é **provar ou descartar que o callback primário é acionado** durante um duelo com tokens. Se o contador continuar zero, isso é informação sobre o escopo do callback e **não** um erro da captura.

Usar uma nova sessão JoeyTrace com as mesmas opções para medir a criação/ausência de tokens e salvar novo YDR. Comparar os arquivos YDR com \`compare_ydr.py\` como evidência adicional, lembrando que RNG e decisões da IA podem gerar diferenças legítimas. Não usar reprodução do YDR antigo como substituto da partida normal sem confirmar que os callbacks são executados.
