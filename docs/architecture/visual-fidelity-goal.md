# Meta de fidelidade visual do YugiRE — pixels, geometria e tempo

## Decisão de produto e critério técnico

O `YugiRE` reimplementa **o resultado visual** de `joey_pc.exe` com a maior fidelidade observável possível. A meta é que, em estados e instantes equivalentes, os elementos visuais ocupem **as mesmas posições em pixels, tenham os mesmos tamanhos, rotações, recortes, sprites, cores, composição e duração das animações**.

**Não é necessário replicar o código nativo, as classes, o renderizador GDI, o loop de execução ou o algoritmo interno**, desde que a saída observável coincida. Usar C#/raylib e modelos próprios para obter os valores nativos é preferível a uma tradução literal de assembly. Não confundir *fidelidade em pixels* com *matching binário de instruções*: o primeiro é o objetivo, o segundo não.

A referência espacial é o canvas lógico do Joey em **800×600**. Comparar no canvas antes de qualquer ampliação da janela (zoom, letterbox ou DPI), com cenário, turno, lado, fase, assets e instante equivalentes. A fidelidade é progressiva na alpha, mas o escopo-alvo final é **todos os elementos visuais**, não só as cartas.

## Componentes cobertos pelo objetivo

| Família | Propriedades a recuperar e comparar |
| --- | --- |
| Campo, zonas e UI base | fundo, dimensões, centros, âncoras, espelhamento, contornos, recortes e prioridades de desenho |
| Cartas na mão/campo/pilhas | largura/altura, origem/pivô, imagem frente/verso, facing, ângulo de defesa, face-down, perspectiva por lado, interpolação e animação de entrada/saída |
| Textos e números | posição, alinhamento, tipo/tamanho dos dígitos, escala, orientação e cor: ATK/DEF acima do adversário e abaixo do próprio, sem girar automaticamente os textos junto com a carta |
| Animações de jogada | trajetórias, escala, rotação, curvas, frames, pausas, ordem de atualização e execução, gates de decisão, filas e efeitos subsequentes |
| Efeitos e partículas | sprites DAT, quadros, máscaras, opacidade, composição, brilho, explosão, danos, seleção, hover e indicadores |
| HUD e transições | LP, nomes, textos, detalhe da carta, fase, botões, prompts, marcadores, espada/mira, trocas de jogador, entrada/saída de cenas |

Não confundir **orientação de 180° do adversário** com **face-down**: frente/verso e rotação são variáveis independentes. A rotação de defesa também deve compor com o lado. O código atual do cliente pode estar incorreto mesmo quando a regra do Core estiver correta.

## Tempo e ordenação

- Recuperar da análise estática o **contador por update**, seu incremento, a condição de término, os estados e a origem das esperas; separar `Update`, `Draw`, produtor de comandos, consumidor e pausas de UI.
- A documentação `native-visual-contract.md` registra a cadência **nominal** do executável: **30 updates/s** no modo normal e **60** sob `-speedy`. Essa informação é uma referência para equivalência temporal, **não garante** que cada animação atualize uma vez por ciclo nem que vídeo capturado reproduza cada quadro.
- Para o perfil **Classic**, preferir a cadência, duração e sequência nativas quando sustentadas por evidência. `Fast` e `Instant` são variações opcionais e podem intencionalmente se afastar do original.
- Duração total sozinha não basta: comparar *quadros-chave*, ordem relativa, intervalos de espera, easing/interpolação, posição/ângulo/alpha por update e quando a ação seguinte pode começar.
- Um evento de jogo pode produzir vários comandos de apresentação. `Joey.Core` decide as regras; `Joey.LookAndFeel` agenda apresentação sem multiplicar efeitos lógicos nem bloquear permanentemente o jogo.

## Método de engenharia reversa e validação

1. **Definir o cenário**: resolução 800×600; carta, side, fase, posição, estado face-up/down, ação e instante; salvar referência do original com provenance e, se disponível, contadores nativos ou vídeo com timestamp.
2. **Localizar no Ghidra** a rotina que atualiza/desenha o elemento, mas sobretudo os **escritores dos campos** que alimentam posição, dimensão, ângulo, escala, timers e parâmetros de composição. Inspecionar tabelas, constantes, vtables, callers e dependência do side/slot.
3. **Identificar os assets** reais no `data.dat` local (BMP/YGA/manifests); consumir sem publicar DAT ou reproduções integrais no Git. Não substituir sprites disponíveis por retângulos genéricos como aparência final.
4. **Extrair um contrato de resultado** reproduzível: `x,y,w,h`, centro/pivô, ângulo, escala, clipping, camada, tipo de sprite, temporização (updates, taxa, início/fim), cenário e endereço de origem. Deixar explícito o espaço de coordenadas e arredondamentos.
5. **Implementar** a fórmula ou um método alternativo que dê os mesmos pixels/updates em `Joey.LookAndFeel`; o `Joey.App` converte isso em desenho raylib. Evitar cálculos duplicados e números mágicos espalhados pela UI.
6. **Comparar** capturas e quadros-chave do original e do cliente no mesmo cenário. Quando houver referência em pixels, calcular diferenças de coordenadas/dimensões; quando houver timing nativo, comparar índices de update e eventos. Regressões devem virar teste determinístico, além de revisão visual quando necessária.

### Níveis de evidência

- **Estático confirmado:** fórmula, lookup, constante, campo e uso pelo renderer identificados no código da versão fixada do EXE.
- **Observado no original:** pixel/ângulo/ordem/tempo identificado em screenshot, vídeo ou trace, com condição e limites da amostragem.
- **Derivado:** valor calculado de evidência sustentada (por exemplo, contador de updates × cadência nominal). Declarar premissas.
- **Provisório:** estimativa do cliente, fallback ou inferência sem confirmação. Não chamar de nativo nem ocultar como valor fixo no contrato.

Uma medida estática não demonstra sozinha que o mesmo caminho é usado em todo contexto. Um vídeo não mede com precisão a cadência interna do original. Ambos são úteis se o grau de prova estiver indicado.

### Metas de aceitação

- **Geometria:** para elementos estáticos com coordenadas nativas conhecidas, alvo **exato em pixels lógicos** (erro 0 px em `x,y,w,h`). Para elementos animados, comparar a coordenada/arredondamento a cada update ou em quadros-chave equivalentes. Não alegar erro zero sem comparação.
- **Orientação e composição:** frente/verso, rotação, ordem de camadas, ancoragem de texto e clipping corretos para os dois lados e todas as posições. Testar ataque/defesa × face-up/down × próprio/adversário.
- **Tempo:** para animações com contador e cadência demonstrados, alvo de **mesmo número de updates** e mesma ordem de eventos; discrepâncias em frames ou milissegundos devem ser medidas, não corrigidas por feeling.
- **Pixels finais:** quando houver sprites, cores e blending recuperáveis, comparar capturas em coordenadas equivalentes; diferença bitmap completa pode depender de GDI vs. GPU, antialiasing, driver e captura. Registrar diferenças residuais sem usar a divergência de implementação como justificativa para posições erradas.
- **Escopo:** correção localizada não encerra o objetivo global. Priorizar na alpha a sequência carta/campo/ATK-DEF/ataque/fase/LP/UI e expandir até todas as famílias visuais.

O aceite final de cada componente deve apontar para evidência + teste/captura + lista de desvios conhecidos. **"Parece similar" não substitui valores comparáveis**; tampouco uma estimativa vira prova porque foi movida para uma classe chamada `Native*`.

## Fontes do repositório e restrições

- [Contrato nativo e endereços conhecidos](native-visual-contract.md): posições, rotações investigadas, contadores, frames e cadência nominal.
- [Política do LookAndFeel](look-and-feel.md): aplicação dos valores e presets.
- [Prévia de ativação](activation-preview.md): implementação parcial e limites de blending/tabela de cosseno.
- [Notas de fluxo de comandos](../research/ai-decision-engine.md) e `docs/research/native-action-scheduler.md`: distinguir agenda lógica da apresentação.
- `scripts/run-archive-query.sh` e `scripts/QueryArchive.java`: consultas read-only ao Ghidra.

Não versionar `joey_pc.exe`, `data.dat`, saves, dump integral proprietário ou capturas privadas. Documentar endereços VA junto ao fingerprint do executável relevante. Refatorações de visual não podem modificar regras do `Joey.Core`; validar com testes existentes e revisão de captura onde possível.
