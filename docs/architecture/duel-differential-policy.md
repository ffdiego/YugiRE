# Validação diferencial de duelos

Mesmo estado inicial (decks em ordem, LP, fase, ocorrências e contexto), mesmas decisões legais e resultados aleatórios controlados quando necessários. Executar regras independentemente. YDR de comandos resolvidos é referência de ações, não prova de regras. Capturar decisões e snapshots correlacionados. Normalizar opcodes/argumentos sem perder ordem e granularidade; decomposição 1:N somente com contrato explícito. Comparar estado após ações e limites relevantes (incluindo atributos e restrições contínuas), reportar primeira divergência e prefixo comparável. Gaps ou contexto insuficiente são inconclusivos. Não promover Verified por teste sintético.

Joey.Cards fornece definições e planos; Core executa somente na fila tipada. Primeira fatia: definições fixas de LP e compra, adaptadores preservam API durante migração. C12 retira executores/fallbacks antigos. AI usa decisões legais comuns e snapshots pré-decisão.

Checkpoints parciais podem hidratar turno e fase conhecidos e cartas previamente Set, sem usar ações antigas para alcançar o estado. Não inferir campos desconhecidos nem tratar o checkpoint como save/load nativo. O caminho efetivo é sempre a fila tipada; manter entradas incompletas como inconclusivas.
