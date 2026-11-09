# YugiLab → Joey.Core: fixtures diferenciais automáticos de invocação normal

**Data:** 2026-11-08. **Status:** implementado no repositório, ainda sem validação com Joey real no Windows.

## Objetivo e escopo

Criar fixtures JSON automaticamente a partir de capturas YugiLab, para executar a mesma ação de invocação em C# e comparar os **campos nativos efetivamente observados**. Não é um replay integral de duelo, um save-state ou uma prova de equivalência de toda a engine.

A instrumentação adicional é passiva e executa na thread original:

1. Na entrada de FUN_005B6FF0, observa a mão, os cinco slots de monstro dos dois jogadores, LP e flags do lado ativo.
2. Na emissão de CE em FUN_005ABD00, verifica o mesmo lado, posição de mão, índice interno da carta, stage e thread. Exclui set, tributo e slots ocupados.
3. No retorno da atualização FUN_005BC700, só se houver candidato pendente, observa a mudança completa. Expira após 1500 updates ou qualquer estado incompatível.

Para gerar summon_transition/confirmed são exigidos: remoção da carta correta da mão com as demais em ordem; ocupação de exatamente uma zona previamente vazia; ausência de alterações nos outros slots de ambos os lados e no LP ativo; flag de invocação 0x20000 consumida; CE com contexto compatível, stage 37 e na mesma thread.

Os snapshots não são atômicos. O evento expressa observação consistente em pontos de execução diferentes, **não causalidade universal nem legalidade completa das cartas**.

## Exportador e harness C#

O módulo yugilab.export_summons exige um JSONL ou ZIP/diretório de captura completa, com seed definida, sem eventos perdidos e com metadados válidos. Revalida o evento antes de gerar auto-gXXXXXX-seqXXXXXXX.json e export-summary.json.

O schema é yugire.partial-normal-summon.v1, compatível com os dois primeiros testes manuais. O JSON inclui antes e depois, posição da mão, slot, índice interno, LP, contexto CE, proveniência e limitações. O teste C# usa um monstro comum nível 4 **substituto**: a legalidade da invocação de cada carta não está validada.

A suíte agora compara, quando disponíveis, **a sequência completa dos índices na mão e os cinco slots de monstros**, além da quantidade e LP. Ela aceita um diretório opt-in pela variável YUGIRE_DIFFERENTIAL_FIXTURES, sem copiar fixtures automáticos para o Git nem modificar os testes existentes.

### Passos no PowerShell, na raiz do repositório

~~~powershell
git pull
$py = ".\native_rules\.venv-frida\Scripts\python.exe"

# Exige uma captura NOVA para conter os snapshots adicionais.
& $py -m yugilab --exe "..\passion2\joey_pc.exe" --duels 10 --workers 2 --seed 12345 --deck-0 ".\native_rules\decks\demo-A.json" --deck-1 ".\native_rules\decks\demo-B.json" --trace-ai --speed-value 600

$run = Get-ChildItem ".\scratch\labs" -Directory -Filter "parallel-*" |
  Sort-Object LastWriteTime -Descending | Select-Object -First 1
$out = ".\scratch\differential-$($run.Name)"

& $py -m yugilab.export_summons $run.FullName --output-dir $out

$env:YUGIRE_DIFFERENTIAL_FIXTURES = (Resolve-Path $out).Path
try {
  dotnet run --project ".\src\tests\Joey.Tests\Joey.Tests.csproj"
} finally {
  Remove-Item Env:YUGIRE_DIFFERENTIAL_FIXTURES -ErrorAction SilentlyContinue
}
~~~

Se não houver transições suficientemente documentadas, o exportador retorna código 2. A ausência de fixtures não será confundida com sucesso. Para diagnosticar sem exigir fixtures, existe --allow-empty.

**Capturas anteriores não contêm o evento novo.** Não podem ser convertidas retroativamente em before/after confiáveis. Os dados podem incluir cartas ocultas: use a pasta scratch/ (ignorada pelo Git), sem publicar os JSONL.

## Limites e próxima etapa

- O processo atual não sincroniza todas as ações, cartas, efeitos e flags como uma transação atômica.
- LP, mão e campos são verificáveis; cemitério, correntes, IA, RNG interno durante ações e resolução detalhada de efeitos ainda não entram no teste diferencial.
- Precisamos validar o hook passivo com o Joey real antes de declarar esse fluxo operacional. Não há garantia de que o primeiro experimento produza fixtures.
- Após validar, migrar o adaptador para ações e estados tipados, usando o catálogo original para evitar a aproximação por carta nível 4.

Testes offline sintéticos foram adicionados em yugilab/tests/test_export_summons.py. Eles não substituem a prova no Windows.
