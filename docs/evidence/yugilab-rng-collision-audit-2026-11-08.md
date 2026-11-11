# YugiLab — repetições de embaralhamento entre workers

**Data:** 2026-11-08. Auditoria offline dos JSONL já capturados; sem alterações no executável ou no Frida.

## Método

Após cada `demo_init/scene_config/enter` com `requestedSceneMode=7`, coletar o PRIMEIRO `demo_init/deck_shuffle/leave` de cada lado (0 e 1). O SHA256 dos dois arrays `deck.indices`, em sua ordem original, é o fingerprint. Embaralhamentos adicionais no mesmo duelo são ignorados. O processamento é streaming, inclusive para arquivos ZIP.

Fingerprints iguais demonstram a **mesma dupla de ordens iniciais de cartas**. Não comprovam seed igual, mesmo RNG, IA determinística ou identidade integral do duelo.

## Evidência empírica

| Captura | Cenas com shuffle de ambos os lados | Fingerprints únicos | Repetições além da primeira | Grupos entre PIDs |
|---|---:|---:|---:|---:|
| `parallel-i79ol65z.zip` | 2 | 1 | 1 | 1 |
| `parallel-u405p7z4.zip` | 6 | 6 | 0 | 0 |
| `parallel-usos_27u.zip` | 100 | **65** | **35** | **17** |

**Atenção:** no lote de 100 cenas, um worker não finalizou a captura; o número de cenas com shuffles não equivale necessariamente ao número de duelos encerrados e validados.

Os PIDs 24512 e 16268 (`parallel-i79ol65z.zip`) tiveram as mesmas 43 cartas embaralhadas no lado 1 e as mesmas 40 cartas no lado 0, nas mesmas ordens. No lote de seis duelos, não ocorreu repetição.

Na bateria de dez workers, houve também fingerprints idênticos em posições diferentes: `7850d7ad4ae6...` aparece no duelo 7 dos PIDs 23528 e 5848, duelo 8 do PID 23032, duelo 6 do PID 23632 e duelo 7 do PID 13284. Os timestamps da entrada da cena ficaram dentro de **7 ms**. Esse padrão é consistente com correlação temporal, mas não estabelece a fonte da seed.

## Ferramenta adicionada

O comando `python -m yugilab.rng_audit` lê diretórios, arquivos JSONL ou ZIPs sem extrair tudo na memória:

```powershell
.\native_hooks\.venv-frida\Scripts\python.exe -m yugilab.rng_audit `
  .\scratch\labs\parallel-EXEMPLO `
  --output .\scratch\rng-audit-example.json
```

No modo paralelo, o YugiLab gera automaticamente `rng-audit.json`, inclui resumo `rngAudit` no `manifest.json` e avisa quando fingerprints se repetem entre workers. A verificação não altera nem invalida o status da captura: `complete=true` e independência estatística são conceitos diferentes.

## Próximo passo: localizar o RNG original

Execute `scripts/Run-RngRecon.ps1`, que usa o projeto Ghidra e a consulta read-only existente. Ele inspeciona `FUN_00568D80(side,passes)`, seus callers e importações candidatas como `rand`, `srand`, `time`, `GetTickCount` e `timeGetTime`.

Os três relatórios esperados são `rng-recon-shuffle.txt`, `rng-recon-crt.txt` e `rng-recon-alternatives.txt` em `docs/evidence/`.

Para distinguir seed inicial, reseed por duelo e estado persistente entre jogos, precisamos seguir a função **realmente chamada pelo shuffle**, e não assumir que o Joey usa o CRT só porque existe uma importação.

Não implementar `--seed` antes de validar a localização e o contrato dessa função.
