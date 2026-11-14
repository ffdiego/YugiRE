# P045 — piloto de dano direto delimitado

Alvo SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Branch feature/p045-direct-damage, criada de 441dda8. Nenhuma execução nativa.
REA open_binary confirmou o hash; analyze_function não dispunha de provider
(Ghidra não configurado). Sessão encerrada. Fallback: objdump GNU em leitura e
conferência de bytes com mapeamento de seções PE, sem banco Ghidra persistente.

Fontes históricas preservadas: sparks-ookazi.md, simple-lp-card-callbacks.txt
(pseudocódigo completo), simple-lp-card-targets.txt, selected-branches e
target-write. A desmontagem atual conferiu o despacho completo 584e60..585120,
a preparação 593cc0..593ce0 e o emissor 5784e0. Apenas os cinco IDs abaixo
são objeto desta investigação; outros ramos não recebem suporte.

| Carta | Índice / recurso | Row | Entrada do ramo | Imediato | Chamada |
|---|---|---:|---|---:|---|
| Sparks | 755 / 342 (156h) | 66 | 00584f1b | 200 | 00584f28 |
| Hinotama | 769 / 343 (157h) | 67 | 00584f42 | 500 | 00584f4f |
| Final Flame | 738 / 344 (158h) | 68 | 00584f5e | 600 | 00584f6b |
| Ookazi | 557 / 345 (159h) | 69 | 00584fa7 | 800 | 00584fb4 |
| Raimei | 1049 / 1007 (3efh) | 172 | 00585076 | 300 | 00585083 |

## Fatos estáticos

Todos têm primary 00584e60, candidate/availability/preparation nulos e
 target_preparation 00593cc0. Isso somente seleciona os caminhos a investigar.
O índice é convertido em resourceId por 004022e0 antes dos testes por ID.
Sparks: comparação 584eb4/584ebb; Hinotama: 584f37/584f3c; Final Flame:
584ea3/584eae; Ookazi: 584f7a/584f7f; Raimei: 584fea/584fef.

- Guarda comum 584e76..584e7b: pedido+4 bit 2 (máscara 4) desvia para
  58511a, sem emissão de dano. O nome semântico desse bit continua Unknown;
  não se afirma que seja toda a regra de negação.
- Cada um dos cinco ramos lê byte[pedido+6] & 1 e envia esse destinatário e
  seu imediato a 005784e0. Não substitui o destinatário preparado pelo
  oponente recalculado: podem ser diferentes em pedido manipulado.
- 00593cc0 calcula 1 - (byte[pedido+2] & 1) e chama 00592a50. Esse helper
  acrescenta palavra ao pedido+6+2*contador, contador nos bits 13..15 de +4.
  A composição usual com contador inicial zero põe o oponente em +6.
  Não se afirma que toda chamada tenha contador zero; a API escalar exige
  destinatário explicitamente preparado e não simula o buffer/counter.
- Emissor 5784e0: para estes valores positivos, comando 0043h (lado 0) ou
  8043h (lado 1), valor do imediato, campo seguinte 0, último campo 1 em
  005b9170. O restante do helper gera apresentação/estado não implementados
  pela API escalar. O retorno dos cinco ramos tem AX=0; bits altos não são
  um retorno inteiro zero garantido.
- O consumidor negativo e saturação a zero já foram seguidos em
  parasite-paracide.md seção 5, com parasite-value-command.txt,
  parasite-life-and-callback-routing.txt e parasite-life-arithmetic.txt.
  Nenhuma nova investigação de outra família foi feita.

Reprodução somente de leitura:

```bash
python3 scripts/inspect_p045.py --exe ../passion/joey_pc.exe
objdump -d -Mintel --start-address=0x584e60 --stop-address=0x585124 ../passion/joey_pc.exe
objdump -d -Mintel --start-address=0x593cc0 --stop-address=0x593ce1 ../passion/joey_pc.exe
```

p045-static-checks.json guarda metadados e fragmentos mínimos, sem descrições
ou recursos extraídos em massa. O script verifica fingerprint, cinco registros,
imediatos, máscara do alvo, destino das calls, AX e guarda/preparação/comando.
Não é prova dinâmica nem emulador independente.

## Design e limites

O demo tinha switches duplicados para Sparks=200 e Ookazi=800, em Play e
ActivateSetSpell. A abstração proposta é uma função pura restrita aos cinco
IDs: quantidade, guarda +4 e destinatário preparado -> plano de comando de LP.
Flags ou destinatário desconhecidos produzem Unsupported, nunca zero implícito.
ID não listado, inclusive variante +2000, não ganha suporte por normalização.
A preparação escalar do oponente é separada. Não há framework genérico de
cadeias, eventos ou efeitos de outras famílias.

O demo pode consumir o plano assumindo explicitamente flags zero e preparação
usual com ator ativo. Ativação imediata, movimento para cemitério, condições
de fase/zona e vencedor são políticas já existentes do demo, não equivalência
nativa nova. O fixture inicial do demo continua contendo apenas os dois burns
anteriores; as novas cartas podem entrar por decks/estado explícito.

Ganho verificável esperado: três IDs novos no caminho de dano do demo e uma
API escalar para os cinco ramos, com guarda conhecida. Carta inteira continua
Partial/Unknown, inventário Open, validação no máximo SyntheticOnly. Sem
fontes independentes de contexto, não promover a ValidatedInScope.

Captura futura necessária: correlacionar índice/recurso, ator, contador e alvo
preparado, flags no consumo, comando 43/8043 e campo de valor, LP antes/depois,
ordem e limpeza; casos dos dois lados, guarda com/sem máscara4, rejeição e
interações. Atestar EXE/captura, registrar eventos sem perda e entradas
independentes. Nome da flag, legalidade completa, cadeias, modificadores,
redirecionamento e animação permanecem lacunas. Análise estática é suficiente
para este plano escalar; não justifica execução de hooks nesta sessão.

## Resultado implementado

`DirectDamageRules.Amount/PrepareTarget/Resolve` em Joey.Core produz plano de
comando, SkippedByRequestFlag ou Unsupported. Flags/target nulos não viram zero.
Demo.Play/ActivateSetSpell consomem o plano com flags=0 declaradas como política.
CanActivateFromHand reconhece os cinco IDs; o fixture inicial não foi ampliado.

Baseline da implementação e dos testes:
`0a9b4434dd7d185b5c44c20680b11258f7f6e164`.
O registry novo em docs/coverage/p045.annotations.json registra três mecanismos
estreitos e quinze vínculos, com fontes e testes pinados nesse commit. Os
inventários de carta ficam Open; validação integral Unknown. Não há promoção
por P045 nem reinterpretação de evidências das branches experimentais.

| Carta | Quantidade/destinatário/guarda | C# | Validação | Lacunas |
|---|---|---|---|---|
| Sparks | 200, +6 & 1, skip se +4 & 4 | Plano escalar e demo | SyntheticOnly no vínculo; carta Unknown | Contexto nativo e ciclo completo |
| Hinotama | 500, +6 & 1, mesma guarda comprovada | Novo ID no demo e plano | SyntheticOnly no vínculo; carta Unknown | Timing, flags, interações |
| Final Flame | 600, +6 & 1, mesma guarda comprovada | Novo ID no demo e plano | SyntheticOnly no vínculo; carta Unknown | Timing, flags, interações |
| Ookazi | 800, +6 & 1, mesma guarda comprovada | Plano escalar e demo | SyntheticOnly no vínculo; carta Unknown | Contexto nativo e ciclo completo |
| Raimei | 300, +6 & 1, mesma guarda comprovada | Novo ID no demo e plano | SyntheticOnly no vínculo; carta Unknown | Timing, flags, interações |

Testes sintéticos: todas as 256 combinações de byte flags por 256 bytes de alvo
para cada ID (327680 combinações ao todo), fontes dos dois lados, contexto
incompleto, LP abaixo/igual/acima do dano, ativação de mão/carta setada, repetição,
fase, jogo encerrado, zonas cheias, índices inválidos e IDs não auditados
(inclusive outros com primary compartilhado e variantes +2000). A validação
estática usa o EXE legítimo em leitura e não constitui oráculo dinâmico.
