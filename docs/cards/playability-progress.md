# Progresso de jogabilidade — 771 cartas dos três Power of Chaos

O objetivo é medir **quantas cartas funcionam no Joey.Core**, sem confundir isso com o inventário de engenharia reversa `docs/coverage/`.

## Denominador

O `Catalog.Load` lê **1.114 registros internos** (sem sentinela), incluindo versões e registros que não fazem parte das três listas. O filtro `CardListResources.ForPacks(cards, card_pack, mask:7)` seleciona **771 registros** da união Yugi/Kaiba/Joey. O pack **Joey** isolado contém **350 registros** (mask:4). Há sobreposição entre os jogos: não some 771+350.

A identidade principal é **índice interno + fingerprint do DAT**. `resourceId` identifica a regra de carta, mas não substitui o índice: versões/aliases podem compartilhar comportamento e ainda ser registros distintos. O numerador é contado **somente** entre os registros que aparecem na união dos packs (ou no subset Joey). Um resource ID presente no manifest mas ausente daquele filtro não conta. Por isso **não** publicar uma porcentagem calculada apenas de 24/771 sem executar o gerador no DAT.

## Como gerar (Windows, PowerShell ou Bash, .NET 10 + Python)

Na raiz do repositório, com `../passion/data.dat` disponível localmente:

```powershell
python scripts/card_playability.py --dat ../passion/data.dat
```

O script executa **somente** o extrator read-only de `src/Joey.Coverage`: não inicia o jogo, não exige Frida, não precisa do EXE, não copia descrições ou assets e escreve os resultados em `scratch/card-playability/` (ignorados pelo Git).

Arquivos:
- `cards.csv` — todas as identidades com jogo(s), classe nativa, categoria, subtipo, mecânica, perfil de callback e estado de implementação.
- `cards.json` — os mesmos registros em formato estruturado.
- `summary.json` — porcentagens gerais por pack e por tipo de carta.
- `PROGRESS.md` — painel Markdown com **Done+Verified / total** e fila a classificar.

Pode-se usar extração existente do `Joey.Coverage` diretamente (objeto bruto com campos `cards` e `pack`):

```powershell
python scripts/card_playability.py --extracted scratch/coverage-current/extracted.json
```

Não usar `attributes.json` no lugar de `extracted.json`: o primeiro não contém nomes no formato de entrada. Para gerar o bruto sem rodar o programa inteiro, o bridge `Joey.Coverage` produz JSON e também pode ser redirecionado para um arquivo local.

## Badges visíveis no GitHub

O README mostra a badge CARDS via Shields.io, alimentada por
`docs/cards/badges/all-packs.json` (771); o recorte Joey (350) permanece no relatório.
ACTIONS e VISUALS têm métricas próprias, descritas no README e no PLANS.md.
O relatório público `docs/cards/PLAYABILITY.md` mostra as categorias e os
subtipos de monstros. Os valores são reproduzidos pelo comando:

```powershell
python scripts/publish_playability.py
python scripts/publish_playability.py --check
```

O workflow `.github/workflows/playability-badges.yml` recalcula e publica os
arquivos derivados quando o manifest, a classificação ou o script são alterados
na `main`. O workflow precisa de permissão de escrita do GitHub Actions;
o código e os artefatos derivados também são mantidos no repositório.
**Mudar a classificação não aumenta Done.** O denominador vem de
`docs/cards/pack-index.json`, que contém apenas resource IDs, mask de pack
e código Monster/Spell/Trap, extraídos do DAT original sem textos ou imagens.

## Subtipos de monstros

A tabela `docs/cards/monster-subtypes.json` classifica **499 dos 499 monstros**
por correspondência de nomes públicos e aliases históricos: **264 Normal,
193 Effect, 34 Fusion e 8 Ritual**. Nenhum permanece ToReview na referência.
Os 87 antigos desconhecidos foram resolvidos usando mais uma base pública e
renomeações documentadas (ex.: Frog the Jam → Slime Toad, Harpie's Brother →
Sky Scout e Kinetic Soldier → Cipher Soldier). Nenhuma dessas
fontes atesta que a engine C# implemente os efeitos ou que o tipo no jogo
original seja necessariamente igual ao catálogo moderno. O CSV local agora
separa `subtype` de `subtypeSource` para evitar essas confusões.

Fontes de classificação:
- https://github.com/omar39/yugioh-dataanalysis/blob/main/card_data.csv
- https://github.com/mahinb/yugioh-card-data-analysis/blob/main/card_data_small.csv
- https://github.com/maxDepa/YGOllama/tree/main/DataSet/Cards
- https://www.db.yugioh-card.com/yugiohdb/
- https://www.yugioh.com/cards
- https://ftp.yugipedia.com/wiki/

Para conferir a classificação com o formato histórico, execute no Windows a
suíte `Joey.Tests` apontando para `../passion/data.dat`. O teste
`NormalMonsterPlayabilityChecks.RunWithCatalog` compara os 264 registros Normal
com sua categoria nativa, nível e regras de invocação usando as propriedades
reais. Essa etapa é opcional para o desenvolvimento rápido do sandbox e ainda
precisa de execução manual. Ausência de callback **não** significa Normal.

## Categorias e progresso

A classe nativa vem de `(Properties >>20)&31`. Âncoras no catálogo de callback confirmam classe **21 = Trap** (Mirror Force, Trap Hole) e **22 = Spell** (Sparks, Sogen). Classes **1..20 = Monster**; classes especiais 0/23/24 ficam **Other** até revisão. Subtipos dos monstros foram associados a referências externas; bits 18–19 de propriedades entram nas regras de categoria de invocação já implementadas em `NormalSummonRules`, mas divergências entre as fontes públicas e o DAT devem ser auditadas. Outros subtipos de magias/armadilhas ainda podem aparecer como `ToReview`.

O estado se baseia em `docs/cards/playability-manifest.json`, com resource IDs explícitos e evidência do código/testes:

- **Pending:** não há implementação específica cadastrada (monstros podem ter invocação/combate genéricos).
- **Partial:** existe mecânica incompleta ou limitada por carta (os três equipamentos).
- **Done:** efeito básico acionável no `DemoDuel`, coberto por testes no escopo documentado.
- **Verified:** carta inteira comparada com o Joey original em cenário equivalente. **Nenhum caso é promovido automaticamente**.

A porcentagem de conclusão usa `(Done+Verified)/total`, e não o número de checks unitários. O CSV diferencia classificação da carta e cobertura: **264 Normals estão Done somente no escopo de ações básicas testadas**; isso não prova que os 193 monstros de efeito, fusões e rituais estejam implementados.

### Taxonomia para a próxima revisão

| Tipo principal | Subtipos a classificar | Famílias de efeito (múltiplas por carta) |
| --- | --- | --- |
| Monster | Normal, Effect, Fusion, Ritual | FLIP, invocação especial, destruição, compra, cemitério, combate, contínuo, substituição |
| Spell | Normal, Equip, Field, Continuous, Ritual, Quick-Play | LP, ATK/DEF, busca, compra, destruição, controle, posições, restrições |
| Trap | Normal, Continuous, Counter | Ataque, invocação, destruição, dano, negação, resposta, custos |
| Other | Casos especiais sujeitos a inspeção | Desconhecido |

**Uma carta pode pertencer a mais de uma família.** A categoria (Monster/Spell/Trap) é mutuamente exclusiva, mas `mechanics` é uma lista. Perfis de callback `Pxxx` ajudam a formar lotes de investigação, **não** provam o mesmo efeito para todos os membros.

## Como manter a métrica atualizada

Ao adicionar uma carta jogável, acrescente seu **resource ID exato** e teste em `playability-manifest.json`; use `Partial` para funcionalidade incompleta e `Done` apenas após teste jogável. Não normalizar `+2000` automaticamente. `CARDS.md` explica as limitações funcionais. Gere o painel local para consultar a nova porcentagem; somente publique um snapshot no repositório após executá-lo com o DAT original e conferir as contagens.

Esta métrica é **independente** de `docs/coverage/progress.md` (baseline antigo de RE, 1.114 registros, validação nativa conservadora). Ambas são úteis, mas respondem perguntas diferentes.
