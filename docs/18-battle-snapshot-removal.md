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

## Continuação: acessores distinguem campo, listas e cópias

A consulta de referências levou a acessores compartilhados, não ainda ao
produtor da captura. [Referências](evidence/battle-snapshot-references.txt),
[decompilações](evidence/battle-snapshot-copy-helpers.txt) e
[instruções](evidence/battle-snapshot-copy-instructions.txt).

`0x005700F0(lado, região, posição)` retorna ponteiro. Para regiões 11–15,
usa arrays de palavras de 32 bits; para o caminho comum com
`região + posição < 11`, retorna registro de 0x90 bytes no campo:

| Região | Base do lado zero | Passo por elemento |
|---:|---:|---:|
| 11 | 0x00A56434 | 4 |
| 12 | 0x00A566B4 | 4 |
| 13 | 0x00A56574 | 4 |
| 14 | 0x00A566F0 | 4 |
| 15 | 0x00A5686C | 4 |
| Campo comum | 0x00A55D74 | 0x90 |

Esses ramos usam lado mascarado por 1 e passo entre lados de 0xD44 bytes.
Não acrescentamos nomes novos às regiões 12/13 somente por sua ordem física.
Os papéis de 11, 14 e 15 já foram discutidos nos estudos de movimentação.

Fora desses ramos, compara o argumento lado com bit 0 de 0x00A57840 e
região com bits 8–10; correspondência seleciona 0x00A57864, qualquer outra
combinação seleciona 0x00A578F4. Não é uma validação defensiva de coordenadas.
`0x00570230` repete o roteamento e retorna o índice nos 12 bits baixos.
As instruções confirmam o ramo por soma (CMP 10/JLE), comparação de lado e
seleção dos ponteiros. Não há conclusão de que argumentos arbitrários sejam
válidos apenas porque o helper retorna um endereço.

`0x005703A0` retorna bit 0 do byte +6 para campo/cópias e zero para regiões
11–15. Ainda não atribuímos aqui esse bit à posição ataque/defesa.
`0x00570420` retorna bit 1 do mesmo byte para campo/cópias; é coerente com a
máscara de face já seguida no filtro de Legendary Sword. Seus demais ramos
possuem regras de visibilidade próprias e não são equivalentes a ler bit 1
em todas as listas.

A região em torno de 0x005A31DF também escolhe entre campo normal e as duas
cópias e testa se seus índices estão ocupados. O trecho selecionado não contém
uma escrita de captura; não foi apresentado como produtor. A consulta a
0x005A1580 revelou lógica de escolha/validação de alvo, cujo fluxo completo
permanece fora deste fechamento. Próximo passo: localizar a cópia/escrita dos
0x90 bytes e os consumidores de ATK/DEF do contexto de batalha.

Esses resultados orientam a separação entre estado físico, listas e contexto
transitório. Não justificam portar os endereços/seletores para o modelo .NET,
que mantém objetos próprios e continua com combate de demonstração.
