# Núcleo de comparação e retirada em batalha

Análise estática do EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
VA, image base 0x00400000. Nenhuma batalha foi executada no original.
O estudo cobre o núcleo comum e sua cadeia; vários modificadores específicos
por carta continuam fora do fechamento.

## Atributos calculados e contexto

**0x005B7C50** consulta 0x0056DD30 e prepara dois blocos de 16 bytes nas bases
0x00A57844 e 0x00A57854. Os acessos escolhem os blocos pelo bit do atacante
em 0x00A57840, com o bloco complementar para o alvo:

| Offset no bloco | Conteúdo seguido |
|---:|---|
| +0, uint16 | Lado/posição, postura, flags de resultado e chave no byte alto |
| +2, uint16 | Índice da carta |
| +4, int32 | ATK calculado |
| +8, int32 | DEF calculada |
| +12, uint32 | Dano associado ao bloco; destinatário pode ser alterado por flags |

O descriptor inclui bit 5 derivado do bit 0 de registro+6. O cálculo seguinte
usa DEF quando esse bit 5 está definido; isso identifica a postura de defesa
no contexto examinado. Bit 1 de registro+6 continua distinto: é a face aberta.
A rotina trata modificadores específicos de confronto e limita atributos
negativos a zero antes de gravar os resultados.

**0x005B8210** limpa flags antigas de resultado com máscara 0xFF2F e zera os
campos de dano. O bit **0x10** no descriptor é usado para marcar destruição.
O bit global 0x04 de 0x00A57840 seleciona o caminho de ataque direto.

## Comparação comum, sem exceções de cartas

Sejam A o ATK do atacante e B o ATK ou DEF usado pelo alvo:

| Confronto | Destruição marcada | Dano básico |
|---|---|---|
| Ataque contra ataque, A > B | Alvo | A-B ao lado do alvo |
| Ataque contra ataque, A < B | Atacante | B-A ao lado do atacante |
| Ataque contra ataque, A = B > 0 | Ambos | Zero |
| Ataque contra ataque, A = B = 0 | Nenhum | Zero |
| Ataque contra defesa, A > B | Alvo | Zero no caso comum |
| Ataque contra defesa, A = B | Nenhum | Zero |
| Ataque contra defesa, A < B | Nenhum | B-A ao lado do atacante |
| Ataque direto | Nenhum alvo | A ao lado do alvo |

A condição do empate positivo aparece diretamente em **0x005B82EC**:
TEST ECX,ECX seguido de JLE para sair sem marcar destruição. A superioridade
contra defesa passa por verificações específicas de perfuração, incluindo
IDs 0x53D, 0x521 e 0x604; a tabela acima exclui essas condições.
Os danos são limitados a **65535**, e depois há outros filtros, imunidades,
redirecionamentos e exceções por ID. Não apresentamos a tabela como resolução
completa de qualquer batalha ou como tratamento de todos os efeitos.

O protótipo tratava empate zero como qualquer outro empate de ataque.
Uma regressão C# reproduziu a divergência antes da correção. A regra foi
ajustada para preservar ambos sem dano. A limitação de um ataque por turno
continua sendo política do protótipo, não uma conclusão desse trecho nativo.

## Etapas e estado separado

**0x005A4090** indexa a tabela em **0x005EFA68** pelo byte 0x00A577FB.
Ao concluir um elemento, incrementa esse byte e zera o byte baixo de
0x00A577FC, usado como continuação dentro do elemento. Isso é outro seletor,
distinto de 0x00A577FE da sequência de turno e do byte privado do callback.
Os elementos 7–10 são:

| Índice | Rotina | Parte seguida |
|---:|---|---|
| 7 | 0x005A2740 | Atributos, comparação, apresentação e retirada do campo |
| 8 | 0x005A2990 | Respostas específicas e encaminhamento do dano a LP |
| 9 | 0x005A2D60 | Pedidos/eventos intermediários |
| 10 | 0x005A31A0 | Consequências e encaminhamento final das cartas |

O índice e a ordem são comprovados; nem todos os ramos dos elementos 8–10
foram interpretados. O argumento transmitido aos handlers identifica o lado
atacante no caminho examinado.

## Apresentação, cópias e LP

No elemento 7, 0x005A2740 emite **0x30**, calcula atributos com argumento 1,
calcula dano e emite **0x31**. O consumidor de 0x30 chama os cálculos com
argumento 0. O de 0x31 é **0x00407A20**, que prepara apresentação de danos por
0x00413BA0 → 0x004140B0 e consulta sua conclusão por 0x00413B80. O setter de
apresentação grava dados/código de estado em objeto. Essa preparação não é
a chamada explícita de atualização de LP que foi seguida depois.

Para cada marca 0x10, o elemento 7 emite **0x3C**. Seu consumidor,
**0x00405020**, em uma etapa posterior à apresentação de movimentação:

1. Executa limpeza de referências por 0x0056A490.
2. Copia **0x90 bytes** do registro do campo para uma cópia temporária.
3. Zera o registro físico do campo e limpa a marca global da posição.

A terceira palavra do comando escolhe cópia **0 = atacante**, **1 = alvo**:
**0x00A57864 + seletor*0x90**, portanto 0x00A57864 e 0x00A578F4.
Essas cópias seguem o papel no confronto, não simplesmente o número do lado.
Esse é o produtor das cópias que faltava ao estudo anterior.

No elemento 8, há caminhos de resposta específicos antes do dano. No estágio
1, **0x00578540** recebe valor/destinatário, ignora zero, limita o valor e
emite **0x43** com as outras palavras 0 e 1. O caminho comum de 0x43 já foi
seguido até 0x00408470(0), delta por 0x0047C4F0, atualização de objeto por
0x0047C560 e **0x00573FB0**, que atualiza LP com saturação em zero. Não atribuímos ao 0x31 essa escrita só porque mostra dano.

## Destino final depois da retirada

0x005A31A0 escolhe entre registro físico e cópia conforme a marca de
destruição; confere também ocupação das cópias e chaves das posições. Seu
caminho de retirada chama **0x00577CF0**, que emite **0x7C**. Conforme estudo
anterior, o consumidor **0x00405190** parte da cópia, prepara o destino e
insere pela movimentação compartilhada. O caso comum vai à região 14;
Banisher e fichas possuem desvios, além de gatilhos e limpeza de equipamentos.

Assim, desaparecer do campo e chegar ao cemitério são etapas distintas.
Efeitos intermediários podem ter registros temporários como fontes mesmo
com a posição física vazia. Isso esclarece o motivo das consultas às cópias
no caso de Cyber Jar, mas não fecha todos os seus cenários ou o timing FLIP.
A ordem lógica é sustentada pelas tabelas, comandos e prioridades do
agendador; tempo de animação e sequência visual permanecem sem teste.

## Validação e limites

`python3 scripts/validate_battle_core.py` confere fingerprint, sequência de
quatro handlers, dois despachos, nove transferências e cinco sequências de
instruções. [Relatório](evidence/battle-core-validation.json).
Não executa funções nem valida todas as exceções do combate.
O teste de empate zero passou junto às 55 verificações C# e à compilação;
nenhuma janela foi aberta neste ambiente.

[Atributos](evidence/battle-result-calculation.txt),
[comparação e exceções](evidence/battle-damage-and-destruction-flags.txt),
[sequência inicial](evidence/battle-comparison-caller.txt),
[instruções da comparação](evidence/battle-core-comparison-instructions.txt),
[consumidores](evidence/battle-damage-command-consumer.txt),
[setter de apresentação](evidence/battle-presentation-setup.txt),
[LP e destino final](evidence/battle-life-application-and-final-routing.txt),
[etapas e indexador](evidence/battle-post-comparison-and-driver.txt),
[cópia e chamada de LP](evidence/battle-snapshot-write-and-life-call.txt).
