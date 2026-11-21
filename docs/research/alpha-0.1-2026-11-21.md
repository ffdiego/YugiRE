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

O turno começa com compra automática. O painel vertical junto ao campo mostra **DP**, **SP**, **M1**, **BP**, **M2** e **EP**. Clique em **BP** para batalhar, **M2** para voltar às ações de fase principal após a batalha e **EP** para encerrar. Se você tiver mais de seis cartas, a fase final pede descarte. Passe o mouse sobre a mão para ver a carta, clique para invocar ou ativar e use o botão direito para alternar para **Set**. Na batalha, clique em um atacante e depois no alvo ou em **Direct attack** se o campo adversário estiver vazio. Quando uma armadilha ou um FLIP lhe dá uma escolha, a CPU aguarda sua resposta. **Reset** inicia um novo duelo.

Para usar um tema próprio ou licenciado, passe `--duel-theme=/caminho/do/tema`. A pasta pode conter `board.png` (fundo do campo, área lógica de 570 × 600), `card-back.png` (verso vertical) e `font.ttf` (texto da tela de duelo). Cada arquivo ausente usa o padrão do cliente. O duelo local tem o fundo do DAT por padrão. A Alpha tem tabuleiro próprio. Ambos usam verso desenhado pelo cliente e fonte padrão do raylib.

## Regras e decisões

A lista inclui oito monstros normais, Man-Eater Bug, Morphing Jar, Magician of Faith, Mask of Darkness, cinco magias de LP, Pot of Greed, Upstart Goblin, Legendary Sword, Trap Hole e Mirror Force. As regras de invocação, batalha, FLIP, recuperação, descarte e respostas vêm de `DemoDuel`. A vitória ocorre por LP zero ou falha de compra. O sandbox pode declarar empate se nenhum lado consegue completar a compra do Morphing Jar.

Os cemitérios ficam no fim das fileiras de monstros, espelhados entre os dois lados. A fila visual apresenta uma destruição por vez, inclusive quando Raigeki limpa o campo no duelo local. Morphing Jar mostra os descartes individuais antes das compras, sem revelar a mão da CPU. O motor resolve os efeitos de forma síncrona e o cliente reproduz os eventos na ordem em que os recebeu. A ordem visual por lado segue o motor C#. Os avisos de fase usam 32 updates (40 para Battle), com posicionamento e composição do cliente. A Standby exibida acompanha o gancho existente após a compra. [Contadores recuperados](../architecture/native-visual-contract.md#contadores-do-executável-e-efeito-de--speedy).

O `CpuDuelController` fica no Core e age um passo por chamada. Usa ATK/DEF efetivos para escolher confrontos e uma estimativa fixa para alvos ocultos. Invoca, coloca em defesa, usa magias e equipamento compatível, configura armadilhas e resolve as escolhas que lhe pertencem. Encerra o turno quando esgota as jogadas vantajosas. O limite tático de 48 passos reserva espaço para encerrar o turno e descartar. O teto de 256 passos produz um diagnóstico explícito se um estado inesperado impedir a continuação. Os módulos `NativeAi*` documentam ranking e simulação em contextos mais estreitos. A estratégia usa as regras C# existentes e heurísticas próprias, sem alegação de equivalência ao seletor de Joey. As respostas de armadilhas são janelas pontuais do sandbox, sem correntes gerais, fusões, rituais ou multiplayer.

Pot of Greed (1010, P119) compra duas cartas. Upstart Goblin (1065, P143) compra uma e recupera 1.000 LP para o adversário. O resolver usa o texto do DAT local e os IDs do catálogo de callbacks. Ele aceita mão ou Set, valida a quantidade de cartas antes de consumir a magia e mantém a compra normal do turno separada. Ambos entram como `Done` apenas no sandbox. A lista é de demonstração e não aplica banlist.

## Apresentação e evidência

| Movimento | Dados de origem | Aproximações da alpha |
| --- | --- | --- |
| Mão → zona | `HandPresentation`, centros de `NativeDuelLayout` e interpolação de posição de `00418B60`. O contador soma `0x20` por atualização até `0x100`, com peso `C[(t/2 - 0x80) & 0x1ff]` em x16.16. | Mão superior compacta, tamanhos, escala do desenho, posições verticais de magias. Oito updates a 30 passos/s em Classic e 60 em Fast. |
| Ativação e destaque de Flip Summon | `NativeActivationTimeline`, +16 por atualização, três passes, escalas e rotações documentados. A prévia existente lê os YGAs locais. | 30 passos/s em Classic e 60 em Fast. Categoria visual de FLIP escolhida pelo cliente e mistura de pixels da raylib. |
| Ataque | Usa os centros das zonas de monstros. | Trilha linear na declaração e trajetória de ataque direto escolhidas pelo cliente. A resolução de dano continua imediata no Core. |

O Ghidra em modo `-readOnly -noanalysis` confirmou a interpolação e a inicialização da tabela. O loop `0055DCFE..0055DD3C` grava 512 inteiros em `00A52EE8` após `FCOS`, multiplicação e conversão. As constantes nos VAs `005DCCF8`, `005DCCB0` e `005DCD00` são π em `double`, 1/256 e 65536. A tabela nasce em memória. O app recalcula seus pesos com `Math.Cos`, com precisão bit a bit e frequência do Joey ainda desconhecidas. O SHA-256 do EXE corresponde a `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`. Consultas reproduzíveis com `scripts/run-archive-query.sh` nos endereços `00418B60`, `xref:00A52EE8` e `0055DCF0`. O contrato consolidado tem o restante da proveniência. [Contrato visual](../architecture/native-visual-contract.md)

Os presets Classic, Fast e Instant controlam somente a apresentação e o intervalo entre passos da CPU. A CPU e a engine não consultam relógios. A explosão usa as janelas do contador nativo e partículas geradas pelo cliente.

Em defesa, o rosto ou o verso da carta é desenhado na horizontal. Depois do teste em janela, Hinotama (343) e Final Flame (344) substituíram as duas cópias de Ookazi (345). No DAT, Ookazi está fora dos packs (`pack=0`) e aponta para `token_sl.bmp`, o mesmo recurso do Slime Token (1923). A alpha agora resolve sua lista apenas entre cartas dos packs, inclusive se `--include-unused` estiver ativo na lista de consulta.

## Verificação e próximos passos

Os testes cobrem vitória, derrota, deck-out, descarte, seleções fora do turno da CPU, ordem dos eventos por carta e 30 partidas completas com o DAT local. `dotnet build YugiRE.sln` passou com aviso `NU1900` na consulta de vulnerabilidades do NuGet. `--alpha-headless` usa a mesma lista e o mesmo controlador que a interface. Na seed 1979, o primeiro lado venceu no turno 21 com 6.200 LP. A janela raylib em um compositor virtual do Fedora mostrou a compra automática, os avisos de fase, a transição para o turno da CPU e as zonas espelhadas. Ainda falta percorrer um duelo humano completo em janela.

1. Rodar um duelo humano completo em janela, incluindo respostas de armadilhas e FLIP nos dois lados.
2. Ajustar o ritmo visual e o feedback de destruição com base nessa rodada.
3. Ampliar a lista com famílias de efeitos testadas e rodar as mesmas seeds para detectar partidas que deixaram de progredir.
