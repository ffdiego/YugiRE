# Eventos, janelas de resposta e correntes: limites da máquina de estados

Esta nota é o desenho **proposto**, não a implementação integral de
correntes. O sandbox atual cobre uma janela única de resposta à
Normal/Flip Summon (P196/P038) e outra à declaração de ataque
(P201/Mirror Force), mais resoluções de efeitos pós-cemitério. Isso já
prepara pontos explícitos de pausa, mas **não é um Duel Engine com chains**.

## Por que não basta apenas ordenar estados?

Ordem de fase de duelo e ordem de resolução de efeitos são conceitos
diferentes. Exemplo com duas cartas encadeadas:

1. Um evento torna o primeiro efeito ativável. O jogador declara a
   ativação e paga custo/seleciona alvos válidos.
2. O adversário pode responder, adicionando outro elo (Chain Link).
3. Após passes alternados, a corrente fecha e resolve **em ordem
   inversa à ativação** (último elo primeiro).
4. Depois da resolução, cartas deixam/entram em zonas, efeitos
   obrigatórios ou opcionais podem ser disparados, e as próximas
   janelas de resposta/decisão são determinadas.
5. Ataque e turnos continuam se o estado ainda permitir, sem declarar
   novamente um evento que já foi consumido.

A condição de ativação é testada no evento apropriado. Um efeito
encadeado pode mudar o campo entre ativação e resolução. Portanto,
validar alvos, custos, negação e identidade da ocorrência apenas no
final do elo é insuficiente.

## Interfaces sugeridas

```csharp
// Esboço intencional, NÃO API já presente no Joey.Core.
record DuelEvent(EventKind Kind, long Sequence, object Payload);
record ChainLink(Card Source, int Controller, EffectContext Context,
                 IReadOnlyList<CardOccurrence> LockedTargets);
record PendingDecision(int Player, DecisionKind Kind, DuelEvent Source);

enum ChainStage { EventWindow, Building, Resolving, CheckingTriggers, Closed }
```

- `DuelEvent`: declarado de modo imutável, inclui ocorrência, lado,
  zona e provenance (Normal/Flip/Special Summon, ataque, destruição
  em batalha/efeito, envio ao GY).
- `PendingDecision`: operação exigida do agente/UI; um jogador pode
  adicionar elo ou passar. Janelas de Trap e escolhas de card search
  não são intercambiáveis.
- `ChainLink`: captura custos/targets/identidade na **ativação**;
  `Resolve` consulta estado vivo e decide se os efeitos ainda são
  aplicáveis, independentemente de possíveis negações.
- `ChainStage.Resolving`: retira elos em LIFO. Durante a resolução
  não oferece uma nova janela comum, mas registra eventos resultantes
  para processamento posterior.
- `CheckingTriggers`: organiza efeitos disparados depois da corrente,
  incluindo prioridade/ordem de efeitos simultâneos quando mapeada.
- `ActionLog`: cada evento, resposta, elo e mudança de estado deve
  produzir dados determinísticos para futuros JSONL/replays e
  diferenciais com o Joey original.

## Implementação incremental segura

1. **Feito no sandbox:** P196/P038 criaram
   `PendingSummonResponse`; P201 criou `PendingAttackResponse`
   **antes do dano**, com Pass e continuação da mesma ocorrência.
2. **Próximo passo proposto:** extrair a interface compartilhada
   `ResponseWindow` e substituir os prompts separados sem mudar a
   semântica dos testes existentes.
3. Introduzir o **registro de Chain Links** com duas respostas
   encadeáveis de teste e resolução LIFO determinística.
4. Implementar negação, custos, ações simultâneas, timing de eventos
   pós-corrente e seleção de alvos em outras janelas.
5. Finalmente comparar episódios específicos com o executável
   original, sem promover comportamento não observado para Verified.

A meta é evitar reescrever a engine inteira: primeiro contratos de
eventos, depois um resolvedor LIFO testado. As restrições de decisão
e os detalhes de prioridade histórica do Joey permanecem pendentes,
apesar de mecanismos semelhantes constarem nas regras atuais de TCG.
