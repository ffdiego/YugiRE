# Rain of Mercy — assinatura nativa de comandos de LP por lado (16/11/2026)

**Escopo:** observação de dois pares de comandos relacionados ao resource ID
`1315` (Rain of Mercy, internalIndex `969`) em uma captura de jogo nativo.
Não se afirma validação integral da carta, gatilhos, legalidade ou aplicação
dos +1000 LP sem amostra anterior da vida.

**Fonte:** `duels-20261116-041556-587954-pid22896.jsonl`, SHA256
`179bab9f66660e1c546a66e717ad37c1a2575784e0551e71fb1d82ebba8292a0`.
Janelas locais extraídas para resource ID 1315 (12 comandos anteriores,
16 posteriores). Captura total: 2.159 eventos, três terminais, integridade
verificada pelo discovery-report. Não versionar o JSONL completo.

## Comandos nativos efetivamente observados

| Duelo | CF 1315 (caller 57977F) | 0x0042 lado 0 | 0x8042 lado 1 |
|---:|---:|---:|---:|
| 1 | seq 335 | seq 338, amount 0x3E8=1000 | seq 339, amount 0x3E8=1000 |
| 2 | seq 1191 | seq 1194, amount 0x3E8=1000 | seq 1195, amount 0x3E8=1000 |

Todos os quatro comandos `0x42` têm caller `0x578823` e terceiro/quarto
argumentos 0/1. O bit 15 do primeiro argumento diferencia os dois lados.
**Observação forte da emissão de planos de aumento de LP para ambos**;
sem prova de que os valores tenham sido aplicados a ambos no estado.

Primeiras amostras de LP depois dos comandos (coletadas na entrada de
produtores subsequentes): duelo 1 seq 340: `[7600,3600]`, duelo 2
seq 1196: `[3950,2900]`. **Nenhuma amostra antes de CF aparece nas
janelas extraídas**, logo não derivar `+1000,+1000` dos snapshots.

## Dano secundário observável, sem fonte atribuída

Depois dos comandos de `0x42`, há `0x0043` com amount `0x12C=300`,
caller `0x578513`. No duelo 1, seq 346→348, os LP mudam
`[7600,3600]→[7300,3600]`. No duelo 2, seq 1202→1204,
`[3950,2900]→[3650,2900]`. Ambas as vezes o lado 0 perdeu
exatamente 300 LP. **Não atribuir esse dano à Rain of Mercy, Raimei
ou a outra carta** até reconstruir contexto independente. Pode haver
gatilho ou efeito de outra carta; isso permanece hipótese.

## Critérios para o próximo experimento

1. Buscar no JSONL original a última leitura de LP antes de cada CF,
   mesmo se ficar fora da janela de comandos, guardando seq e distância;
   recuperar a primeira amostra após os dois comandos `0x42`.
2. Se existir par de amostras, reportar diferença como observação
   entre amostras, não como causalidade garantida. Preservar mudanças
   intercaladas e qualquer outra fonte de LP.
3. Em caso de ausência/ambiguidade de amostra, investigar rotina nativa
   de emissão de `0x42` e consumidor de LP, ou ensaio controlado
   observacional. Não alterar hooks instáveis do antigo PR #1.
4. Portar primeiro um contrato *escopado* de comando de aumento de LP
   ao C#, não um efeito de carta completo por mera analogia.

A extensão offline de `yugilab.inspect_effect_windows.py` no PR #6
detecta o par `CF +3/+4` de `0x42` por caller/thread, preserva
amostras anteriores/posteriores e a diferença não atribuída. Seu novo
teste sintético ainda **não foi reexecutado pelo operador Windows**.
