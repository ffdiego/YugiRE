# Retirada de cópias de batalha: limite do caso Cyber Jar

Análise estática, VA com image base 0x00400000, executável SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Nenhuma execução do original ou alteração persistente do banco.

## Resultado limitado

O callback de Cyber Jar, ID 1106, é 0x0058A880 no catálogo já existente.
A decodificação transitória confirma a entrada, o teste de pedido+4 bit 4 e
um despacho por estado global em 0x0058A8B2. No ramo 0x0058A8B9:

1. Chama 0x00576940 com máscara 0x001F001F.
2. Testa `(uint16[pedido+2] & 0x0FC0) == 0x0500`.
3. Se verdadeiro, chama 0x00578030 com seletores 0 e 1.
4. Retorna AX=0x7F.

Não foi mapeado qual valor do estado global seleciona esse ramo. A condição
0x0500 é registrada numericamente, sem lhe atribuir um nome de evento.

0x00578030 seleciona `0x00A57864 + seletor * 0x90`, consulta o índice nos
12 bits baixos e retorna sem produzir retirada se estiver vazio. Lado e
posição são obtidos de 0x00A57840, com deslocamentos diferentes para os dois
seletores. São cópias de registros, não a matriz normal do campo.

O produtor consulta presença do ID 0x453, Banisher of the Light, e distingue
o intervalo 0x780–0x783 das quatro fichas. Emite comando 0x79 por 0x005B9170
em 0x005781BB e encaminha o registro para 0x00576F40 e 0x00577280.
O primeiro helper também é chamado pelo produtor de destruição por efeito
0x00576B30; o segundo trata vínculos/equipamentos, conforme análise anterior.
Assim, há encaminhamento compartilhado de consequências de retirada.
Não seguimos aqui todos os casos específicos de 0x00576F40.

## Consequência para a implementação

A existência dessas cópias e o tratamento de Banisher/fichas mostram por que
`Monsters[p,z] = null` mais inserção simples no cemitério não reproduz toda a
lógica do original. O protótipo mantém esse comportamento temporário para
combate, explicitamente sem efeitos de retirada, fichas ou equipamentos.

Este resultado NÃO confirma comparação de ATK/DEF, fórmula de dano, vitória,
ordem de eventos de batalha ou que todo combate passe por 0x00578030.
O chamador concreto examinado é Cyber Jar. A implementação de Sparks/Ookazi
continua fundamentada na análise específica já concluída; o restante do
combate em Demo.cs continua sendo política de demonstração.

## Evidências e reprodução

- [Produtor](evidence/battle-removal-entry.txt): decompilação e chamadores.
- [Roteamento](evidence/battle-removal-routing.txt): instruções da entrada,
  chamadas do ramo de Cyber Jar e helper compartilhado.
- [Cauda](evidence/battle-removal-caller.txt): emissão e chamadas posteriores.
  A consulta por endereço interno não recuperou função do chamador; foi
  complementada pela decodificação transitória seguinte.
- [Entrada de Cyber Jar](evidence/cyber-jar-battle-snapshot.txt): 45 instruções,
  sem persistir código no projeto Ghidra.

```bash
bash scripts/run-archive-query.sh battle-removal-entry.txt 00578030
bash scripts/run-archive-query.sh battle-removal-routing.txt 00576f40 ins:00578030:100 ins:0058a8b0:35
bash scripts/run-archive-query.sh battle-removal-caller.txt 0058a8dc ins:00578165:65
bash scripts/run-archive-query.sh cyber-jar-battle-snapshot.txt decode:0058a880:45
```

Os relatórios de instruções indicam quando o endereço pedido não coincide
com uma instrução definida; nesses casos a ferramenta parte da próxima
instrução existente. Não se interpreta o trecho omitido como código ausente.
