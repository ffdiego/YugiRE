# P025 — três equipamentos no sandbox

Escopo fechado aos recursos **300, 307 e 301**. Legendary Sword é regressão;
Beast Fangs e Sword of Dark Destruction passam a ter vínculo e contribuição no
demo. O avaliador aritmético já continha os três deltas. Não implementamos os
outros 38 registros nem ampliamos FromResolvedContext para novos monstros.

## Contrato e evidência

EquipmentTargetContext separa lado/posição, índice interno, recurso, face,
tipo dinâmico, atributo dinâmico e resultado da guarda genérica. Campos são
nullable por padrão. EquipmentRules.Eligibility devolve Unsupported se qualquer
entrada necessária estiver ausente ou o recurso estiver fora da whitelist;
com contexto completo, devolve true/false. Zero de índice conhecido é posição
vazia e false, diferente de índice ausente. Nenhuma normalização +2000.
O contrato recebe o resultado da guarda; não implementa 0056C510.

Os predicados são os ramos individuais estáticos de 00579CE0:
300 requer tipo15, 307 tipo11, 301 atributo2. Ambos os lados são possíveis.
A contribuição vem exclusivamente de NativeAiAttributes.EquipmentDelta,
que reavalia os mesmos predicados e retorna 300/300, 300/300 e 400/−200.
Fontes e linhas exatas: [investigação](p025-preliminary.md),
[Legendary Sword](legendary-sword.md) e [limpeza](equipment-cleanup.md).
Nenhuma execução nativa independente foi observada nesta implementação.

## Política do demo e limites

- O demo normal é um sandbox sem efeitos de remapeamento, supressão, proteção
  ou bloqueios globais. Seu adaptador declara tipo=ClassCode, atributo=Properties>>29
  e guarda genérica satisfeita. Isso é política de contexto, não descoberta do
  estado nativo. FromPartialObservation deixa contexto de equipamento desconhecido;
  o parâmetro equipmentContextResolved:true é uma declaração explícita do chamador
  para usar essa mesma política fechada, destinada aos testes de escopo.
- A seleção da interface cobre somente a whitelist. Equip lê novamente a mão,
  alvo, face, propriedades e zona livre na hora da resolução. Toda validação
  antecede alterações da mão, zona ou lista de vínculos. Não há fila ou cadeia.
  Ativação com seleção a partir de equipamento já setado continua fora do escopo.
- Equipment conserva dono, zona fonte, lado/zona alvo e referência ao Card alvo.
  A referência é proteção de identidade do sandbox, não a chave nativa do pedido.
  Não se afirma equivalência da representação C# com os arrays nativos tipo1.
- O cálculo começa de stats base. Alvo face-down retorna antes das contribuições;
  fonte vazia/substituída ou face-down não contribui. Os vínculos válidos somam
  deltas ATK/DEF separados. ApplyResolvedTail(...,0,0) soma aos valores base e
  aplica clamp uma vez; Shield/Sword, Mirror Wall e inversão não são ativados
  por este sandbox. Exemplo: DEF100 −200 +300 => **200**, em qualquer ordem,
  não 300 por clamp intermediário. Nenhuma segunda tabela de bônus foi criada.
- RemoveMonster preserva destruição comum, removendo os equipamentos dos dois
  donos para seus respectivos cemitérios. RemoveEquipment é uma operação explícita
  de retirada do sandbox, sem legalidade de ativação, trigger ou novo efeito de carta.
  Apaga vínculo/fonte, evitando herança ao reutilizar slots. Remoção repetida não
  duplica cemitério. Sem troca de controle, outros destinos ou movimentos.

Desconhecidos externos continuam Unsupported no contrato. A classe DemoDuel,
como antes, não representa estado nativo parcialmente observado de forma completa.
As flags e temporização nativas, as mudanças dinâmicas posteriores, a ordem de
movimentos simultâneos e as exceções de limpeza não são implementadas.

## Verificação e ganho

EquipmentChecks cobre positivos e negativos de cada carta, cada campo ausente,
guarda falsa, identidade/posição inválidas, ambos os donos/alvos, face-down,
zonas cheias, revalidação após mudança de face/troca/saída do alvo, fase de batalha,
retirada isolada, destruição com equipamentos dos dois donos, reutilização dos
slots, ATK/DEF independentes e múltiplos equipamentos com clamp final.
Os outros 38 membros e variantes +2000 são explicitamente rejeitados.
Program mantém a regressão completa antiga de Legendary Sword; DirectDamageChecks
mantém os cinco P045 e as combinações sintéticas de flags/alvo.

Ganho: **um → três IDs equipáveis**, dois novos ciclos parciais; nenhuma nova
regra aritmética duplicada; DEF independente e clamp correto no estágio modelado.
Esses testes são sintéticos e não justificam ValidatedInScope.
Comandos, contagens finais e commits: handoff native-p025-implementation.md.
