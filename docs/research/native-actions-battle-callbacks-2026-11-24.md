# A06 — callbacks de batalha recebidos (24/11/2026)

Coleta privada `ghidra-actions-battle-callbacks.zip`, SHA-256
`a090c44925b0e34b4021560f920ea25c656e4519ab9e90271e57bd5686f6b19b`,
fingerprint EXE conforme guia. 7/7 VAs decompiladas e assembly exato
nos retries; 7 janelas iniciais deslocadas não indexadas. Importada
em v4-actions, consultar [guia](joey-knowledge-sqlite.md). Sem alteração
de comportamento C# nesta consolidação.

## P164 — recrutamento elemental

- 00581770: chama 00570B40 com lado extraído de +2. Na base anterior,
  00570B40 e 00570AF0 testam restrições de Special Summon; IDs
  1426/1510/1526 correspondem a Jam Breeding Machine, Jowgen e
  The Last Warrior from Another Planet. Não generalizar a todos
  os efeitos nem ao escopo de campo isolado.
- 0058AC10: após testar máscara 0x04 em +4, despacha
  byte 00A55C8A menos 0x7B, domínio 0..5, jump table 0058ADAC.
  O ramo 0x80 checa slots livres (0056A000), disponibilidade
  (00570B40), candidatos (00599E30); caminho automático por flags
  00A54E5C usa 005AF5A0, humano pede 0x2F por 005BF800/005BF9A0
  e retorna 0x7F. Sem candidatos há ramo de retorno 1 e, no modo
  humano, mensagem 0x15. Ramo 0x7E consulta 00A558B4; 0x7D
  chama 0040C940 com modo 6 e resource; 0x7C retorna 0x7C;
  0x7B chama 005B9170 com command 0x60 (seed tratada em PR #138).
  Ramo de entrada 0x7C/0x7D etc. é estado do callback, não prova
  de opcode da chegada ao GY. Não atribuir ordem de execução pela
  numeração das labels; produtor/driver e empate precisam de trace.
- A seleção nativa tem gates e caminho humano/automático distintos.
  A implementação C# P164 já oferece o fluxo lógico, não replica
  o contador do callback. Não alterar ordem de shuffle ou mapear
  7C do GY por esta evidência isolada.

## P087 — Penguin Soldier

0057A930 valida slot 0..4, chama 0056C510 e testa os 12 bits de
ocupante na tabela 00A55D74. Candidate também serve P072; não
confundir candidato genérico com filtro completo de retorno.
00594200 prepara até dois alvos; ramo automático checa lado
oponente e pode incluir a própria carta, enquanto humano percorre
subestados 0..3, consulta 0059DC50/0059DCA0 e evita duplicata
comparando 00583440 antes do segundo registro. Cancelamento do
segundo alvo tem caminho de retorno 1. 005861F0 testa máscara
0x04, lê contagem em +4 >> 13, limita a dois e valida cada
ocorrência por 00583470 (identidade codificada) antes da máscara
lado*16 + slot. Chama 00577A40 -> 00576960 com operação 0xB,0.
A exclusão de alvo marcado por batalha é comportamento observado
e testado no sandbox, mas ainda não isolada em uma condição única
dessas rotinas; não concluir que o candidate sozinho a garante.

## P118 — Skelengel

00593CA0 registra o lado em 00592A50, retorna 1.
005887C0 testa máscara 0x04 em +4, obtém resource por 004022E0:
539 (Skelengel) e 1447 (Jar of Greed) passam 1, 1010 (Pot of Greed)
passa 2 a 00578B60; lado vem de +6 & 1. O helper chama 00578860,
que emite comandos 0x61 para a compra. **Não aparece um prompt
de confirmação nesses dois corpos**; a escolha opcional no sandbox
continua uma hipótese de domínio/política de laboratório. O driver
de ativação pode ter uma etapa anterior. Não chamar obrigatória
nem opcional nativa sem validar dispatch e observação. Não mudar
a política existente somente com esta coleta.

## Limites e continuidade

Roteiro 31 encerrado; 7/7 incorporadas à base, não solicitar novamente.
PR #138 em paralelo estuda a fronteira de shuffle nativo e sua seed.
O presente handoff não altera seu código ou documentos em disputa.
Confirmar ordem dinâmica, aceite/recusa do FLIP P118, posição/seleção
e comparação de replay com original em cenário dirigido quando necessário.
