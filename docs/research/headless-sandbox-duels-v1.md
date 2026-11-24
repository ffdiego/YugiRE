# YugiRE — runner de duelos headless (protótipo C#)

Este é um **laboratório de jogabilidade C#** (`Joey.Lab duel-sim`), não uma reexecução de Joey/Frida, um replay de comandos nativos ou um certificador de paridade. Seu objetivo é rodar `DemoDuel` muitas vezes com entradas explícitas, produzir transições determinísticas e revelar erros na engine do protótipo.

O alvo futuro é um pool auditado de 100 identidades e duelos automáticos. **A primeira entrega não afirma que 100 cartas já foram auditadas ou que seus efeitos estão implementados.**

## O que a política faz

- Compra uma carta por turno (política do demo).
- Ativa magias de dano direto P045 quando disponíveis.
- Invoca no máximo um monstro normal nível 1–4 sem tributo, escolhendo maior ATK.
- Na fase de batalha, ataca o alvo disponível de menor ATK/DEF ou diretamente.
- Resolve descarte da fase final, vitória por LP ou deck vazio do demo, e limite de turnos.

Não usa tributos, invocações especiais, seleção de alvos de equipamentos, armadilhas, efeitos de monstros, correntes ou a IA original. **Os vencedores pertencem à política do protótipo**, não ao Joey original. Não altera o Card Coverage Registry.

## 1. Executar dez duelos completamente sintéticos sem DAT

No PowerShell, na raiz do repositório:

```powershell
dotnet restore .\YugiRE.sln
dotnet build .\YugiRE.sln --no-restore -m:1
dotnet run --project .\src\tests\Joey.Tests --no-build
dotnet run --project .\src\Joey.Lab --no-build -- duel-sim --fixture --duels 10 --seed 12345 --output .\scratch\sandbox-fixture-10.jsonl
```

O fixture cria dois decks de 40 cartas com 38 monstros de atributos inventados e duas magias de dano suportadas em cada. Essas identidades sintéticas **não** contam como cartas reais implementadas. O limite padrão é 160 turnos por duelo; máximo de 500 duelos por execução. O runner nunca sobrescreve um JSONL existente: para repetir, use outro destino e compare os arquivos.

## 2. Propor um pool de até 100 cartas reais para revisão

```powershell
dotnet run --project .\src\Joey.Lab --no-build -- pool-candidates --dat ..\passion\data.dat --game all --count 100 --output .\scratch\pool-candidates-100.json
```

`--game joey` restringe ao pack do Joey (sem reproduzir unlocks); `all` é a união dos três jogos. O filtro estrutural seleciona nome não vazio, classe monstro, nível 1–4, ATK positivo, **categoria nativa 0 (normal)** e ausência de invocação especial conhecida.

**Esses filtros não comprovam ausência de efeitos.** O resultado ainda pode incluir monstros de efeito ou exceções não implementadas no demo. Os candidatos saem com `mode: "UnreviewedCandidates"` e `note` vazio: o runner se recusa a executá-los automaticamente. Se menos de 100 passarem pelo filtro, o programa informa a contagem efetiva.

O arquivo de saída inclui nomes e atributos para revisão **local**, sem descrição integral. Se houver ao menos 80 candidatos, ele propõe dois decks iniciais distintos de 40 cartas cada, ainda **não aprovados**.

## 3. Revisar um manifesto de execução parcial

O arquivo possui `schemaVersion: 1`, `cards`, `deck0`, `deck1`. Para transformá-lo em manifesto válido:

1. Conferir cada identidade por índice interno + resource ID, nunca somente nome.
2. Deixar `policy: "BaseMonsterSandbox"` apenas quando se aceita conscientemente executar **só atributos e regras básicas**. Não equivale a declarar que o monstro não tem efeito no Joey.
3. Pode-se adicionar manualmente uma magia com `policy: "DirectDamageSandbox"`, desde que seu índice/ID corresponda ao DAT e a regra P045 seja suportada. Equipamentos ainda não são executados automaticamente nesta versão.
4. Preencher `note` com uma justificativa de escopo por carta (ao menos 12 caracteres). Uma nota não é evidência de paridade.
5. Só após revisão, mudar `mode` de `UnreviewedCandidates` para `SandboxPartialOnly`. Esse é o opt-in consciente ao sandbox limitado.
6. Cada deck precisa ter 40–80 entradas da whitelist aprovada, no máximo três cópias por índice interno.

Depois de revisar e salvar, por exemplo, como `scratch/pool-reviewed.json`:

```powershell
dotnet run --project .\src\Joey.Lab --no-build -- duel-sim --dat ..\passion\data.dat --manifest .\scratch\pool-reviewed.json --duels 100 --seed 12345 --output .\scratch\duels-csharp-100.jsonl
```

O runner valida índice interno, ID, política, notas, tamanho do deck e multiplicidades. A revisão humana é uma **declaração de política**, não prova de compatibilidade nativa.

## 4. Formato de saída

- `sandbox_run`: fonte (`SyntheticFixture` ou `OperatorDeclaredSandboxPool`), `nativeParity:false`, seed, limites, índices dos decks e hashes SHA256 do DAT/manifesto quando informados.
- `sandbox_action`: número de duelo, sequência, turno, jogador, ação, índice de origem, alvo e snapshots de antes/depois (LP, contagens de deck, mão ativa, slots).
- `sandbox_duel_end`: vencedor e status determinados pelo demo (`SandboxWinner` ou `TurnLimit`).
- `sandbox_complete`: fechamento da execução e contadores. Ausência deste registro significa que o arquivo pode estar incompleto.

Sem datas ou tempos de relógio na saída: a mesma entrada e mesmo runtime produzem bytes determinísticos. Os logs contêm índices de decks e decisões simuladas; mantê-los em `scratch/` e revisar antes de qualquer publicação.

## 5. Critérios de aceite e continuidade

- Build `YugiRE.sln` e checks `Joey.Tests` no Windows; duas execuções do fixture com o mesmo seed devem gerar JSONL idêntico.
- Gerar candidatos reais, auditar um subconjunto pequeno, montar dois decks legais para este sandbox e verificar duelos terminando.
- **Não modificar a validação nativa** do registry; testes sintéticos não comprovam paridade.
- Na próxima etapa: ampliar o pool revisado, apoiar equipamentos com escolha de alvo, gerar estatísticas de ações por carta e construir adaptador de comparações com capturas nativas cujo contexto seja suficiente.

O PR #1 de hooks internos de combate está isolado após o crash observado em Windows. Este fluxo headless C# **não depende dele**.
