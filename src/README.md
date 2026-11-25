# Protótipo .NET 10 + Raylib

Na pasta `src/Joey.App`, execute:

```bash
dotnet run
```

Requer SDK .NET 10, desktop gráfico e bibliotecas do sistema usadas pelo Raylib.
A dependência Raylib-cs 7.0.2 fica fixada no projeto (compatível via net8.0).
O aplicativo procura `passion/data.dat` nos diretórios ancestrais da pasta atual.
Para outra instalação:

```bash
dotnet run -- /caminho/para/data.dat
```

Os arquivos originais são abertos somente para leitura e não são distribuídos
com o código. Voice.dat e Region.dat não são necessários nesta versão.
Não há extração permanente de recursos nem dependência do Ghidra em execução.

## Windows e Visual Studio

Abra `YugiRE.sln` na raiz do repositório e defina **Joey.App** como projeto
de inicialização. A solução inclui o leitor, a aplicação e o executável de
verificações. A partir da raiz, também é possível usar PowerShell:

```powershell
dotnet restore .\YugiRE.sln
dotnet build .\YugiRE.sln --no-restore
dotnet run --project .\src\Joey.App -- "C:\Jogos\Joey\data.dat"
```

Ajuste o caminho de `data.dat` para sua instalação. Ele não acompanha o clone.
Para corrigir NETSDK1004 antes de obter a solução, restaure diretamente o
projeto: `dotnet restore .\src\Joey.App\Joey.App.csproj`. Isso restaura
Joey.Core por sua referência. `project.assets.json` é gerado pelo NuGet em
`obj/`; essa pasta fica fora do Git e deve ser recriada em cada máquina.
Se a restauração falhar, resolva o erro apresentado pelo próprio restore
antes de executar build com `--no-restore`.

No Visual Studio, a ação **Restaurar Pacotes NuGet** no menu da solução
executa essa etapa caso a restauração automática esteja desabilitada.

## Telas e controles

- Menu: fundo e botões originais lidos do DAT em 800×600. Single Duel Mode,
  Card List e Quit Game funcionais. Deck Construction abre o inspetor de deck
  descrito abaixo; edição ainda não implementada. Options permanece desabilitado.
- Card List: grade de 10 colunas × 5 linhas, 50 cartas por página, em ordem
  alfabética pela tabela inglesa do DAT. Passe o mouse sobre a miniatura para
  ver atributos e descrição. As setas originais ou PageUp/PageDown mudam a página.
  Digite para pesquisar por nome; Backspace apaga. Registros sem nome ficam no fim.
  O fundo, o título, o botão Back e as imagens das cartas vêm do DAT.
  Use a roda do mouse sobre a descrição para percorrer textos longos.
  Os botões originais All cards e Joey alternam entre as 771 cartas dos três
  jogos e as 350 do Joey, sem restrição de desbloqueio. A busca é preservada
  entre filtros e a página retorna ao início. O filtro
  usa `card_pack.bin`, não a imagem da carta. Para inspecionar os 1114 registros
  internos, use `dotnet run -- --include-unused` (junto ao caminho DAT, se necessário).
  Esse modo mantém variantes e registros sem nome, sem inferir jogabilidade.
- Duelo local: a mão exibida pertence ao jogador ativo, alternado por End turn.
  Draw compra uma carta por turno. Passe o mouse sobre uma carta para elevá-la
  e mostrar Summon/Activate; botão direito alterna para Set. Clique na carta
  ou no balão para confirmar. A colocação usa o primeiro slot livre à esquerda.
  Níveis 5–6 exigem um tributo; 7+ exigem dois. Clique nos monstros próprios
  para pagar os tributos; a colocação acontece ao completar a seleção.
  Cancel abandona a seleção. É possível liberar uma zona de um campo cheio.
  Fusões, Rituais e monstros especiais reconhecidos pelo gate nativo são
  recusados para invocação normal; seus procedimentos especiais não estão implementados.
  Evidência e limites: [invocação normal](../docs/26-normal-summon-tributes.md).
- Activate permite as cinco magias de dano P045: **Sparks (200)**,
  **Hinotama (500)**, **Final Flame (600)**, **Ookazi (800)** e **Raimei (300)**.
  No demo, elas causam dano ao oponente e seguem ao cemitério. Podem ser ativadas
  da mão ou após Set, clicando na magia setada. Set sozinho não resolve o efeito.
  A API escalar preserva desconhecidos e a guarda do pedido; a ativação imediata,
  flags=0 e o alvo usual são políticas do demo, sem cadeias ou negação nativa.
- Os três equipamentos P025 são ativados **da mão**: use Activate e selecione
  um monstro compatível face-up de qualquer jogador. Precisam de uma zona de
  magia própria livre; falha na revalidação não consome carta nem ocupa zona.
  Ativação de equipamento já setado com seleção de alvo ainda não é suportada.

  | Equipamento (ID de recurso) | Restrição resolvida | ATK / DEF |
  | --- | --- | --- |
  | Legendary Sword (300) | Tipo dinâmico 15 (Warrior) | +300 / +300 |
  | Beast Fangs (307) | Tipo dinâmico 11 | +300 / +300 |
  | Sword of Dark Destruction (301) | Atributo dinâmico 2 | +400 / −200 |

  O sandbox usa classe/atributo estáticos sob política explícita sem remapeamentos
  ou bloqueios externos. A API exige contexto completo; desconhecido retorna
  `Unsupported`. ATK/DEF acumulam deltas separados antes do clamp final em zero.
  Destruir o alvo limpa equipamentos dos dois donos; reutilizar slots não herda
  bônus. Retirada isolada existe como operação do sandbox, sem novo efeito de
  carta na interface. Supressão, efeitos globais e troca de controle ficam fora.
- Infinite Cards mantém o comportamento legado: ocupa uma zona de magia e elimina
  o limite de mão dos dois jogadores enquanto ativa. Não integra o conjunto de
  oito cartas dos pilotos P045/P025 nem recebe nova cobertura nesta integração.
  Os demais efeitos são recusados. A apresentação está pausada; evidências e
  roteiro em [duelo e animação da mão](../docs/visual/duel-hand-pause.md).
- Battle inicia combate após o primeiro turno. Clique em um monstro próprio,
  depois em um monstro inimigo; Direct attack funciona se o adversário não
  tiver monstros. Cada monstro ataca uma vez. Em Main, Cancel e clique num monstro próprio para alternar ataque/defesa
  aberta uma vez por turno, a partir do próximo turno próprio após a invocação.
  Monstros em defesa não atacam; invocar não impede atacar no mesmo turno.
- Hand </> percorrem mãos grandes. Cancel limpa seleção; Reset reinicia.
- End turn com mais de seis cartas exige descarte. Selecione uma carta e use
  **Discard** até ficar com seis; só então o jogador muda. A regra básica foi
  confirmada por RE em [limite da mão](../docs/25-end-turn-hand-limit.md).
  Infinite Cards ativa dispensa esse descarte; outros efeitos de fim de turno ainda não são simulados.
- Escape ou Menu retornam ao menu principal. A janela pode ser redimensionada;
  a área de interação acompanha a escala da imagem.

## Alpha vs CPU

Escolha **Alpha - vs CPU** no menu ou execute da raiz:

```bash
dotnet run --project src/Joey.App -- ../passion/data.dat --alpha
```

O modo inclui um baralho próprio, mão rival oculta, turnos automáticos e animações de carta. Fast (60 updates/s, 2x Classic) é o perfil padrão; use `--classic` para a cadência de referência (30 updates/s), `--fast` para solicitá-lo explicitamente, `--instant` para ignorar animações ou `--alpha-headless` para simular um duelo no terminal. O perfil explícito `--mute-joey` continua selecionando Silent. O cliente gráfico mostra somente warnings e erros do raylib por padrão; `--raylib-verbose` restaura os logs INFO, inclusive carregamento de texturas. Os logs do console de duelo não são filtrados. [Controles, regras e resultados dos testes](../docs/research/alpha-0.1-2026-11-21.md)

## Importar e comparar um deck original

O inspetor procura `deck.ydc` ao lado de `data.dat`. Para outra localização,
execute da raiz do repositório no Windows:

```powershell
dotnet run --project .\src\Joey.App -- "C:\Jogos\Joey\data.dat" "--deck=C:\Jogos\Joey\deck.ydc"
```

Abra **Deck Construction**. As abas mostram a lista principal e as duas listas
adicionais, preservando ordem e cópias repetidas. Os papéis das listas 1 e 2
continuam sem confirmação completa; não atribuímos nomes modernos por analogia.
**Reload** relê o mesmo arquivo após uma alteração feita no original.
Para comparar nomes e contagens sem janela, acrescente `--check-deck` ao comando.
Esse modo verifica estrutura e resolução de IDs, não legalidade nem o cabeçalho
de compatibilidade ainda desconhecido. O arquivo é aberto somente para leitura.

**Play this deck** inicia o duelo local com o mesmo deck principal para ambos
os jogadores; cada jogador recebe cinco cartas e pode comprar do restante.
Requer 40–80 cartas, conforme a checagem nativa documentada. O embaralhamento
com semente fixa é política de demonstração, não o algoritmo original.
**Demo fixture** retorna às cartas de teste anteriores. Reset preserva a opção
usada ao iniciar o duelo. As listas adicionais não participam da simulação.
Os efeitos suportados continuam limitados aos já descritos acima.

O inspetor reutiliza o fundo e as miniaturas da Card List com controles
temporários; não reproduz a tela original de construção de decks.
Comparação sugerida no Windows: conferir as três contagens, duas cópias da
mesma carta, nomes/ilustrações e a segunda lista. Na amostra deste workspace,
esperamos 42/1/0 e Alligator's Sword Dragon na lista 1. Ao iniciar o demo,
a mão deve ter cinco cartas e o deck restante 37; Draw muda para seis/36.

## RNG e embaralhamento nativo (módulo isolado)

`Joey.Core/NativeRng.cs` implementa o `rand` do MSVC
encontrado em `joey_pc.exe` e o caminho de embaralhamento
utilizado nos duelos de demonstração acelerados.
Sua saída foi comparada offline com **oito embaralhamentos
originais** de dois ZIPs independentes, verificando os
**32 bits de cada palavra de carta**.

Para executar os testes C# do gerador e as demais verificações:

```powershell
dotnet run --project .\src\tests\Joey.Tests\Joey.Tests.csproj
```

`NativeRngTests` utiliza duas seeds e hashes SHA256 calculados
diretamente das capturas do Joey. A documentação está em
[Joey native shuffle](../docs/evidence/joey-native-shuffle-matching-2026-11-08.md).

O algoritmo utiliza a divisão por `32767` do original,
que raramente pode gerar um índice fora do deck quando
`rand()==32767`; nosso código verifica e lança exceção
em vez de acessar memória inválida. Também não foi
implementado o outro modo de embaralhamento nativo.

**Importante:** `DemoDuel.FromDecks` ainda utiliza
`System.Random.Shuffle` como política do protótipo.
A classe nativa é uma implementação de referência para
futuras etapas da engine, **não** uma troca automática
do comportamento existente.

## Pacotes de comando nativo (primeiro subconjunto)

`Joey.Core/NativeDefaultQueuePacket.cs` modela o pacote
nativo de **4 palavras uint16** gravado por `FUN_005B9170`
para os opcodes verificados `0x37`, `0x61` e `0x86`,
que percorrem o caminho sem remapeamento de parâmetros.
`NativeDefaultQueuePacketTests` verifica cinco cenários
usando os argumentos reais das duas capturas com a mesma
seed. O código original possui outros caminhos que
modificam os parâmetros antes do enfileiramento; esses
não são aceitos por `TryFromProducerArguments`.

A classe conserva o bit 15 da primeira palavra como flag
de lado; os bits 16–31 dos argumentos de entrada não
fazem parte do pacote desses três opcodes. Preserve os
valores brutos de 32 bits no JSONL para análise forense.

[Assembly e resultados dos 41 pacotes comparados](../docs/evidence/joey-command-highword-origin-2026-11-08.md).

## Primeiro teste diferencial Joey → C# (invocação normal)

O harness `DifferentialNormalSummonTests` lê duas observações
**reais** do Joey, reconstruídas em fixtures JSON:
- `console-normal-summon-side1-221.json`: carta interna 221,
  posição 5 da mão → slot 0, mão 6→5 e LP 8000;
- `watch-normal-summon-side1-705-slot1.json`: carta interna
  705, posição 4 da mão → slot 1, mão 5→4.
  O comando `0xCE` original codifica `0x4103`,
  que confirma lado 1, mão 4 e slot 1.

O teste usa `DemoDuel.FromPartialObservation` para montar
somente os campos conhecidos, **executa `DemoDuel.Summon`**
e verifica mão, slot e LP quando observado. Também possui
controle negativo para demonstrar que uma carta divergente
seria detectada. **Não interpreta um replay inteiro** e
não verifica flags, cemitérios ou efeitos ausentes nas
capturas. Propriedades/IDs não conhecidos são modelados
com substitutos sintéticos no setup, documentados no
[relatório de evidência](../docs/evidence/joey-first-differential-normal-summon-2026-11-08.md).

Para os novos checks, use o mesmo comando existente:

```powershell
dotnet run --project .\src\tests\Joey.Tests\Joey.Tests.csproj
```

O usuário confirmou **144 checks** na versão anterior
a estes dois fixtures. Os novos testes ainda precisam
de execução no .NET do Windows.

### Capturas geradas automaticamente pelo YugiLab

O teste diferencial de invocação também aceita arquivos JSON
`auto-g*.json` gerados **somente a partir de transições
nativas confirmadas** por `yugilab.export_summons`.
Eles usam a mesma interface temporária de
`DemoDuel.FromPartialObservation`, mas verificam também
a mão inteira e os cinco slots de monstro quando há
evidência para esses campos.

```powershell
$env:YUGIRE_DIFFERENTIAL_FIXTURES = (Resolve-Path ".\scratch\differential-parallel-XXXXX").Path
try { dotnet run --project .\src\tests\Joey.Tests\Joey.Tests.csproj }
finally { Remove-Item Env:YUGIRE_DIFFERENTIAL_FIXTURES -ErrorAction SilentlyContinue }
```

Não há injeção de assembly no código de domínio. O hook
e os bitmasks permanecem no adaptador de engenharia reversa.
O setup ainda utiliza uma carta comum nível 4 substituta
quando o atributo real não foi capturado; portanto apenas
a **movimentação observada** é validada.
[Documentação de exportação](../docs/evidence/yugilab-automatic-summon-fixtures-2026-11-08.md).

### Validação opcional dos atributos reais com `data.dat`

A análise do primeiro ZIP automático encontrou **67 invocações,
mas somente 18 cartas internas distintas**. Os 67 fixtures
contêm identidade e posições, mas nenhum contém o nível ou
a elegibilidade nativa das cartas; sem o catálogo, o harness
continua usando um substituto nível 4 para a carta invocada.

Agora é possível executar um segundo nível de comparação:
com `YUGIRE_DIFFERENTIAL_FIXTURES` definido, passe
o `data.dat` original como argumento de `Joey.Tests`:

```powershell
$env:YUGIRE_DIFFERENTIAL_FIXTURES = (Resolve-Path ".\scratch\differential-parallel-7yq78u8y").Path
try {
    dotnet run --project ".\src\tests\Joey.Tests\Joey.Tests.csproj" -- "..\passion2\data.dat"
} finally {
    Remove-Item Env:YUGIRE_DIFFERENTIAL_FIXTURES -ErrorAction SilentlyContinue
}
```

O caminho de `data.dat` acima é um exemplo: use o
arquivo original efetivamente instalado no computador.
Depois que `Catalog.Load` carregar o DAT, o harness
realiza três verificações **adicionais por fixture**:
ID observado no comando CE = ID da carta do catálogo,
regras básicas de elegibilidade sem tributos para essa
carta, e resultado de `Summon` usando cartas e atributos
originais (não substitutos).

Falhas devem ser investigadas; não significa
automaticamente que o Joey original está errado, pois
o demo C# ainda não modela todas as exceções e efeitos.

[Distribuição completa das 67 invocações](../docs/evidence/yugilab-67-automatic-summon-fixture-coverage-2026-11-08.md).

## Procedência e limites

| Categoria | Uso nesta versão |
|---|---|
| Comprovado estaticamente por RE / recursos | BMPs e manifestos de posições/imagens, índice DAT, troca de nibbles, LZSS, IDs/índices, nomes, descrições e campos de propriedades; referências em `../docs/02-file-formats.md`. |
| Observado no jogo original | Organização geral de telas fornecida pelo usuário; fidelidade visual ainda não conferida neste ambiente. |
| Hipótese | Códigos de classe são preservados numericamente; não atribuímos nomes a classes ainda não confirmadas. |
| Demonstração | `Joey.Core/Demo.cs`: mãos/decks fixos, LP 8000, dois jogadores locais, turnos manuais, uma compra e invocação por turno, seleção local de tributos e combate simplificado por ATK/DEF. Classes 1–20 como monstros seguem o gate nativo; restrições especiais completas de invocação continuam ausentes. |

`Joey.Core/DeckFile.cs` transcreve o leitor YDC comprovado em
`../docs/02-file-formats.md`, preservando os oito bytes opacos do cabeçalho,
IDs, ordem e duplicatas. Importar esse deck para os dois jogadores do demo
e embaralhá-lo com semente fixa são escolhas locais de demonstração.

Os três equipamentos reutilizam `NativeAiAttributes.EquipmentDelta` e o caminho
comum de [Legendary Sword](../docs/cards/legendary-sword.md) e
[limpeza de equipamentos](../docs/cards/equipment-cleanup.md).
O [contrato e as políticas P025](../docs/cards/p025-implementation.md) separam
as propriedades dinâmicas resolvidas da aproximação estática do demo. Ao destruir
o alvo, cada equipamento vai ao cemitério de seu dono. Exceções de efeitos,
Banisher, fichas, remapeamento e negação não são reproduzidos.

Os nomes, atributos, fundos e ilustrações vêm do DAT. O menu usa os cinco
botões originais e suas dimensões; Card List usa também título e botão Back
originais, além de moldura, setas e números de página. As três telas usam
800×600, com escala proporcional ao redimensionar. A grade usa os passos
nativos de 55×87 pixels. A busca e os controles e posições das cartas no duelo
ainda são temporários. O menu usa dois quadros estáticos em lugar da animação.
Alguns registros apontam para imagens compartilhadas de placeholder no próprio
DAT; isso não significa que tenhamos uma ilustração exclusiva para cada carta.
Evidências e limites: [gráficos DAT](../docs/24-dat-graphics.md) e
[cinco magias de dano P045](../docs/cards/p045-direct-damage.md).
Ativação imediata sem correntes, cemitério, combate, bloqueio de batalha no
primeiro turno, derrota por deck vazio e vitória por LP são políticas do demo,
ainda não atribuídas ao motor original. A Alpha usa compra automática para a CPU e o sandbox oferece efeitos de monstros e armadilhas selecionados. O progresso por carta está em [CARDS.md](../CARDS.md).

## Validação

A partir de `src/`:

```bash
dotnet build Joey.App
dotnet run --project tests/Joey.Tests
dotnet run --project tests/Joey.Tests -- ../../passion/data.dat
dotnet run --project tests/Joey.Tests -- ../../passion/data.dat ../../passion/deck.ydc
dotnet run --project Joey.App -- --check-data
dotnet run --project Joey.App -- --check-graphics
```

`Joey.Tests` é um executável de verificações, sem framework externo; falha com
código diferente de zero. `dotnet test` não substitui esse comando. Os argumentos
DAT e deck habilitam verificações adicionais com os artefatos locais originais.

A validação desta integração e os comandos reproduzíveis estão no
[relatório do registry](../docs/coverage/integration-report.md#validacao-da-integracao):
1.051 checks Core/Lab/App com DAT/deck, 853 offline e 58 testes Python do registry,
com geração real dos relatórios. `--check-data` lê 1.114 registros não sentinela;
`--check-graphics` decodifica 793 BMPs sem abrir uma janela. Esses comandos não
substituem um smoke test interativo nem demonstram 1.114 cartas jogáveis.

Para comparar memória do original no Windows, há também
`scripts/Get-JoeyDuelSnapshot.ps1`, descrito em
[comparação de estado](../docs/26-normal-summon-tributes.md). Ele lê um processo
já aberto e produz JSON com LP, quantidade na mão e flags dos monstros, sem
alterar o jogo. O código C# de interop compilou; a execução PowerShell/Windows
ainda precisa ser conferida no ambiente do usuário.

O smoke test gráfico continua pendente: esta sessão não dispõe de
DISPLAY/WAYLAND_DISPLAY utilizável; a execução normal informa a ausência de
sessão gráfica e termina com código 2. Sockets locais não garantem acesso ou
autenticação. A validação de dados e imagens passou sem janela. NuGet pode
emitir NU1900 quando a consulta de vulnerabilidades estiver indisponível.

Roteiro visual pendente: abrir a janela em um desktop, percorrer os destinos do
menu, pesquisar cartas, mudar páginas e redimensionar; carregar um deck local com
as cinco magias/três equipamentos, testar seleção nos dois lados, alvo incompatível,
ativação de magia setada, ATK/DEF e limpeza. Não executar o Joey original para esse
smoke test; ele diz respeito apenas ao protótipo `Joey.App`.

Conferência posterior por RE: a comparação nativa só destrói ambos em empate
de ATK **positivo** ([evidência](../docs/22-battle-core.md)). O protótipo foi corrigido para manter ambos em 0 contra 0,
com teste que falhava antes da correção. Os demais limites do modo de
demonstração continuam válidos; não foi feita validação visual nesta sessão.


## Organização dos projetos

- `Joey.Core`: engine independente, sem Frida/Python ou UI.
- `Joey.App`: interface raylib-cs, com referência a Core.
- `Joey.Lab`: CLI de análise offline, também com referência direta a Core.

Da raiz do repositório:

```bash
dotnet run --project src/Joey.Lab -- summarize caminho/captura.jsonl --json
```

[Comandos e limites do laboratório](../docs/research/joey-lab-migration.md).
Instrumentação do processo original permanece em `native_hooks/`;
`yugilab/` é o laboratório Python legado durante a migração.
