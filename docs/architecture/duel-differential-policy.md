# Validação diferencial de duelos

Mesmo estado inicial (decks em ordem, LP, fase, ocorrências e contexto), mesmas decisões legais e resultados aleatórios controlados quando necessários. Executar regras independentemente. YDR de comandos resolvidos é referência de ações, não prova de regras. Capturar decisões e snapshots correlacionados. Normalizar opcodes/argumentos sem perder ordem e granularidade; decomposição 1:N somente com contrato explícito. Comparar estado após ações e limites relevantes (incluindo atributos e restrições contínuas), reportar primeira divergência e prefixo comparável. Gaps ou contexto insuficiente são inconclusivos. Não promover Verified por teste sintético.

Joey.Cards fornece definições e planos; Core executa somente na fila tipada. Primeira fatia: definições fixas de LP e compra, adaptadores preservam API durante migração. C12 retira executores/fallbacks antigos. AI usa decisões legais comuns e snapshots pré-decisão.

Checkpoints parciais podem hidratar turno e fase conhecidos e cartas previamente Set, sem usar ações antigas para alcançar o estado. Não inferir campos desconhecidos nem tratar o checkpoint como save/load nativo. O caminho efetivo é sempre a fila tipada; manter entradas incompletas como inconclusivas.

## Checkpoints do laboratório

```sh
dotnet run --project src/Joey.Lab -- duel-console --fixture roteiro.txt \
  --trace-output=trace.json --checkpoint-output=checkpoints.json
```

O documento `yugire-duel-checkpoints-v1` começa com o estado inicial.
Cada checkpoint seguinte identifica a linha executada e a última
sequência de comando executada até aquele limite. Uma linha de
leitura pode repetir a sequência anterior. Uma escolha bloqueante
também pode delimitar um checkpoint e fica marcada no campo
`hasPendingChoice`. Um roteiro interrompido por falha ou escolha
sem resposta conserva o prefixo com `complete: false`.

Os lados incluem LP, zonas em ordem, posições e atributos efetivos
de monstros revelados. IDs de ocorrência são locais ao duelo e
precisam de associação ao comparar engines. O export contém mão e
deck de ambos os lados para análise privada. O arquivo representa
o C# e ainda precisa de decisões, contexto e checkpoints nativos
correlacionados para uma comparação diferencial.

## Comparação de checkpoints pareados

```sh
python -m yugilab.compare_duel_checkpoints captura.jsonl checkpoints.json \
  --pair 120:initial --pair 124:3 --pair 130:5
```

Cada par informa a sequência do JSONL nativo com `--trace-state` e a
linha do roteiro C#. O primeiro par aponta para o estado inicial do
roteiro. Os demais pares devem seguir a ordem das duas fontes e
pertencer ao mesmo duelo. Quem monta os pares precisa confirmar
decisões, estado e momento equivalentes. O estado do JSONL é lido na
entrada do produtor do comando, antes de seu enfileiramento, e pode
perder estados transitórios.

A projeção compara LP, ordem dos índices internos na mão e contagens
de deck e cemitério por lado. O relatório inclui o hash do JSONL, o
prefixo pareado, as lacunas e a primeira diferença observada. Uma
diferença após uma lacuna fica visível com `afterGap: true`. Códigos de
saída 0 para concordância na projeção solicitada, 1 para diferença no
prefixo, 2 para entrada inválida e 3 para resultado inconclusivo. Ainda
faltam campos visíveis do tabuleiro, identidade das ocorrências e uma
captura equivalente com decisões correlacionadas.
