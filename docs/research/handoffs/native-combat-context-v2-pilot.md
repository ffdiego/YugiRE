# Handoff nativo — consumo F40 v2 e próximo piloto

Branch `feat/native-combat-context-v2`. Implementação: commit `8227f4c`
(`Observe consumed flags, posture and seed in combat context v2`). Contrato,
procedimento e handoff estão no commit documental subsequente desta branch.
Contrato: [revisão v2](../ai-combat-observation-contract.md#revisão-v2--pontos-de-consumo-e-fronteira-de-independência).
Parada: pronto para piloto Windows; nenhum Joey executado neste ambiente.

## Evidência reutilizada e resultado

S3 permanece 64 avaliações / zero contextos completos. Não reexecutei o diagnóstico
escalar para produzir uma falsa cobertura adicional. Reutilizei os corpos F40,
wrappers e ciclo de simulação citados no contrato. Conferência local somente leitura:

```bash
sha256sum ../passion/joey_pc.exe
objdump -d -Mintel --start-address=0x5b0f40 --stop-address=0x5b1309 ../passion/joey_pc.exe
objdump -d -Mintel --start-address=0x5add60 --stop-address=0x5ade3c ../passion/joey_pc.exe
```

SHA256 EXE: `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
REA binary_session consultado: sem alvo aberto; usei objdump local para os pontos
exatos, sem novo import/alteração de projeto Ghidra. Essas conclusões são assembly,
não captura observada. Os nove spans de instruções são conferidos no PE pelo teste.

Mudança principal: EAX após 1195 prova o DWORD rival carregado; TEST de memória em
11C6 requer testemunho dos ramos para provar o bit próprio, pois uma releitura não
é o mesmo consumo. Postura em CL após F74, seed em DX após 1086, visibilidade pelos
registradores/ramos dos wrappers. Instrumentação limitada, com campos raw/null e
pilhas por thread existentes. Não houve alteração em Core, Lab, PLANS ou AGENTS.

## Próximo piloto mínimo (operador Windows, depois da revisão)

Alvo: uma avaliação natural com **Axe Raider (24/004D) e Gemini Elf (550/0213)** revelados,
preferencialmente ambos em ataque. Conferir identidades contra o catálogo local. Usar um duelo inicial simples sem ativação conhecida de efeitos,
sem equipamento, sem troca de atributos e sem Mirror Wall; guardar histórico e
proveniência do cenário. Se isso não ocorrer em 1–3 duelos, encerrar Incomplete.
Não mudar decks/seeds/estado com RPC para fabricar o confronto. Não investigar
Goblin Attack Force ou outras cartas nesta unidade.

1. No Windows, jogo original em PID próprio, sem overrides. Na raiz do repositório:

   ```powershell
   .\.venv-frida\Scripts\python.exe .\native_hooks\patch_dual_rules.py --observe-only --pid <PID>
   ```

   Preservar stdout/preflight que identifica caminho carregado, SHA e PID. O host
   já verifica hash e autoriza o atestado no loader. Não copiar o hash deste handoff
   para metadata de uma captura anterior.
2. Armar perto da decisão pretendida, antes do seletor natural:

   ```text
   trace on 20000
   trace ai on combat-context
   trace mark combat-context-v2-pilot
   ```

3. Deixar ocorrer a decisão natural, sem console draw/damage/summon, prep, demo,
   seed, deck ou speedy override. Parar na primeira avaliação candidata ou no
   limite, sem rearmar repetidamente para acumular duplicatas. Um readError,
   anomalia, crash ou perda encerra o piloto e preserva o material.
4. Depois do retorno natural do seletor, console:

   ```text
   trace off
   trace status
   trace dump
   stop
   ```

   Preservar JSONL original privado, hash `Get-FileHash <jsonl> -Algorithm SHA256`,
   log de preflight, versão/commit do loader e relato de retorno normal. Revisar
   seq/overwritten, pares e contagem de filhos, assinaturas/19 hooks, snapshots,
   nove tipos de ponto e metadata `exeSha256`. Não commitar o trace bruto.
5. Selecionar **um** par revelado suportado e aplicar checklist do contrato. Ausência
   de um ramo `attacker_protection_taken` só determina false junto de TEST e junção
   íntegros. Exigir um único ramo de visibilidade rival por wrapper. Caso oculto
   não atende este cenário. Campos novos só são observados após executar esse piloto.

O piloto pode fechar lacunas temporais sem fechar independência. Campo que ainda
pode impedir o primeiro contexto: **manifesto completo de modificadores/elegibilidade
DD30**, inclusive efeitos globais. LinkCount zero e stats iguais aos do catálogo não
o resolvem. Nesse caso registrar Incomplete com esse nome; não passar buffers
nativos a Core como se fossem cálculo independente. Nenhuma equivalência declarada.

## Interface para Terra / automação

Inspeção de `src/Joey.Lab/Experiment.cs`, `yugilab/__init__.py` e
`native_hooks/native_rules.py::_run_spawn_cycle`: `--trace-ai` hoje é bool e chama
`traceaistart(False)`. O ciclo arma `tracedemostart()` e speedy por padrão, além de
seed/decks opcionais; `startAiProbe('combat-context')` recusa esses mutadores.
Logo, **substituir False pela string não basta**. O console acima é a menor mudança
operacional disponível agora, sem editar a trilha T.

Interface proposta, ainda **não implementada**: `experiment --observation-mode
combat-context`, encaminhada ao Python; workers=1, duels=1..3, sem speed override,
seed, decks e mutex override, com erro explícito se solicitados. Backend deve
reutilizar ownership do PID, hash do caminho carregado, startup observer,
LiveDuelCapture/streaming, cancelamento e encerramento verificado; não armar
tracedemostart/setdemo*/setspeedy. Para término N duelos, reaproveitar terminal de
command trace sem hooks demo. Não alterar as restrições do modo para encaixá-lo no
launcher atual. A revisão desta interface deve coordenar posse do backend nativo.

Leitor v2: aceitar eventos instantâneos `phase=leave` com `valueRaw`,
`preservedMaskRaw`, `playerOffsetRaw`, `protectionConsumed`, `faceUpConsumed`,
`wrapperArgsRaw` e posições pré/pós, sem exigir `returnRaw` desses pontos. Validar
multiplicidade, ordem por frame e ambos os ramos condicionais; preservar JSON raw.
Contagem de filhos inclui pontos. Leitor atual classifica v2 como Unsupported:
comportamento esperado até adaptação por Terra. Manter a ausência do manifesto
independente como Incomplete mesmo depois de aceitar schemaVersion=2.

## Validação e limitações

Testes efetivamente executados:

- `python3 -m unittest discover -s native_hooks/tests`: 88 testes OK, incluindo
  confronto das nove assinaturas com o EXE local, sem skip nesse confronto.
- `node native_hooks/tests/test_loader.cjs`: PASS, incluindo ambos os valores do
  bit consumido, reentrância/thread, falha de registro, assinatura incorreta,
  rollback, perda/overflow, junção sem TEST e nenhuma escrita no mock do duelo.
- `node native_hooks/tests/test_deck_trace.cjs`: PASS.
- `node native_hooks/tests/test_demo_rng_seed.cjs`: PASS.
- `python3 -m unittest discover -s yugilab/tests`: 50 testes OK (mocks, sem jogo).
- `python3 -m unittest discover -s scripts/tests`: 14 testes, OK com um skip
  porque o JSONL privado original S3 não está neste checkout.
- `git diff --check`: sem erros.

 Mocks de CPU
não emulam x86/Frida. A relocação dos ramos e a preservação de registradores,
latência e comportamento normal do jogo só podem ser validados no Windows.
Os hooks não chamam helpers, não escrevem estado de duelo nem o EXE em disco;
Interceptor instala trampolines no código em memória. Não há prova de snapshot
atômico contra escrita por outro thread. Retornos naturais/DD30 não foram
executados aqui. Não foram rodados .NET nem alteradas regras C#.
