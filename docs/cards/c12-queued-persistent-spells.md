# C12.1 — Ativação de fontes persistentes (P036, Infinite Cards)

Migração de regras já jogáveis; nenhum aumento de Done/Verified.
P036: 329–334, 1069, 1125–1130. Inclui ativação de Fusion Gate
1547, mas não migra a fusão em si. Infinite Cards 1427 (0x593).
Preservar semântica e evidência já documentadas em FieldSpellRules
e p036-field-spells-sandbox.md, não inventar timing nativo.

FIELD Set já emitia SetCommand. Agora PrepareFieldSpellActivationCommand
reserva substituições por ocorrência, emite SendToGraveyardCommand
para cada FIELD substituído e ActivateFieldSpellCommand após gates.
Hand ocupa FIELD livre/recém-liberado, Set vira a mesma ocorrência.
Preserva o FIELD Set adversário; ativa um campo global e substitui
o próprio Set se ativado da mão. Sem gasto ao GY da fonte nova.
Cada substituição revalida fonte e lista restante, sem trocar alvos.
Estatísticas continuam derivadas pelo cálculo existente.

Infinite Cards usa ActivateInfiniteCardsCommand para colocar/virar
a fonte na zona S/T; HandLimitDisabled deriva do estado de ambos
os lados, sem flag persistente nova. Sem alterar a regra do End Turn.

Console: activate hand INDEX, activate field (FIELD próprio Set),
activate set INDEX para Infinite Cards. API explícita para FIELD.
Testes: 14 IDs × ambos lados × Hand/Set, substituição e gate, Set
adversário preservado, Set próprio substituído, ocorrência stale,
Infinite Cards em ambos lados/Hand/Set. Rota legada ainda disponível
para clientes não migrados; UI de mão/Set e ativação CPU Fusion Gate integradas em modo fila no PR de reconciliação. Fusão e comparação nativa pendentes.
Não confundir executor opt-in com eliminação de todas as rotas legadas.

## Próximas fatias de C12

1. Produtores restantes de ativação e escolhas, em acordo com ACTIONS.
2. Fusion: seleção/reserva por ocorrência, material e colocação separados; Fusion Gate e Polymerization.
3. Gatilhos e respostas de armadilhas, FLIP de batalha e buscas dependentes da fila A06.
4. Cobrir efeitos Done de posição, cemitério, combate e condições especiais ainda legados; inventariar por rota antes de remover atalhos.

Medir separadamente executor, produtor e eliminação do bypass; não
promover carta a Verified por migração ou CI sintético.
