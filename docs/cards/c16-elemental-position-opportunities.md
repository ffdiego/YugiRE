# C16 — P253 Aqua Spirit / Garuda: contrato de oportunidade

SQLite cumulativo v2 (quick_check ok), consulta somente leitura.
Ambas 1517/1519 compartilham candidate 0057B1F0, preparation 0057CD40,
primary 005907D0 e target_preparation 005944D0. Os três primeiros
faltam em decompilations e visual_transient_decompilations; 005944D0
está no segundo e escolhe 00593440 ou 005B3FA0(...,500) conforme
flags de DAT_00a54e5c e lado. Não prova filtro nem execução.

005A4990 percorre slots 0..4 do lado 1-param1, consulta 00569790
com resource 0x5ef (Garuda), chama 005BA490 com chave incluindo
0x820, incrementa cursor DAT_00a57800 e retorna 0 quando emite.
005A76E0 percorre os mesmos slots, consulta 0x5ed (Aqua) e usa
chave 0x20. Não igualar os bits 0x800/0x20 a fases ou ao lock
sem análise do consumidor. Pointers: 005DD02C -> 005A4990;
005DD0A0 -> 005A76E0. A relação tabela/dispatch/fase ainda
precisa do trabalho A07, não foi inferida pelo nome.

00569790 compara resource ID, testa bit 2 (máscara 0x02) em
DAT_00a55d7a e rejeita máscara 0x22 em DAT_00a55e00, depois
exige 0056B440(...) == 0. Confirma uma guarda compartilhada
de estado/supressão, não a semântica completa dos bits.

## Contrato de texto (provisório para Joey)
Fontes públicas oficiais atuais, não o DAT original:
- https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=5253&ope=2
- https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=5255&ope=2&request_locale=en

Aqua: oportunidade opcional na Standby adversária, um monstro
adversário face-up, muda posição e restringe mudança de posição
pelo resto do turno. Garuda: End Phase adversária, um monstro
adversário face-up, muda posição, sem a restrição adicional.
Não confundir a End Phase com Standby. O DAT da versão alvo e
a rotina de execução devem confirmar detalhes históricos.

ElementalPositionRules emite apenas intenção após oportunidade
explícita, não dispara no Main ou em DrawCommand. O produtor
A07/A08 deverá fornecer fase, identidade da fonte e estado de
supressão, controle de oferta por ocorrência e oportunidade;
A06 fornece decisão opcional/seleção revalidada. O comando de
mudança por efeito deve distinguir mudança manual (sem ignorar
Dream Clown ou outros gatilhos) e o lock de Aqua por ocorrência,
limpo ao fim do turno/saída do campo; sem inferir regras de
conflito, chain ou flag nativa. Não reaproveitar ChangePositionCommand
manual sem ajustar seu contrato. Nenhuma mutação nesta fatia.
Ambas permanecem Partial: invocação C16 pronta, efeito não integrado.

## Coleta pendente
Roteiro 17: recuperar callbacks, helpers e trecho de tabela
005DD000. Reutilizar saída de roteiro 11 de ACTIONS para o
dispatch de fases. Não repetir coleta se já existir bundle.
Testes puros cobrem fases distintas, lado, fonte, oferta,
supressão, game over e alvos. CI pendente, sem promoção.
