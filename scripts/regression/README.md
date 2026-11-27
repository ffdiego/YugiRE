# scripts/regression — verificações opcionais

Execute os comandos da raiz do repositório, com as dependências e os dados locais de cada verificação.

### 14 — C01: taxonomia de efeitos pelo DAT local

- Ambiente: Windows (Git Bash) ou Fedora, Python 3 e .NET 10, DAT original local.
- Comando: `bash scripts/regression/14-card-effect-taxonomy.sh /caminho/para/data.dat`.
- Saída privada: `scratch/card-taxonomy.json` (IDs, fingerprints, famílias, rotas e status atuais; sem descrições).
- Pergunta: quais cartas dos packs são candidatas a lotes triviais e quais exigem revisão/continuações/respostas?
- Encerramento: conferir as 771 entradas por ID, máscara e categoria e validar o fingerprint com `docs/cards/pack-index.json`. O SQLite v2 contém 442 identidades. O DAT local fornece as descrições para a triagem.
- Estado: execução concluída em Fedora/.NET 10. Rotas validadas no lote local: 264 sem efeito aplicável, 433 fila/resposta, 72 revisão e duas candidatas triviais. A compra já consta como Done e a cura permanece Pending. Mantenha a saída `--taxonomy-input` no pipe privado do classificador. A classificação preserva os status do manifesto e a saída em `scratch/` contém somente metadados.

## A03/A04 — 22-actions-console-visual-smoke.sh (piloto validado no Windows)

Execute `bash scripts/regression/22-actions-console-visual-smoke.sh /caminho/para/data.dat` para a regressão local; consulte os comentários do script para as opções.

Roteiro 22 executado via PowerShell, headless e gráfico: 18 Applied e 106 linhas relevantes idênticas após fff5e6e. Novo roteiro `scripts/fixtures/duel-console-two-sides-smoke.txt` executou 29 Applied, sem espera >=5s; operador confirmou compras animadas de ambos lados. CI 45328386043. Manter script como regressão opcional. Não implica paridade visual nativa; A05/A07 pendentes.


## A09 — actions-a09-combat-smoke.py (smoke gráfico com DAT local)

- Requisitos: Python 3.10+, .NET 10, DAT privado e sessão gráfica. Não requer
  joey_pc.exe nem modifica saves. Rodar a partir da raiz:
  `python scripts/regression/actions-a09-combat-smoke.py --dat ../passion/data.dat`.
- Roteiro: `scripts/fixtures/duel-console-combat-smoke.txt`; direct lado 1,
  combate com alvo lado 0, decks não vazios, setup explícito por spawn.
  A hipótese de ID 1351 com ATK positivo é validada pelo resultado do Lab,
  não pela fixture sintética do CI.
- Fechar Classic e Instant depois de Script completed. Logs em
  `scratch/actions-combat/smoke-*/` (diretório único). Enviar os três .txt e
  registrar se a espada aparece antes do dano/remoção; não versionar o DAT.
- Critério: mesmo trace/state headless e visual nos dois perfis, fila livre,
  nenhuma espera >=5s, ordenação Started/Completed serial. A comparação
  não mede pixels nem paridade com o original. A captura nativa equivalente
  de A09 continua pendente; este roteiro não a substitui.

V20.2 — revisão de moeda (Windows/Fedora desktop): `dotnet run --project src/Joey.App -- /path/to/data.dat --review-coin=3,5,400,100,1`. F7 avança, F8 reinicia, F12 captura PNG/JSON. Verificar uma/três moedas e ambos os bits; recortes/halo e flags de reflexão no JSON. Reflexão/brilhos não implementados; não repetir coleta 16. Validação manual pendente, sem dados privados versionados.

V20.3: o review de moeda agora inclui brilho key5 e JSON schema 2 com o plano de reflexão. A reflexão continua omitida até resolver +94/+A0. Roteiro 24 já abrange esses offsets da vtable; não repetir a coleta 16.

### 41 — V17, formato da superfície da cena em Wine

O [procedimento para preparar Proton, Xvfb e a cópia do original](../../docs/research/joey-proton-validation.md) descreve a abertura do duelo, a captura e a identificação do processo.

Abra um duelo na sua cópia de teste com GDB e Wine/Proton disponíveis.
Confira o SHA-256 do executável, o PID da cópia que você iniciou e o
mapeamento do PE em `00400000`. O fingerprint é o da coleta 34.
O roteiro usa breakpoint de hardware, lê três entradas de `0044DD00`
e desanexa. Os campos de formato e flag são lidos na vtable conhecida.

```bash
timeout --signal=INT 15s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/41-wine-scene-surface.gdb > scratch/scene-surface-wine.log 2>&1
```

O piloto em Proton 9.0, Xvfb a 800×600 e llvmpipe retornou
`0x005dc6ec format=3 flag78=0` nas três amostras do duelo de
demonstração. Os breakpoints interferem no tempo de execução.
A coleta 34 em Windows continua sendo a referência para cadência e
para a passagem de composição na seleção.

### 42 — V11/V17, compositor do cemitério adversário em Proton

Use a cópia i386 do executável e o display privado do roteiro 41.
No duelo individual, deixe o adversário com pelo menos uma carta
no cemitério. Anexe ao processo de teste e clique na pilha do
cemitério adversário. O roteiro usa quatro breakpoints de hardware
e desanexa após a primeira passagem estável.

```bash
timeout --signal=INT 75s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/42-wine-selection-compositor.gdb > scratch/selection-compositor-wine.log 2>&1
```

O recorte registra vtables, formatos, origem da amostra, chave,
retorno do blit e a consulta do ramo por cor-chave RGB565. O piloto
observou a chave `0x07E0` em `004B2D29` com retorno 0 da chamada.
Para verificar a transparência na mesma cena, capture o canvas
antes e depois de abrir a lista e confronte os pixels do canto e
do interior. A coleta 34 continua disponível para o cenário Windows.

### 43 — V31, quadrilátero base da reação em Proton

Use o [prefixo e display privados do piloto](../../docs/research/joey-proton-validation.md).
Confirme o fingerprint, o PID da cópia e o mapeamento do PE. Anexe no
menu e aguarde a demonstração automática. O roteiro usa dois
breakpoints de hardware e desanexa quando o objeto observado termina.

```bash
timeout --signal=INT 115s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/43-wine-reaction-geometry.gdb > scratch/reaction-geometry-wine.log 2>&1
```

Compare os quatro vértices de origem e os quatro de destino com
`bustUpReactionGeometry` no JSON do inspetor DAT. O piloto encontrou
uma correspondência única com `reaction07.txt`, formato 3 em ambas as
superfícies e 63 chamadas base com contador de 8 a 504, em passos de
8. O log deve terminar com `reaction-end` e a confirmação de
desanexação. O manifesto encontrado e o contador final podem variar
conforme a reação. O recorte mede argumentos e contadores do processo.

### 44 — V31, camadas da reação em Proton

Use o mesmo cenário e a mesma identificação de PID do roteiro 43,
em uma nova execução da demonstração. O roteiro seleciona uma reação
cujo primeiro cabeçalho é 1 no contador 8. Ele registra os dois
despachos, suas variantes, as posições nos slots `+0C` e `+4C` e o
término. Usa quatro breakpoints de hardware.

```bash
timeout --signal=INT 115s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/44-wine-reaction-overlays.gdb > scratch/reaction-overlays-wine.log 2>&1
```

O piloto capturou as duas variantes da boca e dos olhos de
`reaction07.txt` no slot `+0C`. Confira que o primeiro despacho
precede o segundo no mesmo contador e que cada linha `layer` tem
um despacho correspondente. Use o registro `selected` e a origem
do manifesto para conferir a posição final. As omissões dependem
dos bits de estado. Confira o encerramento em `reaction-end` e a
desanexação no log.
