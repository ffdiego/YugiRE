# Legendary Sword — vínculo e modificadores de ATK/DEF

Análise estática em 2026-10-29. Alvo SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Endereços VA, base 0x00400000. Nenhuma execução do jogo, alteração de originais
ou gravação do banco. Timing e interações ficam para o Windows.

## Identidade e resultado

Índice **614**, ID **300 / 0x12C**, registro **24**. A descrição local exige
Warrior e informa aumento de **300 ATK e 300 DEF**. O
[catálogo](../17-card-callback-catalog.md) coloca a carta em **P025**, com
41 registros que compartilham os mesmos cinco ponteiros:

| Slot | VA | Papel seguido neste caso |
|---|---|---|
| +4 | 0x00584500 | Confere origem/alvo e emite vínculo |
| +8 | 0x0057A470 | Filtro do candidato |
| +12/+16 | zero | Sem callbacks nesses slots |
| +20 | 0x00593A20 | Prepara seleção de alvo |

O efeito usa uma associação do monstro para a posição do equipamento.
O cálculo de atributos percorre essa associação e chama um helper que
seleciona a regra por ID. Para Legendary Sword, exige tipo dinâmico **0x0F**
e acrescenta **300/300** aos acumuladores. Os valores estão em código.
Este estudo cobre seleção, vinculação e recálculo; não fecha toda a limpeza.

## Seleção e revalidação

**0x0057A470 → 0x00579CE0** exige posição de monstro <5, índice ocupado
e máscara de face 0x0002 definida em registro+6. Para ID 300, exige tipo
dinâmico **0x0F / 15**, coerente com Warrior na descrição. O tipo vem de
**0x0056F6F0**, relacionado aos modificadores dinâmicos no caso de Parasite.
Esse helper recebe também a palavra auxiliar de registro+4 da origem quando
ela está no campo; o significado completo desse argumento segue aberto.
O ramo de ID 300 não exige que o alvo esteja do mesmo lado do equipamento.

Depois, 0x0057A470 consulta **0x0056C510**, verificação genérica já encontrada
em Man-Eater Bug, com exceção por ID 0x2E4 ainda não expandida aqui.

**0x00593A20** verifica contexto, conta candidatos e encaminha seleção por
flags de modo. No caminho de interface examinado, monta lado/posição,
testa 0x0057A470 e confirma por **0x00592B00**, chegando ao
[empacotamento de alvo com chave](man-eater-bug-targeting.md). Outros contextos
e ramos por ID do seletor permanecem fora deste fechamento.

Na resolução, **0x00584500** confere se a origem ainda tem o índice da carta,
resolve o primeiro alvo por **0x00583470** — incluindo sua chave — e
**repete 0x0057A470**. Só então chama **0x00574990**. Uma falha de alvo/filtro
nesse trecho marca pedido+4 com OR 8; a semântica geral dessa flag segue aberta.

## Escrita do vínculo

0x00574990 encaminha para **0x005749B0** com tipo **1**. Este emite comando
**0xB2** e evento **0x1A**. Os argumentos de dados do comando são:

1. Origem: `lado_equipamento | posição_equipamento<<8`.
2. Alvo: `lado_monstro | posição_monstro<<8`.
3. Tipo: **1**.

O dispatcher normaliza 0xB2 para **115 decimal** e usa stub
**0x005B970D → 0x0040A230**. O consumidor cria apresentação, espera os objetos
pendentes e chama **0x0056AB60(alvo, origem, tipo=1)**. Esse helper acrescenta
ao registro **do monstro** a posição do equipamento:

| Campo | Endereço |
|---|---|
| Contador uint16 | `0x00A55D7E + lado*0xD44 + posição*0x90` |
| Posições uint16 | `0x00A55D80 + lado*0xD44 + posição*0x90 + n*2` |
| Metadados uint16 | `0x00A55DC0 + lado*0xD44 + posição*0x90 + n*2` |

Ele grava posição/metadados e incrementa o contador. O vínculo persistente
seguido aqui guarda coordenadas; não carrega a chave do pedido de seleção.
Não inferimos ausência de referências obsoletas sem seguir a limpeza, nem
um vínculo recíproco somente a partir dessa escrita.

## Recálculo dos atributos

**0x0056DD30(lado, posição, saída)** produz uma estrutura temporária:

| Offset | Campo observado |
|---|---|
| +0, uint16 | Índice da carta |
| +2, byte | Tipo dinâmico |
| +3, byte | Outro atributo dinâmico |
| +4, int32 | ATK calculado |
| +8, int32 | DEF calculada |

O cálculo começa pelos acessores base **0x004027B0 (ATK)** e
**0x00402800 (DEF)**. Para posição virada para baixo, retorna antes da parte
de modificadores examinada. Depois percorre as associações do monstro.

No **tipo 1**, exige equipamento ocupado, com face definida, sem máscara
0x20 em registro+0x8C e sem certos bloqueios globais. Há ainda uma condição
de proteção do monstro. Para ID 300, chega a **0x0056FD40**, que verifica
**0x0F no tipo dinâmico da saída** e soma **300/300** aos acumuladores.

Em **0x0056FF13**, ESI recebe 300 e EDI recebe o mesmo valor. O trecho
**0x00570066** soma esses registradores nos dois acumuladores. O chamador
incorpora os acumuladores aos campos ATK/DEF da saída. Há um ramo global que
inverte o sinal e outros que substituem valores; +300/+300 descreve o caminho
comum sem esses efeitos. A função também satura valores finais negativos em zero.

O resultado é recalculado de valores base, associações e condições atuais.
A restrição Warrior reaparece no cálculo da contribuição. Isso não resolve
sozinho todas as interações com alterações de tipo e supressão de equipamentos.
**0x0056F690** retorna o campo +4 e **0x0056F6B0** retorna +8 aos chamadores;
nem todos os usos em batalha/interface foram percorridos nesta etapa.

## Reprodução e limites

```bash
PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_legendary_sword.py
```

Esperado: doze linhas `PASS`. Revalida estudos anteriores e preservação dos
originais/banco; confere identidade, registro, combinação de 41 membros,
destinos das tabelas para ID 300, instruções de tipo/valor/soma e dispatch
de 0xB2. **Nenhuma função nativa foi executada pelo validador.**

Evidências: [callbacks](../evidence/legendary-sword-callbacks.txt),
[filtro/seleção](../evidence/legendary-sword-link-and-filter.txt),
[emissão da associação](../evidence/legendary-sword-associations.txt),
[consumidor](../evidence/legendary-sword-link-consumer.txt),
[escrita/cálculo](../evidence/legendary-sword-state-and-stats.txt),
[helper de bônus](../evidence/legendary-sword-bonus-table.txt),
[instruções](../evidence/legendary-sword-selected-instructions.txt),
[escritas/chamadas](../evidence/legendary-sword-write-and-call-instructions.txt),
[xrefs dos acessores base](../evidence/legendary-sword-attribute-accessors.txt),
[relatório](../evidence/legendary-sword-validation.json).

Pendências estáticas: remoção/compactação dos vínculos ao sair equipamento
ou monstro; mudanças posteriores de tipo; composição/ordem dos modificadores;
outros ramos P025. Execução futura pode confirmar o caminho comum e a interface.
