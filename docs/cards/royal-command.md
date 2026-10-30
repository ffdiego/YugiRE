# Royal Command — supressão de efeitos FLIP

Análise estática em 2026-10-29. Mesmo executável SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Endereços VA, base 0x00400000. Nenhuma execução do jogo ou gravação do banco.
Testes dinâmicos ficam para uma etapa futura no Windows, conforme orientação
do usuário; esta rodada permanece estática.

## Identidade e resultado

O ID **0x5FA / 1530**, pendente no estudo de FLIP, é **Royal Command**,
índice **108**. A descrição local diz que nega a ativação e os efeitos dos
monstros FLIP. O registro **418** contém
`(0x5FA, 0x00588D90, 0, 0, 0, 0)`.

O comportamento aparece em três pontos distintos: validação do pedido,
ramo do callback de Royal Command e verificação anterior à resolução do
monstro. Há também flags globais e por posição para representar a supressão.
A descrição corrobora a interpretação; os desvios e escritas abaixo são
evidência de código separada do texto.

## 1. Validação e criação do pedido

**0x0057EAC0** consulta **0x00567620(índice, -1)** e, para carta reconhecida
nessa categoria, chama **0x005699C0(0x5FA)**. Resultado não zero leva ao
retorno de rejeição. É uma das condições genéricas antes de admitir o pedido.

No produtor condicional **0x00574F70**, estudado na
[etapa anterior](man-eater-flip-activation.md), essa validação pode impedir
que a possibilidade local seja aceita. O produtor ainda emite o comando
de face **0x80**, mas a condição para criar o pedido por 0x005BA490 falha.
Portanto, nesse caminho, virar a carta e admitir seu efeito são decisões
separadas. Outro produtor, **0x00575210**, também exige ausência de Royal
Command num ramo que pode criar pedido após mudar orientação.

0x005699C0 soma os dois lados por **0x00569820**. O contador exige entrada
ocupada, ID correspondente, máscara de face 0x0002 definida e condições sobre
flags de posição; também consulta 0x0056B440. Não equivale a procurar o ID em
qualquer lugar do duelo. A semântica completa de 0x0056B440 fica pendente.

## 2. Callback de Royal Command sobre outro pedido

**0x00588D90** é compartilhado com outras cartas, incluindo Magic Jammer.
Antes dos ramos por ID, exige:

- Flag `pedido_atual+4 & 4` ausente.
- Segundo parâmetro apontando para outro pedido, não nulo.
- Marcador **0x00A55C8A == 0x80** na máquina de resolução.

Para **ID 0x5FA**, o ramo em **0x00588F6E** confere se o índice guardado na
posição indicada pelo pedido atual ainda é o índice de Royal Command. Essa
comparação é por índice, não uma comparação de chave de ocorrência nesse
trecho. Depois classifica a carta do outro pedido com 0x00567620.

Se for FLIP, aplica **`uint8[outro_pedido+4] |= 0x0E`** e termina com AX=0.
O callback de Man-Eater Bug, por exemplo, testa a máscara 4 desse mesmo
campo antes de resolver o alvo e emitir destruição. Assim, há ligação concreta
entre a escrita no pedido anterior e a interrupção do efeito já estudado.

Esse ramo de Royal Command retorna após marcar o pedido. Não chega à chamada
de destruição existente em outros ramos do callback compartilhado. O caminho
é compatível com negação do efeito sem destruir o monstro por essa operação.
As condições que permitem ao jogador responder e a ordem temporal completa
da cadeia não foram demonstradas por execução.

## 3. Verificação antes de chamar o callback do monstro

No **estado 4 de 0x005BB720**, o caminho de monstro reconhecido por
0x00567620 consulta novamente Royal Command. Se o contador for positivo,
ou se a máscara 4 do pedido já estiver definida, emite **0xB1** e salta
para **0x005BB8B6**. Esse bloco escolhe o estado **100**, usado no
encerramento do pedido. Ele desvia dos estados 5/6, onde ficaria a chamada
do callback primário por 0x0059DEB0.

É uma proteção adicional para um pedido que já chegou à máquina de resolução.
Não depende somente de a validação inicial ter bloqueado sua criação.

O comando **0xB1** normaliza para **114 decimal**; stub **0x005B9708**
encaminha a **0x00408730**. Para posição válida e ocupada, o consumidor chama
**0x00570710(lado, posição, 5, 1)** no caso emitido aqui. Esse helper define
**0x20 em uint32[registro+0x8C]**. O consumidor também pode criar um objeto
de apresentação. Portanto, 0xB1 tem escrita de estado, além do caminho visual.

## 4. Flags globais e atualização das posições

**0x00598D70** usa **0x00569B00(0x5FA)** dentro de um ramo sujeito a
condições globais anteriores. O resultado alimenta
**0x005798F0(5, booleano)**, que atualiza por **0x00570670** a máscara
**0x20 em 0x00A577EC** e emite comando 0x0C se houve mudança.
As condições anteriores podem impedir o reconhecimento; não modelamos
todas as interações com os demais IDs desse atualizador.

0x00569B00 soma **0x005699F0** para ambos os lados. Esse contador difere de
0x00569820: no caminho de cartas das posições 5–10, exige face definida e
ausência da máscara 2 em registro+0x8C, sem aplicar todas as verificações
do contador usado diretamente na ativação/resolução. Não são substitutos
intercambiáveis.

Para monstro FLIP numa posição <5, **0x00598C40** retorna verdadeiro quando
a máscara global 0x20 está definida. 0x00598D70 usa esse resultado para
atualizar a máscara 0x20 nas posições ocupadas e viradas para cima, podendo
tanto definir quanto limpar o estado por posição.

Há, portanto, campos distintos com papéis relacionados:

| Campo | Operação observada |
|---|---|
| Global 0x00A577EC, máscara 0x20 | Estado global calculado para supressão FLIP |
| Posição+0x8C, máscara 0x20 | Estado da carta/posição atualizado pelo consumidor e recomputação |
| Pedido+4, OR 0x0E | Marca o pedido afetado no ramo de resposta de Royal Command |

Isso não estabelece equivalência universal entre cada bit dessas máscaras.
O código mantém verificações locais e globais, em momentos diferentes.

## Reprodução e limites

```bash
PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_royal_command.py
```

Esperado: onze linhas `PASS`. Revalida preservação dos originais/banco,
identidade e registro de Royal Command, trechos de instruções e despacho
de 0xB1. **São verificações estáticas; nenhum ramo foi executado no jogo.**
Timing, janelas de resposta e interações com outras cartas permanecem futuros.

Evidências: [verificações e helper de bloqueio](../evidence/flip-suppression-checks.txt),
[flags globais](../evidence/royal-command-global-flags.txt),
[desvio e consumidor 0xB1](../evidence/royal-command-command-b1.txt),
[escritas e contadores](../evidence/royal-command-state-writes.txt),
[callback compartilhado](../evidence/royal-command-shared-callback.txt),
[instruções selecionadas](../evidence/royal-command-selected-instructions.txt),
[relatório](../evidence/royal-command-validation.json).
