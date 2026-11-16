# Piloto de efeitos P045 — YugiRE

Esta etapa prepara **os mesmos decks** para `Joey.Lab duel-sim` (C#) e YugiLab Demo (Joey nativo).
Ela não compara seed, shuffle, decisões ou estado como se fossem equivalentes.
**Não reativa os hooks internos do PR #1.**

## 1. Preparação e testes offline

Na raiz do worktree Windows use o manifesto de 53 monstros normais aprovado
anteriormente (`scratch/pool-joey-sandbox.json`) e o registry/attributes gerados
localmente por `generate_card_coverage.py` do seu DAT/EXE.

~~~powershell
python -m unittest discover -s tests -p test_effect_pilot.py -v
python -m unittest discover -s yugilab/tests -p test_effect_report.py -v
python .\scripts\prepare_effect_pilot.py --pool .\scratch\pool-joey-sandbox.json --registry .\scratch\coverage-current\registry.json --attributes .\scratch\coverage-current\attributes.json --output-dir .\scratch\effect-pilot-decks
~~~

O diretório de saída deve ser novo. Verifica IDs e índices internos, disponibilidade
no pack Joey, categoria 0 para monstros e a identidade das cinco magias P045.
Se uma magia não estiver disponível no pack Joey, será omitida.
Cada deck conterá 40 cartas: até duas cópias de cada magia disponível e
o restante de monstros normais. As magias podem ser jogadas no sandbox,
mas **não** estão totalmente validadas contra a engine nativa.

Arquivos locais criados: `sandbox-mixed.json` (para C#),
`native-effect-A.json` e `native-effect-B.json` (para YugiLab),
e `effect-pilot-plan.json` com IDs e hashes de origem.

## 2. Dez duelos C# com as magias reais

~~~powershell
dotnet build .\YugiRE.sln -m:1
dotnet run --project .\src\Joey.Lab --no-build -- duel-sim --dat ..\passion\data.dat --manifest .\scratch\effect-pilot-decks\sandbox-mixed.json --duels 10 --seed 12345 --output .\scratch\effect-pilot-csharp-10.jsonl
~~~

É esperado encontrar eventos `FixedDamage` se as magias forem compradas.
Os resultados são **somente do sandbox C#**.

## 3. Piloto nativo: primeiro três duelos

Use a mesma cópia do EXE reconhecida pelo preflight, com backup de saves
e apenas um worker. O YugiLab inicia e encerra somente o processo que cria.
Não execute o PR #1 experimental nem novos hooks. A troca de decks e seed
é em memória, mas o jogo pode acessar saves e recursos compartilhados.

~~~powershell
.\native_hooks\.venv-frida\Scripts\python.exe -m yugilab --exe ..\passion\joey_pc.exe --duels 3 --workers 1 --deck-0 .\scratch\effect-pilot-decks\native-effect-A.json --deck-1 .\scratch\effect-pilot-decks\native-effect-B.json --seed 12345 --speed-value 2400 --trace-ai --output-dir .\scratch\effect-pilot-native-3
~~~

Exigir captura completa, decks aplicados/verificados, sequência contínua,
nenhum evento perdido e encerramento verificado do PID. Diante de crash,
perda, mismatch ou PID remanescente, **parar** e preservar os arquivos.

Depois, sem reabrir o jogo:

~~~powershell
python -m yugilab.effect_report --input .\scratch\effect-pilot-native-3 --output .\scratch\effect-report-native-3.json
~~~

O auditor registra comandos `CF` associados a resource IDs de P045,
eventos `CE` de mesmo ID e comandos de LP `0x43` separadamente por duelo.
**Coocorrência NÃO prova ativação ou dano causado por aquela carta.**
Cada exemplo preserva o `seq` e o hash SHA256 da captura para investigação.
Se não houver comandos candidatos, isso aponta para instrumentação insuficiente
ou ausência de ocorrência, não para inexistência de efeitos.

## 4. Ampliar condicionalmente

Somente se o piloto tiver integridade e evidências úteis, repetir com
`--duels 10`, nova pasta `effect-pilot-native-10` e novo relatório.
Depois decidir se vale executar 100. Não escalar direto para 100 se
faltarem eventos candidatos, se a IA não ativar magias ou se houver falhas.

Este piloto ainda **não oferece estado completo do efeito**.
Para provar a resolução precisamos observar LP/campo/mão antes/depois
com contexto causal independente e depois confrontar a engine C#.
Não commitar EXE, DAT, capturas completas, saves nem decks com dados sensíveis.
Os artefatos locais ficam em `scratch/`.

O preparador permite classCode 21/22/23 para as cinco identidades P045 auditadas, preservando o código observado no plano; não infere que categorias inteiras tenham efeito implementado.
