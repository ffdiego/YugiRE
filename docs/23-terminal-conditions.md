# Condições centrais de encerramento

Análise estática do mesmo EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
VA, image base 0x00400000. Sem execução de vitória/derrota no original.

## Exodia é uma regra central, não um callback das cinco partes

**0x005BBF70(lado)** consulta **0x0056D2C0** para IDs de recurso
**16, 17, 18, 19 e 20**, exigindo contagem positiva de cada um.
0x0056D2C0 percorre somente a mão: byte de contagem em 0x00A55D68,
array de DWORDs em 0x00A56434, com stride de lado 0xD44, conversão índice→ID.
Não testa posição no campo nem exige exatamente uma cópia.

As identidades foram conferidas nos recursos locais CARD_ID/CARD_Name:

| ID | Índice | Carta |
|---:|---:|---|
| 16 | 777 | Right Leg of the Forbidden One |
| 17 | 775 | Left Leg of the Forbidden One |
| 18 | 778 | Right Arm of the Forbidden One |
| 19 | 776 | Left Arm of the Forbidden One |
| 20 | 774 | Exodia the Forbidden One |

**Nenhum desses cinco IDs está nos 443 registros da tabela de callbacks.**
A verificação é chamada para os dois lados por **0x005BC050**, que grava a
condição no bit **0x200** do word 0x00A55D6C de cada lado.
Isso é um exemplo concreto de por que ausência de callback não significa
ausência de efeito e por que o catálogo não equivale a todo o motor de regras.

## Destiny Board

**0x005BBFD0(lado)** exige:

- Contagem válida do ID **1528/0x5F8, Destiny Board**, via
  0x005699A0 → 0x00569820. Esse caminho testa ocupação, face, flags de efeito
  e 0x0056B440 no bloco examinado.
- Presença de IDs **1541–1544/0x605–0x608**, Spirit Message I/N/A/L, por
  **0x00569720**. Esse helper procura posições 5–10, ocupadas e de face aberta.

Os critérios dos dois helpers não são idênticos. O de letras não repete
explicitamente todos os testes do contador de Destiny Board.
O predicado consulta cada ID, sem testar sua ordem física nas zonas.
A produção das letras e suas demais regras não foram seguidas neste estudo.
0x005BC050 grava o resultado no bit **0x400** do word de cada lado.

## Falta de cartas, condições especiais e LP

No caminho local examinado de **0x005BC050**, a ordem dos testes é:

1. Marcas **0x100** de falta de cartas, produzidas pelo consumidor de compra.
2. Predicado das cinco partes de Exodia, gravando **0x200**.
3. Predicado de Destiny Board/letras, gravando **0x400**.
4. LP dos dois lados; se ambos forem diferentes de zero, continua o duelo.

Havendo condição de encerramento, prepara bits 6–7 de 0x00A577FA e marca
0x00A577F4 bit 0x10. O código preserva casos de ambos os lados satisfazerem
uma condição e de ambos terem LP zero. A perspectiva usa o seletor local de
0x00A54E5C, distinto do bit de lado do turno; os valores altos vão para o
handler seguinte, que emite notificações/resultado. Não foram jogados casos
simultâneos e não afirmamos uma prioridade universal de todas as interações
somente pela ordem dessa função. O agendador controla quando ela é chamada.

## Evidências e validação

[Predicados](evidence/duel-terminal-condition-predicates.txt),
[regiões consultadas](evidence/duel-terminal-condition-locations.txt),
[ordem e resultado](evidence/native-draw-special-kinds-and-terminal-checks.txt).
As identidades locais usam os recursos já fingerprintados; o mapa CARD_ID
foi relido em 2230 bytes e conferido com SHA256 conhecido durante a rodada.

`python3 scripts/validate_native_draw.py` foi ampliado: além de compra,
confere cinco IDs nas instruções de Exodia, dez CALLs dos predicados e a
ausência dos cinco IDs na tabela nativa de callbacks. O relatório continua
em [native-draw-validation.json](evidence/native-draw-validation.json).
São verificações estáticas; Exodia e Destiny Board não foram implementados
no protótipo nesta rodada.
