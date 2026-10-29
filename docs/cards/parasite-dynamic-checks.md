# Parasite Paracide — experimentos dinâmicos pendentes

Plano para uma sessão futura assistida pelo usuário. Nenhum experimento foi
executado. Usar cópia dos arquivos do jogo; alvo SHA256 da
[análise consolidada](parasite-paracide.md). Endereços abaixo são VA preferenciais;
se a base carregada mudar, calcular `base carregada + (VA - 0x00400000)`.

Comparar regiões pequenas e capturar pilha/argumentos nos breakpoints. Um dump
geral antes/depois não estabelece a ordem das escritas. O alvo é x86/PE32.

| Experimento | Ação no jogo | Pontos de observação | Resultado esperado / hipótese refutável |
|---|---|---|---|
| Ativação | Virar Parasite para cima e resolver seu efeito | 0x005875A0, chamada em 0x00587623; pedido de 20 bytes | Primeiro ramo, destino no bloco oposto, nibble 13 e vínculo extraído de pedido+4 |
| Embaralhamento | Completar a ativação | 0x00408B90, 0x00568D80; sequência 0x00A56574 + bloco*0xD44 | Mesmos elementos após permutação; ocorrência de Parasite preserva sua chave |
| Compra | Comprar a ocorrência inserida no deck | 0x00578981, 0x0059DEB0, 0x0058766E | Pedido 0x365F, mesma chave e estados 0x80/0x7F/0x7E |
| Colocação | Resolver a compra com posição livre | 0x004063E0 e 0x00568610; registros de passo 0x90 | Carta sai da origem e aparece em posição 0–4 face-up em defesa |
| LP | Concluir a resolução da compra | 0x00573FB0; uint16 em 0x00A55D64 + bloco*0xD44 | LP reduzem 1000, saturando em zero; confirmar bloco afetado |
| Tipo dinâmico | Manter outro monstro face-up e depois retirar/virar Parasite | 0x0056F6F0; registrar argumentos e retorno | Classe 10 enquanto a condição vale; deixar de receber essa alteração após seu término |
| Limpeza | Resolver e também tentar um pedido rejeitado | 0x0057CCC0, 0x00571BF0; arrays em 0x00A563B0/F0 | Zerar a posição associada, sem presumir compactação ou redução do contador |
| Campo cheio | Comprar com cinco posições ocupadas | 0x005876A3, 0x0058771B, comando 0x79 | Distinguir caminho alternativo e confirmar destino visível, ausência/presença de dano |
| Negação/interrupção | Negar ou remover a fonte antes da resolução | Callbacks e arrays acima | Confirmar quais validações impedem execução e quando o vínculo é removido |

Para cada caso, registrar configuração do cenário, cartas envolvidas, base
carregada, endereços/argumentos, dumps antes/depois e observação na tela.
Classificar separadamente resultado observado, inferência e caso não reproduzido.
Não alterar registradores, código ou memória no primeiro ensaio: observar o
comportamento original. Mudanças deliberadas exigem um experimento separado.
