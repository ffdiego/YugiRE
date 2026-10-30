# Invocação normal e tributos

EXE SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Endereços VA, image base `0x00400000`. Análise estática, sem execução do original.

## Critério comprovado

`0x005AAB90` verifica condições para invocação normal e consulta o nível por
`0x004026C0`. Níveis 0–4 exigem espaço livre; 5–6 exigem ao menos um candidato
e condição de espaço; o ramo restante exige ao menos dois candidatos.
`0x005AAB30` também admite um monstro próprio elegível que libere a posição.
Logo, campo cheio não equivale automaticamente a impossibilidade de invocar
um monstro que exige tributos. Um segundo consumidor, `0x005AEE70`, repete a
distribuição zero/um/dois e procura essa quantidade de candidatos.

As tabelas de sete bytes em `0x005AADF0` e `0x005AEF08` são
`0,0,0,0,0,1,1`. Os ramos de `0x005AEE70` atribuem 0 e 1 para esses grupos,
com 2 no caso padrão. Isso não determina sozinho a legalidade de cada monstro.
O gate anterior também consulta bloqueios, estado do jogador e restrições
especiais. `0x00402690` aceita classes **1–20**, excluindo a classe vazia 0;
esse detalhe foi corrigido em `Card.IsMonster` e nos fixtures sintéticos.

## Pagamento e colocação

No modo 1 de `0x005AB820`, `0x005ABD00` lê até três referências selecionadas
do descritor `0x00A55080`. Para as referências presentes, emite comandos 0x57
e chama `0x00577B30` antes de emitir o comando 0xCE. O helper usa o mecanismo
compartilhado de retirada com destino 14 e flags 0x1000, já seguido nos Rituais.
O consumidor de 0xCE, `0x004059C0`, copia a carta da mão, remove a entrada
pela rotina `0x00570790` e encaminha a colocação para `0x00568610`.
Essa é ordem lógica do código/fila, não medição de animações ou frames.

`0x0056A200` considera ambos os lados através de `0x0056A0D0`, mas isso não
libera tributos adversários comuns. O filtro exclui IDs 0x781–0x782, usa
`0x0056A140` e respeita Mask of Restrict (0x58A). As permissões especiais de
Soul Exchange já estão documentadas em [soul-exchange.md](cards/soul-exchange.md).

## Demo

Selecione o monstro da mão, depois os monstros próprios a tributar. A seleção
fica vermelha. Após selecionar a quantidade exigida, clique numa zona vazia
ou numa das selecionadas. Cancel limpa a seleção sem pagar custo.

Todas as validações precedem as alterações: quantidade incorreta, posição
inválida/repetida, monstro ausente, lado incorreto, posição ocupada não
selecionada ou segunda invocação no turno não removem cartas. O pagamento
usa a retirada comum, incluindo a limpeza de equipamentos do alvo.
Essa transação compacta, os controles e a transferência direta ao cemitério
são decisões do demo; não reproduzem fila, respostas ou efeitos disparados.
Continuam ausentes Soul Exchange, fichas, restrições especiais completas,
invocação baixada e efeitos que alteram a quantidade de invocações/tributos.

## Validação

`python scripts/validate_normal_tributes.py` confere fingerprint, dois seletores,
duas tabelas de destinos e quatro ramos. [Resultado](evidence/normal-tributes-validation.json).
Os testes C# cobrem níveis 4/5/6/7/8, campo cheio, pagamento parcial proibido,
equipamentos, conservação da mão/cemitério e segunda invocação. Total: **133
verificações**, build sem avisos/erros. Validação visual ainda pendente.

No Windows: use um deck importado com monstros de nível baixo e um de nível
5–6 ou 7+. Compare a quantidade exigida, tente destino ocupado não selecionado,
cancele antes de confirmar e confira que apenas os monstros escolhidos saem.

Evidências: [consumidores de nível](evidence/normal-summon-level-consumers.txt),
[instruções](evidence/normal-summon-level-instructions.txt),
[modos do contexto](evidence/normal-summon-tribute-context.txt),
[pagamento e espaço](evidence/normal-summon-tribute-payment.txt),
[elegibilidade](evidence/normal-summon-tribute-eligibility.txt),
[colocação e limites](evidence/normal-summon-placement-boundaries.txt).

## Monstros que não podem usar invocação normal

O gate `0x005AAB90` rejeita retorno não zero de `0x00567A00` antes de contar
tributos. Esse helper usa a categoria derivada de `0x00402740`: para monstros,
categorias 2 e 3 são bloqueadas. Na categoria não zero, também há uma lista
explícita de IDs, incluindo Great Moth, Harpie Lady Sisters, Gate Guardian,
Toons, Valkyrion e Dark Necrofear. A transcrição completa está isolada em
`Joey.Core/NormalSummonRules.cs`; não depende de comparação de nomes.

O acessor da categoria usa bits 18–19 e preserva as exceções por classe e
IDs 0x776–0x778. Evidência anterior do acessor em
[card-property-consumers.txt](evidence/card-property-consumers.txt), e do gate em
[normal-summon-placement-boundaries.txt](evidence/normal-summon-placement-boundaries.txt).

A interface informa que é necessária invocação especial. A recusa não paga
tributos nem remove a carta da mão. Isso **não** implementa Fusão, Ritual ou
os procedimentos especiais dessas cartas; apenas impede uma invocação normal
que o original recusa. Os outros gates dinâmicos e modificadores continuam
fora do demo.

Validação acumulada: **138 verificações C#**. Inclui Alligator's Sword Dragon,
Black Luster Soldier, Great Moth e Blue-Eyes do DAT original, rejeição sem
pagamento e as exceções da categoria derivada. Não houve execução do original.
