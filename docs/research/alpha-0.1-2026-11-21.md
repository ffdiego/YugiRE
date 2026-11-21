# YugiRE Alpha 0.1 — 2026-11-21

Abra **Alpha - vs CPU** no menu ou inicie com `--alpha`. O jogador fica embaixo e a CPU em cima. O campo mantém essa perspectiva após cada troca de turno. Ambos começam com cinco cartas e decks de 40 cartas da mesma lista, embaralhados separadamente. A mão rival mostra apenas quantidade e versos. O modo usa a tela de duelo existente com um campo próprio. O DAT local fornece os dados e recursos que o app já sabe ler.

```bash
dotnet run --project src/Joey.App -- ../passion/data.dat --alpha
dotnet run --project src/Joey.App -- ../passion/data.dat --alpha --fast
dotnet run --project src/Joey.App -- ../passion/data.dat --alpha --instant
dotnet run --project src/Joey.App -- ../passion/data.dat --alpha-headless
dotnet build YugiRE.sln
dotnet run --project src/tests/Joey.Tests
dotnet run --project src/tests/Joey.Tests -- ../passion/data.dat ../passion/deck.ydc
```

Use **Draw** para começar seu turno. Passe o mouse sobre a mão para ver a carta, clique para invocar ou ativar e use o botão direito para alternar para **Set**. Em **Battle**, clique em um atacante e depois no alvo ou em **Direct attack** se o campo adversário estiver vazio. **End turn** pede descarte se você tiver mais de seis cartas. Durante o turno da CPU, os comandos de turno ficam indisponíveis. Quando uma armadilha ou um FLIP lhe dá uma escolha, a CPU aguarda que você a resolva. O painel indica as ações, LP, contagens e o resultado. **Reset** inicia um novo duelo.

## Regras e decisões

A lista inclui oito monstros normais, Man-Eater Bug, Morphing Jar, Magician of Faith, Mask of Darkness, cinco magias de LP, Pot of Greed, Upstart Goblin, Legendary Sword, Trap Hole e Mirror Force. As regras de invocação, batalha, FLIP, recuperação, descarte e respostas vêm de `DemoDuel`. A vitória ocorre por LP zero ou falha de compra. O sandbox pode declarar empate se nenhum lado consegue completar a compra do Morphing Jar.

O `CpuDuelController` fica no Core e age um passo por chamada. Usa ATK/DEF efetivos para escolher confrontos e uma estimativa fixa para alvos ocultos. Invoca, coloca em defesa, usa magias e equipamento compatível, configura armadilhas e resolve as escolhas que lhe pertencem. Encerra o turno quando esgota as jogadas vantajosas. O limite tático de 48 passos reserva espaço para encerrar o turno e descartar. O teto de 256 passos produz um diagnóstico explícito se um estado inesperado impedir a continuação. Os módulos `NativeAi*` documentam ranking e simulação em contextos mais estreitos. A estratégia usa as regras C# existentes e heurísticas próprias, sem alegação de equivalência ao seletor de Joey. As respostas de armadilhas são janelas pontuais do sandbox, sem correntes gerais, fusões, rituais ou multiplayer.

Pot of Greed (1010, P119) compra duas cartas. Upstart Goblin (1065, P143) compra uma e recupera 1.000 LP para o adversário. O resolver usa o texto do DAT local e os IDs do catálogo de callbacks. Ele aceita mão ou Set, valida a quantidade de cartas antes de consumir a magia e mantém a compra normal do turno separada. Ambos entram como `Done` apenas no sandbox. A lista é de demonstração e não aplica banlist.

## Apresentação e evidência

| Movimento | Dados de origem | Aproximações da alpha |
| --- | --- | --- |
| Mão → zona | `HandPresentation`, centros de `NativeDuelLayout` e interpolação de posição de `00418B60`. O contador soma `0x20` por atualização até `0x100`, com peso `C[(t/2 - 0x80) & 0x1ff]` em x16.16. | Mão superior compacta, tamanhos, escala do desenho, posições verticais de magias. Oito updates a 30 passos/s em Classic e 60 em Fast. |
| Ativação e destaque de Flip Summon | `NativeActivationTimeline`, +16 por atualização, três passes, escalas e rotações documentados. A prévia existente lê os YGAs locais. | 30 passos/s em Classic e 60 em Fast. Categoria visual de FLIP escolhida pelo cliente e mistura de pixels da raylib. |
| Ataque | Usa os centros das zonas de monstros. | Trilha linear na declaração e trajetória de ataque direto escolhidas pelo cliente. A resolução de dano continua imediata no Core. |

O Ghidra em modo `-readOnly -noanalysis` confirmou a interpolação e a inicialização da tabela. O loop `0055DCFE..0055DD3C` grava 512 inteiros em `00A52EE8` após `FCOS`, multiplicação e conversão. As constantes nos VAs `005DCCF8`, `005DCCB0` e `005DCD00` são π em `double`, 1/256 e 65536. A tabela nasce em memória. O app recalcula seus pesos com `Math.Cos`, com precisão bit a bit e frequência do Joey ainda desconhecidas. O SHA-256 do EXE corresponde a `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`. Consultas reproduzíveis com `scripts/run-archive-query.sh` nos endereços `00418B60`, `xref:00A52EE8` e `0055DCF0`. O contrato consolidado tem o restante da proveniência. [Contrato visual](../architecture/native-visual-contract.md)

Os presets Classic, Fast e Instant controlam somente a apresentação e o intervalo entre passos da CPU. A CPU e a engine não consultam relógios. A animação de explosão nativa permanece documentada e ainda não é ligada ao duelo.

Em defesa, o rosto ou o verso da carta é desenhado na horizontal. Depois do teste em janela, Hinotama (343) e Final Flame (344) substituíram as duas cópias de Ookazi (345). No DAT, Ookazi está fora dos packs (`pack=0`) e aponta para `token_sl.bmp`, o mesmo recurso do Slime Token (1923). A alpha agora resolve sua lista apenas entre cartas dos packs, inclusive se `--include-unused` estiver ativo na lista de consulta.

## Verificação e próximos passos

Os testes cobrem vitória, derrota, deck-out, descarte, ausência de opções vantajosas, seleções pendentes inclusive fora do turno da CPU, duelos determinísticos sob Classic e Instant e 30 partidas completas usando o DAT local. `dotnet build YugiRE.sln` passou com aviso `NU1900` na consulta de vulnerabilidades do NuGet. `Joey.Tests` passou 2.827 verificações offline e 4.183 com DAT/deck. `--alpha-headless` usa a mesma lista e o mesmo controlador que a interface. Na seed 1979, o primeiro lado venceu no turno 21 com 6.200 LP. `--check-graphics` decodificou 797 recursos, incluindo os quatro sprites de ativação. O gerador e os testes Python de jogabilidade passaram. A sessão Fedora não tem um display utilizável. Falta conferir os cliques e os movimentos numa janela no Windows ou num desktop Fedora.

1. Rodar um duelo humano completo em janela, incluindo respostas de armadilhas e FLIP nos dois lados.
2. Ajustar o ritmo visual e o feedback de destruição com base nessa rodada.
3. Ampliar a lista com famílias de efeitos testadas e rodar as mesmas seeds para detectar partidas que deixaram de progredir.
