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
