# Lote 14 — P252 Dark Necrofear: custo e controle

Status: Partial. Custo implementado no Core; seleção interativa, CPU e efeito
posterior não implementados. Identidade SQLite 1514/513, classe 3, perfil P252.

## Evidência estática
0059DEF0 retorna 3 para 0x5EA, 2 para 0x5EB e 1 para 0x5EC..0x5EF.
00599E30 filtra cemitério próprio, FUN_004025D0 == 3, para 0x5EA.
005AB640 valida contagem e espaço; consulta 0x60F (Spirit Elimination),
com caminho alternativo que procura monstros no campo. Não implementar
essa substituição enquanto Spirit Elimination não estiver modelada.
0059DF40 chama 0059DEF0, filtra classe 3 e consome escolhas uma por vez.
O Core usa uma transação de custo completo; não reivindica timing da UI.
0057A470, candidato P252, chama 00579CE0 (ramo 0x5EA compara lados)
e 0056C510 (ocupação/condições do alvo, inclusive 0x2E4).
00583050, 005944D0 e 005906B0 estão catalogados, mas não decompilados.
00576F40 contém gate de 0x5EA com bits de origem/remoção e comparação
de lado; 00577CF0 e 00579330 também consultam 0056B440 (Prohibition)
e chamam 005783C0/00579790. Não inferir semântica completa desses bits.

## Contrato implementado
GraveyardBanishSummonRules valida três posições distintas, classe 3,
identidade dos candidatos e origem. DemoDuel revalida mão e zona livre,
remove os custos do GY para a zona banished compartilhada, coloca
Necrofear face-up e emite evento MonsterSpecialSummoned. Não consome
Normal Summon. Não expõe ainda ação na UI/CPU; não é carta completa.

## Próximo contrato, antes de implementar o controle
- FieldExitEvent: ocorrência, proprietário, controlador, causa, jogador
  responsável, origem do efeito, destino e Turn. Distinguir tributo,
  banimento, destruição própria e adversária, e envio sem destruição.
- EndPhaseRequest: registrar somente destruição adversária que chega ao
  GY; revalidar que a mesma ocorrência continua lá na End Phase.
  Reviver ou retirar a carta invalida o pedido.
- ControlLink: equipamento (origem), ocorrência alvo, proprietário
  imutável, controlador atual, zona original e destino válido.
  Mudar controlador preserva estado relevante, vínculos e identidade.
  Ao perder equipamento, devolver controle ou aplicar regra da época
  para campo cheio; qualquer saída vai à zona do proprietário.
- Necrofear equipada é carta S/T; destruí-la nessa forma não pode
  enfileirar novamente seu efeito como Monster Card.

Os rulings históricos e texto oficial apoiam os requisitos de
destruição adversária e permanência no GY. A ordem nativa, escolha
de alvo, campo cheio, remoção de equipamento e interação com outras
cartas de controle ainda precisam de evidência.

Fontes externas: https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=5222&ope=2
e https://yugioh.fandom.com/wiki/Card_Rulings%3ADark_Necrofear

## Pendência
scripts/completed/10-dark-necrofear-control-ghidra.sh (coleta importada no SQLite cumulativo).
