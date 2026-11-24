# C12 — Migração das magias fixas de LP

Recorte vertical: cinco P045 (342,343,344,345,1007), quatro P041
(338,339,340,341) e Rain of Mercy 1315. Todos já Done no manifesto
legado. Reusar LifePointSpellRules.ResolveDemo, não duplicar valores.
P040/Mooyan Curry 337 requer escolha de lado e não aparece no índice
dos 771 packs; não ampliar o escopo por callback compartilhado.

TryQueueLifePointSpell admite uma fonte da mão ou S/T setada, com
ocorrência e lado, sem modificar estado. ActivateLifePointSpellCommand
revalida antes de emitir SendToGraveyardCommand e zero/um comando
Damage/Heal por lado conforme a regra. A ordem do sandbox é gastar
a carta, depois LP próprio, depois LP oponente. Rain of Mercy produz
dois HealCommand independentes. Cada mutação espera sua apresentação;
não há nova fila ou chamada de Play/ActivateSetSpell pelo executor.

O envio de S/T ao GY está limitado a magias fixas de LP setadas,
sem vínculo; não generaliza outras cartas. A fila não implementa
respostas/negação, nem movimentação visual de ativação. O adaptador
normal da UI/CPU continua legado até migração ACTIONS; este recorte
é opt-in headless. Não promover novos IDs. Testes de ambos os lados
e zonas, gates, identidade obsoleta e LP letal, ainda aguardando CI.
