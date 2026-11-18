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

## Categorias e progresso

A classe nativa vem de `(Properties >>20)&31`. Âncoras no catálogo de callback confirmam classe **21 = Trap** (Mirror Force, Trap Hole) e **22 = Spell** (Sparks, Sogen). Classes **1..20 = Monster**; classes especiais 0/23/24 ficam **Other** até revisão. A classificação de efeito/natureza exata para monstros e magias não anotados é **ToReview** — não se inventam subtipos a partir de bits 18–19, cujo significado está em aberto.

O estado se baseia em `docs/cards/playability-manifest.json`, com resource IDs explícitos e evidência do código/testes:

- **Pending:** não há implementação específica cadastrada (monstros podem ter invocação/combate genéricos).
- **Partial:** existe mecânica incompleta ou limitada por carta (os três equipamentos).
- **Done:** efeito básico acionável no `DemoDuel`, coberto por testes no escopo documentado.
- **Verified:** carta inteira comparada com o Joey original em cenário equivalente. **Nenhum caso é promovido automaticamente**.

A porcentagem de conclusão usa `(Done+Verified)/total`, e não o número de checks unitários. O CSV também diferencia a infraestrutura de monstro normal do comportamento específico da carta: suporte a invocação e combate não prova que centenas de monstros de efeito estejam prontos.

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
