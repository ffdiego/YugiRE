# Materiais substitutos de fusão

O Joey reconhece quatro substitutos pelo resource ID em `00568580`. Os quatro registros têm máscara Kaiba em `CARD_PACK`. O texto do DAT inglês descreve uma substituição por fusão, consistente com os testes de cota no executável.

| ID | Carta |
|---:|---|
| 108 | Goddess with the Third Eye |
| 257 | Beastking of the Swamps |
| 268 | Versago the Destroyer |
| 641 | Mystical Sheep #1 |

## Disponibilidade e seleção

- **Materiais e cópias.** `00591A10` procura no campo próprio por ID da receita, depois na mão própria. Se não encontra material exato, procura substituto no campo e depois na mão. A busca exata admite a variante com ID base + 2000. O predicado de substituto confere os quatro IDs diretamente. Cada chamada recebe os descritores das ocorrências já escolhidas e evita reutilizá-las.
- **Cota.** `00591D10` exige preencher toda a receita. Para dois materiais, verifica o único par e rejeita dois substitutos. Para três materiais, verifica os três pares e rejeita qualquer par de substitutos. Nenhuma das 55 receitas base lista um dos quatro substitutos como material nominal.
- **Escolha manual.** O seletor `005921C0` consulta a lista de materiais ainda exigidos em `00592040`. Para um candidato substituto, `00592506–00592523` consulta o contador e admite a escolha quando ainda está em zero. `00592593–005925A3` rejeita a repetição do mesmo descritor de mão/campo. Após confirmação, `00592604–0059262A` incrementa o contador. `005920D0` retira uma exigência exata por vez. Quando é usado um substituto, a exigência que ele cobre fica implícita nos materiais exatos escolhidos nas outras etapas.
- **Vaga.** `00591D10` verifica se há vaga livre com `0056A030`. Com campo cheio, exige que pelo menos um dos materiais que o pré-teste encontrou esteja no campo e possa liberar a vaga segundo `00569E10`. A seleção manual também repete a checagem de vaga.
- **Magias.** Polymerization (1003, 1034) usa `00580600` para disponibilidade e `005921C0` para seleção. Depois de conferir a origem, o ramo de efeito de Fusion Gate (1547) em `005916E0` entra em `005921C0` pela chamada em `00591739`. A cota é compartilhada. O destino dos materiais continua dependendo da magia, conforme o [catálogo de receitas](../research/card-static-sweep/12-receitas-de-fusao.jsonl).

## Caso de campo cheio para conferir no original

Com Baby Dragon (3) e Time Wizard (15) na mão e Goddess with the Third Eye no campo cheio, há uma combinação que respeita a receita de Thousand Dragon (68) e libera vaga. O caminho lido em `00591D10` monta primeiro a combinação exata da mão e testa espaço nessa combinação. O trecho examinado retorna falha sem experimentar a substituição no campo. A consequência esperada é Thousand Dragon faltar entre os resultados disponíveis nesse estado. É uma previsão da leitura estática e da simulação, pendente de confirmação no jogo.

## Recorte para o Core

A validação proposta é contabilizar as ocorrências de material por ID da receita e permitir que um substituto cubra a única exigência restante. Para uma escolha parcial, a interface deve oferecer somente materiais que ainda permitem completar a receita com uma vaga livre ou liberada. O mesmo teste de existência de uma combinação completa pode servir para abrir a seleção e para a CPU escolher materiais. A política proposta aceita a combinação válida no caso de campo cheio acima enquanto a preferência específica do pré-teste do Joey fica pendente de reprodução.

| Cenário | Resultado esperado no Core |
|---|---|
| Baby Dragon e um substituto para Thousand Dragon | Aceitar |
| Dois substitutos para Thousand Dragon | Recusar |
| Dois Blue-Eyes White Dragon e um substituto para Blue-Eyes Ultimate Dragon | Aceitar |
| Um Blue-Eyes White Dragon e dois substitutos para Blue-Eyes Ultimate Dragon | Recusar |
| Duas referências à mesma ocorrência de Thunder Dragon | Recusar |
| Campo cheio e única combinação com vaga usa o substituto no campo | Aceitar segundo a política proposta |

## Validação estática

Decompilações e referências do SQLite foram lidas em modo somente leitura. As instruções do seletor foram conferidas no `joey_pc.exe` cujo SHA-256 começa em `db8fe7b7`; o hash completo está no [resumo da campanha](../research/card-static-sweep/README.md). Uma simulação independente em Python reproduziu as seis combinações acima e a diferença do pré-teste em campo cheio. A ordem e apresentação das opções ainda requerem verificação com o Joey em execução.
