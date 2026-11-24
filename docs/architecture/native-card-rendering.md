# Contrato de desenho de cartas

Alvo local: `joey_pc.exe`, SHA-256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Os endereços abaixo são virtuais. A conferência usa o Ghidra em modo
somente leitura, os bytes do PE e o vídeo de duelo em torno de 00:43.

| Propriedade | Valor/fórmula nativa | Endereço | Confiança |
| --- | --- | --- | --- |
| Lado | Lado relativo = bit 0 do descritor XOR bit 0 de `A54E5C` | `00418AC0–00418AE3`, `0045C280` | Estático confirmado |
| Orientação própria | 0° em ataque no ramo normal | `00418A80`, `00418EF0` | Estático confirmado |
| Orientação adversária | 180° em ataque no ramo normal | `00418A80`, `00418EF0` | Estático confirmado |
| Defesa | Soma um quarto de volta. ângulo interno = `(2 × lado relativo + bit14) × 128`. Draw converte cada 128 unidades em 90°. O ramo com `obj+21h` ativo força zero em `00418A80`. | `00418AD8–00418AE6`, `00418EF0` | Estático confirmado |
| Face | Bit 15 tem trilha de transformação separada no campo `obj+48h`. O ângulo de lado e defesa fica em `obj+40h`. | `00418AF6–00418B06`, `00418B60` | Estático confirmado |
| ATK/DEF próprios | Topo = `centroY + 46 − trunc(alturaDoGlifo / 2)` | `00419612–00419630`, `004801E0` | Estático confirmado |
| ATK/DEF adversários | Topo = `centroY − 46 − trunc(alturaDoGlifo / 2)` | mesmos | Estático confirmado |
| Origem do sprite | Draw lê o centro em `obj+30h/+34h`, consulta dimensões via virtual `+38h` e deriva canto subtraindo metade. | `005DA868+0Ch → 00418EF0 → 004AD9C0` | Estático confirmado |
| Dimensões no campo | 50 × 72 em pé no vídeo e no verso pequeno do DAT. Defesa troca os eixos na projeção. | `mini/card_ura.bmp`, vídeo 00:43 | Visual observado |
| Centros dos monstros | X 369…681, passo 78, lado oposto em ordem inversa. Y 352 / 248. | `0045C2CA`, `005DB560` | Estático confirmado |
| Centros de magias/armadilhas | Mesmos X e ordem, Y 460 / 140. As entradas efetivas começam em `005DB510`, pois o índice já inclui a base de região 5. | `0045C2E0`, `005DB510` | Estático confirmado |
| Centro de magia de campo | (286,353) / (766,236) | `0045C2EF`, `005DB5E0` | Estático confirmado |
| Escala e projeção | `obj+44h` em x16.16. Draw tem caminho direto sob escala unitária e ângulos nulos, e caminho transformado com consulta à tabela x16.16 em `A52EE8`. | `00418EF0`, `00422A20` | Estático confirmado |
| Tipografia usada hoje | Texto horizontal do raylib, corpo 10, par centrado. O espaçamento exato entre os dígitos e o clipping nativo ainda não foram portados. | cliente | Aproximação |

## Caminho numérico

`004182E0` passa `duel/font/number.txt` ao carregador `00413DB0`
e prepara o objeto em `005F33EC`. O manifesto lista as imagens
`duel_fonts.bmp`, `duel_fontm.bmp` e variantes de cor, além de outros
recursos numéricos. O Draw da carta chega a `004194D0`, que extrai
lado, região e slot, testa visibilidade e chama `0056DD30` para os
atributos do estado atual. A comparação com os valores-base seleciona
variantes de dígitos. A defesa altera a ênfase dos dois valores. O
desenho numérico passa por `00480500` e `00480300`, recebendo X/Y
sem o ângulo da carta. Os dois valores compartilham Y. A referência
horizontal é o centro da carta em `obj+30h`, com os valores distribuídos
em torno da barra com base no passo configurado do recurso.

## Escopo de integração

`DuelCardLayout` compartilha centros, giro e retângulos entre cartas
assentadas, chegada ao campo, ativação e início da destruição. O raio
de clique pertence ao cliente. A interpolação existente em `CardTravel`
mantém a aproximação da tabela de cossenos. A mão continua usando seu
contrato anterior. Os testes percorrem ambos os lados, ataque e defesa,
face-up e face-down. O vídeo confirma os números legíveis em ambos os
lados. As oito combinações não foram pareadas quadro a quadro com o
original.
