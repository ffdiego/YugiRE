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

## Mudança de posição no turno de colocação

`0x00568610` define bit **0x20000** no DWORD de `registro+0x8C` ao colocar
um monstro no campo. `0x0056B5A0` recusa a mudança de posição quando esse bit
está definido; `0x005A0530` consulta o helper ao construir as ações disponíveis.
`0x0056C180` é outro produtor da mesma marca. O consumidor do comando 0x02,
`0x00574230`, aplica AND 0xFFFF aos flags dos 12 registros de ambos os lados,
removendo essa marca antes da alternância de jogador já seguida anteriormente.

Não confundir com o bit **0x20000 do controle do jogador**, consultado em
`0x005A02A0`, ou com **0x100000 do controle do jogador**, consultado em
`0x00570A70`. O segundo helper também verifica Jam Breeding Machine do lado
consultado e The Last Warrior from Another Planet nos dois lados; ele não é
simplesmente um contador de invocações normais. O ciclo completo dos bits de
controle do jogador permanece parcialmente aberto.

O demo agora bloqueia mudar para defesa imediatamente após invocar. No
próximo turno próprio, volta a permitir uma mudança; atacar depois da
invocação continua permitido quando a fase de batalha está disponível.
Os testes de combate foram ajustados para montar defesa em um turno legal.
Total acumulado: **146 verificações C#**. O validador de tributos também
confere os quatro trechos de instruções relacionados à marca de posição.

Para comparar no Windows: invoque um monstro, tente mudar sua posição no
mesmo turno e repita no próximo turno próprio. Não exige uma carta específica.

Evidências: [gates da mão](evidence/normal-summon-turn-controls.txt),
[ações de campo](evidence/monster-field-action-gates.txt),
[condições](evidence/monster-position-gates.txt),
[colocação](evidence/monster-placement-turn-flags.txt),
[limpeza](evidence/monster-turn-flag-reset.txt) e
[instruções](evidence/monster-turn-state-application.txt).

## Comparação de estado no Windows

Com o original já aberto em um duelo, execute da raiz do repositório:

```powershell
.\scripts\Get-JoeyDuelSnapshot.ps1 -OutputPath "$env:TEMP\joey-before.json"
# Invoque um monstro no jogo e capture novamente:
.\scripts\Get-JoeyDuelSnapshot.ps1 -OutputPath "$env:TEMP\joey-after.json"
```

O script valida o SHA256 do executável selecionado e abre o processo com
VM_READ/QUERY_LIMITED_INFORMATION. Não inicia o jogo, não pausa threads e não
escreve na memória. Lê um bloco cobrindo os dois jogadores: LP em
`0x00A55D64 + lado*0xD44`, contagem da mão em +4, cinco registros de monstros
a partir de +0x10 e flags do jogador em +0x6CC. Usa o endereço relativo à base
real do módulo. `card_index` é índice interno, **não** ID de recurso; zero
significa zona vazia. `position_locked_this_turn` decodifica o bit seguido acima.

O arquivo de saída é criado com CreateNew, sem sobrescrever capturas existentes.
Sem OutputPath, mostra JSON no console. Se houver vários processos, escolha
um com `-TargetProcessId`. A leitura de um processo vivo não é um snapshot
atômico: faça a comparação com a interface em repouso entre ações.

Compare antes/depois de invocar e depois de encerrar o turno: o bit de posição
deve aparecer no monstro recém-colocado e desaparecer na limpeza. As flags do
jogador são exportadas em hexadecimal, sem dar nomes definitivos a cada bit.
O estudo adicional dos setters em [player-summon-flag-lifecycle.txt](evidence/player-summon-flag-lifecycle.txt)
não fechou o ciclo completo do contador de invocação normal.

O C# de interoperabilidade do script compilou sem avisos/erros. PowerShell
não está instalado no host Fedora desta sessão. Em 2026-10-31, o usuário
executou o script no Windows e forneceu as duas capturas descritas abaixo.

## Primeira comparação fornecida pelo operador — 2026-10-31

[JSON transcrito das duas saídas](evidence/windows-gemini-elf-before-after-2026-10-31.json).
As capturas têm o mesmo PID 2240, hash do alvo estudado e base 0x00400000.
Não são observação direta do agente. O operador posteriormente confirmou a
invocação em ataque e esclareceu a sequência de turnos descrita abaixo.

| Campo | Antes | Depois |
|---|---|---|
| LP dos lados 0/1 | 8000/8000 | 8000/8000 |
| Mão do lado 0 | 5 | 5 |
| Mão do lado 1 | 6 | 5 |
| Zona 0 do lado 1 | vazia | índice 550 |
| Flags do registro nessa zona | 0 | 0x00030000 |
| Palavra de posição | 0 | 0x0202 |
| Flags do jogador lado 1 | 0 | 0x000B0000 |

O DAT associa índice **550** a ID **531**, **Gemini Elf**. O registro observado
contém o bit 0x20000 que a análise estática ligou ao bloqueio de mudança de
posição. Também contém 0x10000; as flags do jogador têm bits 16, 17 e 19.
Não atribuímos individualmente significado a esses bits a partir de um único
par de capturas. `occurrence_word=0` é observado no monstro ocupado e não deve
ser usado como teste de zona vazia; a ocupação é determinada pelo índice.

A mudança é compatível com a colocação de Gemini Elf da mão no campo,
sem custo de LP entre as duas capturas completas. O booleano
`position_locked_this_turn` é uma interpretação do script baseada no bit;
a confirmação independente do comportamento veio pelo relato posterior.

### Continuação: defesa e encerramento de turno

O operador relatou "mudei pra defesa e encerrei o turno" e forneceu um trecho
adicional, preservado no mesmo JSON como `follow_up_excerpt`. No lado 1, zona
0 mantém índice 550, palavra de posição passa de 0x0202 para **0x0203** e os
flags passam a **zero**, incluindo a marca 0x20000. O relato corrobora o bit 0
da palavra de posição associado à defesa. A ausência da marca após encerrar
o turno é compatível com a limpeza estática seguida anteriormente; não isola
o instante exato da limpeza.

O trecho também informa LP 7900, mão com seis cartas e `occurrence_word=2`
(antes 0). Não foram fornecidos timestamp, PID ou restante da captura. Não
atribuímos causas à perda de 100 LP, à carta adicional ou à alteração dessa
palavra. O operador identifica a carta como a mesma Gemini Elf, mas não foi
fechado o mecanismo que atualiza essa palavra em memória.

### Sequência esclarecida pelo operador

O usuário confirmou que **não é possível trocar a posição no turno da
invocação**. Relatou invocar Gemini Elf em ataque e usá-la para atacar e
destruir uma carta no turno inicial; só no **próximo turno próprio** mudou-a
para defesa. O trecho final foi capturado após encerrar esse segundo turno.
O momento exato do ataque em relação à segunda captura completa não foi fixado.

Esse relato corrobora no original as três regras já usadas no demo: bloqueio
da mudança manual no turno de invocação, possibilidade de atacar nesse turno
e liberação da mudança no próximo turno próprio. Não foi necessária alteração
de código. A causa da mudança de `occurrence_word`, os demais bits dos flags
e o instante preciso de cada limpeza continuam separados dessas conclusões.

## Tributo observado pelo operador — 2026-10-31

[Capturas transcritas](evidence/windows-tribute-summon-before-after-2026-10-31.json),
PID 22040, mesmo fingerprint/base estudados. O operador identificou a ação
como tribute summon. O lado 0 permanece integralmente igual no par.

| Lado 1 | Antes, 05:02:46 UTC | Depois, 05:03:01 UTC |
|---|---|---|
| LP | 400 | 400 |
| Mão | 3 | 2 |
| Zona 0 | índice 651, Alligator's Sword Dragon | índice 823, Flame Cerebrus |
| Palavra de ocorrência | 34 | 35 |
| Palavra de posição | 0x0202 | 0x0202 |
| Flags do monstro | 0x00020008 | 0x00030000 |
| Flags do jogador | 0x00090000 | 0x010B0000 |

O DAT identifica o primeiro monstro como Fusão de nível 5, ID 812, e o segundo
como monstro de nível 6, ID 528 (grafia **Flame Cerebrus** no recurso inglês).
O par corrobora a retirada de um monstro para invocar o de nível 6, o consumo
de uma carta da mão, o reaproveitamento da mesma zona e ausência de custo de
LP nessa operação. O monstro usado como tributo tinha a marca 0x20000; isso
também demonstra que o bloqueio de posição não impede servir de tributo.

Uma Fusão já presente no campo pode ser tributada: a restrição de invocação
normal da Fusão aplica-se à carta a invocar, não a essa utilização como custo.
Foi adicionado um teste de regressão com os dois registros reais, incluindo
os 400 LP. Total C# acumulado: **147 verificações**.

O XOR dos flags do jogador é 0x01020000: surgem os bits 17 e 24. O primeiro
também apareceu na invocação anterior de Gemini Elf; o segundo é candidato
a relação com esta invocação por tributo, mas seu significado individual
ainda precisa ser seguido no código. Não usamos o par para deduzir sozinho
o destino da carta retirada: o script atual não captura o cemitério.
