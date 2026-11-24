# Prohibition, Jinzo e fontes efetivas — roteiro do lote 10

Este documento acompanha a integração dos PRs #55 e #56, **sem
declarar Prohibition implementada**.

## Evidências disponíveis no SQLite

- Prohibition: resource ID **1162** (`0x48A`), índice DAT **229**,
  classe `22` (Spell); `card_callback_slots` aponta primário
  `0058C430` e target preparation `00595F20`.
  Essas duas funções **não estão decompiladas no banco atual**.
- `0056B440` está decompilada e possui **37 funções chamadoras**
  diretas no índice de referências. Ela procura Prohibition ativa
  nas zonas S/T, percorre ligações de tipo **9** e **11** e
  chama `005674E0` para comparar identidades.
- `005674E0` normaliza certos IDs com deslocamento de 2000 e
  reconhece grupos especiais (por exemplo, as duas ocorrências
  históricas de Polymerization `1003/1034` e Kuriboh `57/1901`).
  É comparação interna de família, **não prova de equivalência
  de todos os efeitos dessas cartas**.
- `00569820`/ `005699C0`: contagem de presença **efetiva**
  que consulta `0056B440`; distingue lado, posição e estado.
- `005699F0`/ `00569B00`: contagem de presença face-up que
  consulta bits de supressão por posição, mas **não** chama
  `0056B440` nesse caminho.
- `00598D70`: recalcula supressão por Jinzo efetivo `0x2EF`,
  consulta Royal Decree `0x409` e Royal Command `0x5FA`,
  além de máscaras temporárias e flags de posições.
- `0059D370`: quando a origem é Prohibition, lê a declaração
  ligada ao slot de tipo 9 e emite operações às cartas com
  identidade correspondente nos dois campos. A função é outro
  candidato para compreender o ciclo de vida dos vínculos.

## Limite arquitetural

Não basta criar um `HashSet<ushort>` de IDs declarados e bloquear
todos os efeitos em todo instante. É necessário modelar:
**origem Prohibition**, identidade declarada,
ligações/ocorrências, momento de incidência e remoção/restauração.

Os textos atuais de Prohibition admitem exceção para cartas
que já estavam no campo quando a declaração passou a valer.
Isso é contexto histórico a verificar contra o jogo de 2004,
não uma prova derivada da decompilação.

## Experimentos nativos recomendados

1. Declarar Jinzo **antes** de ele entrar no campo vs. após
   ele estar face-up; observar Royal Command face-up e um FLIP.
2. Declarar Royal Command antes vs. após sua ativação; observar
   efeitos FLIP e reação de Royal Decree.
3. Remover Prohibition e confirmar quais ocorrências recuperam
   efeito, sem pressupor que os mesmos bits de supressão são limpos
   sincronicamente.
4. Capturar `0x0058C430`, `0x00595F20`, `0x0056B440`,
   `0x0059D370`, referências e mudanças das ligações nos
   11 slots por lado usando o ZIP do roteiro 06.

**Estado:** Pending para implementação de Prohibition, e
Partial para efetividade global. Sem validação de duelos nativos.
