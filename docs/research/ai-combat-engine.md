# Avaliador de combate da IA em C#

## Estado desta sessão
Implementação incremental independente do processo Joey. A parte de ranking
já validada com15 candidatos/7seletores recebe agora um produtor de eventos.
O código não é usado pelo DemoDuel nem resolve dano real deLP.

Baseline antes da mudança:454checks C#,16testesPythonai* e13decode_ai*;
buildsemwarnings. EXEfixado:db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Todas asreferências abaixo sãoVA,base00400000. Nenhum original é executado.

## Confronto puro — 005B0F40
`NativeAiCombat.Evaluate` recebe dois `NativeAiMonster`, slots e um
`NativeAiCombatContext` explicitamente resolvido. Atributos são blocos de
12bytes (`NativeAiStats`); ID de recurso é separado do índice interno.
Um contexto default é desconhecido e retorna `IsSupported=false` com razão.
`ResolvedNoExternalModifiers` é uma declaração do caller de que todos os
predicados externos são falsos, **não** uma dedução a partir de um buffer.

Implementado:ATK/ATK,ATK/DEF,empates/zero,remoções,scoreint16,flags e slots;
ordem nativa deproteção/supressão; exceções locais01DD/04E5/011F/053D/0199/
04CF/04E3; contagem Metalmorph resolvida e perfuração porlado deorigem.
O cálculo não resolve vínculos,legalidade ou todos osatributos deum duelo.
Fontes:[relatórioF40](../evidence/ai-decision-combat-deep.txt), seçõesF40 e
ADD60/ADDC0, e [assembly](../evidence/ai-decision-control-flow.txt).

A comparação usa int32 comwrap nas operações locais; score trunca a diferença
para int16. Um score negativo ainda pode ser útil quando há remoção rival
sem perda própria. Autilidade não deve ser interpretada como dano real deLP.
`InitialFlags` conserva bits0xF005 exatamente; sem o seed original, só é
possível confrontar todos osbits calculados e os16bits de score.

`SpecialTarget` reproduz a construção do caminho especial de005B0D90 após
osgatesBF20/BFD0/B05A0. Não decide que o caminho é permitido. O critério de
retorno usaATKtruncadoint16>0, semrecalcularbit deutilidade. O descritor
selecionado0600E803 observado no trace detalhado resulta deATK1000,slot0
combit4 do loop externo. Targetbits dessa rota podem ser scratch preservado.

## Validação e proveniência
EtapaA:531checks C# passaram (454baseline+77checks novos), incluindo22
buffers nativos em layout/signedscore/utilidade e vetores sintéticos para
ramos,slots,truncamento e ordem dasexceções. O caso observado25 tem inputs
numéricos550/GeminiElf1900 contra24/AxeRaider1700 e output0808C800. Sob
contexto neutro e postura deataque **declarados**, o novo cálculo gera esse
buffer; a captura não contém todos ospredicados para provar a ausência
deefeitos. Isso é compatibilidade condicional, não replay completo docampo.

O corpus novo `ai-combat-differential.json` preserva22outputs independentes,
14comatributos calculados pareados e8incompletos. **Zero**contextos completos
foram observados: faltamposição,flags,vínculos e seed deoutput. Nãoinferir
inputs ausentes a partir doexpectedbuffer. Capturas recentes de gates/detail
não estão noFedora; asvalidaçõesdocumentadas seguemválidas comsuaslimitações.

## Continuidade
Próximos incrementos: subconjunto deatributos com fontesDAT; seleção dealvos,
sortnãoestável,máscara/remoções/soma; hipótese de candidato seguida doranking.
Cada etapa deve propagarunsupported e manter osdados nativos capturados
separados decontextos sintéticos declarados. Ver
[checkpoint operacional](ai-combat-checkpoint.md).
