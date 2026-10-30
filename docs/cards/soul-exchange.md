# Soul Exchange — tributo adversário e restrição de combate

Análise estática do EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
VA com base 0x00400000. Sem execução do original ou alteração persistente do
banco. Identidade: índice 264, ID 1220/0x4C4, registro 309 em 0x005EEDA0.
A descrição inglesa local fala em usar monstro adversário como tributo e
pular a Battle Phase do turno de ativação.

## Slots e seleção

| Slot | Função | Trecho seguido |
|---|---|---|
| Principal | 0x0058E160 | Guarda, flag do jogador e associação no alvo |
| Candidato | 0x00579C40 | Lado adversário, posição e elegibilidade |
| Disponibilidade | 0x00582080 | `(byte[0x00A577FA] & 0x0E) == 4` |
| Preparação | zero | Sem callback nesse slot |
| Alvo | 0x005944D0 | Seletor interativo ou automático |

0x00579C40 recebe lado/posição empacotados. Rejeita lado próprio, posição
maior que 4, falha de 0x0056C510 e posição vazia. Há um ramo adicional para
origem de classe 0x16 usando 0x0056C460, pois o filtro é compartilhado. O
filtro mostrado não exige face aberta, ao contrário do filtro de candidato
adversário do Ritual. Não transportar automaticamente uma restrição de um
consumidor para outro.

0x005944D0 escolhe 0x00593440 para o modo interativo ou 0x005B3FA0 com
parâmetro 500 no outro modo. O protocolo completo desses seletores não foi
expandido neste caso.

## Escrita que permite tributar o alvo

0x0058E160 obtém o primeiro alvo por 0x00583440, consulta a guarda
0x00583510 e chama 0x00579930(ator, 18, 1). Só depois testa pedido+4 bit 4
e resolve a ocorrência com **0x00583470**. Essa ordem é relevante: não
condicionamos a marca de combate ao sucesso da associação final.
O callback não repete explicitamente 0x00579C40 nessa sequência.

Com a ocorrência válida, chama 0x005747E0 e emite **0x86**:

| Palavra | Valor no caso Soul Exchange |
|---|---|
| 1 | Opcode 0x86 e bit de lado |
| 2 | Índice de Soul Exchange, não coordenada de carta no campo |
| 3 | `lado_alvo | posição_alvo << 8` |
| 4 | `3 | ator << 8` |

O consumidor 0x00571BC0 chama **0x0056AB60(palavra3, palavra2, palavra4)**,
acrescentando a entrada à lista do monstro. O tipo baixo é **3**, e o byte
alto dos metadados é o ator autorizado. Isso não é um vínculo físico com a
zona onde a magia esteve, nem uma transferência de controle do monstro.
O retorno baixo do callback é zero; os bits altos do retorno decompilado
não são tratados como contrato.

0x0056A140, usado na seleção de tributos, permite posições próprias após
suas guardas. Para posição adversária, procura **0x0056DA10(lado,pos,0x4C4)**
e compara o byte alto da associação encontrada com o ator. 0x0056DA10 aceita
tipos **3, 11, 12 e 13**, converte o índice guardado para ID e retorna a
primeira correspondência. Não busca uma segunda correspondência se o
chamador depois recusar o byte de ator.

Correção da anotação inicial de Rituais: a busca NÃO exige tipo 4. O listing
0x0056DA56–0x0056DA63 confirma tipo 3 ou intervalo 11–13; Soul Exchange grava
3 neste caminho. O documento de Rituais foi corrigido junto a este estudo.

## Restrição de combate

0x00579930/0x005706B0 marca **bit 18, máscara 0x40000**, em
`0x00A56430 + lado*0xD44`; quando o valor muda, também emite 0x0D.
**0x005A0DD0** retorna falso se esse bit estiver definido. Sem ele, verifica
outro estado global e monta candidatos por **0x005A0D60 → 0x0056B730**.
O resultado é usado pela interface 0x00481A20, que decide uma opção de
transição, e pela rotina 0x005B0A50 que percorre escolhas de ataque.

A associação do bit com a restrição de combate é sustentada pelo produtor,
esses consumidores e pela descrição. Não demos nomes globais a todos os
estados numéricos da interface nem fechamos todo o controle de fases.
O helper 0x005A4010, consultado como possível reset, na verdade também marca
o bit; não é evidência de limpeza. A [continuação de limpeza](../19-temporary-state-cleanup.md) identificou
o comando 0x02 que limpa o bit e a associação. O agendamento temporal desse
comando, necessário para fechar a duração por turno, permanece aberto.

## Validação e limites

`python3 scripts/validate_soul_exchange.py` verifica fingerprint, registro de
seis DWORDs, cinco sequências de instruções e cinco alvos de CALL. Resultado
em [soul-exchange-validation.json](../evidence/soul-exchange-validation.json).
Não executa funções e não valida todas as interações de Soul Exchange.

As quatro entradas não estavam definidas como funções no banco. Foram
criadas/decompiladas em transação temporária, descartada ao terminar; nova
consulta sem criação voltou a não encontrar as funções. O processo headless
também usou `-readOnly`.

Evidências principais: [decompilação temporária](../evidence/soul-exchange-transient-functions.txt),
[instruções de entrada](../evidence/soul-exchange-entry-instructions.txt),
[produtor](../evidence/soul-exchange-association-producer.txt),
[consumidor e byte do ator](../evidence/soul-exchange-association-consumer.txt),
[predicado de combate](../evidence/soul-exchange-battle-restriction.txt),
[interface e ataques](../evidence/soul-exchange-battle-entry-consumers.txt).
As buscas auxiliares de duração preservam pistas, sem concluir que um helper
com nome de relatório “reset” faça efetivamente a limpeza.
