# Indicadores nativos sobre as cartas — pesquisa assembly-first (22/11/2026)

## Evidências e distinções importantes

Base estática: `joey-knowledge-visual-decoded.sqlite` (6.518 funções) do
`joey_pc.exe` SHA-256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Funções consultadas: `004182E0`, `00418EF0`, `004194D0`, `00419B70`,
`0041A070`, `0041A3B0`, `0041A800`, `00413DB0`, `00417F40`,
`004025D0`, `00402650` e `0056DD30`.

**São duas famílias de recursos, com propósitos distintos:**

- `004182E0` carrega **`duel/card/card.txt`** na coleção
  `DAT_005F2FD8`. Todos os Draws estudados aqui consultam essa
  coleção para os índices 4..13. Os índices 0..3 são os quatro
  `card_waku#*.bmp` de foco já mapeados por `NativeCardSelectionFrame`.
- `0041A800` carrega também **`duel/card/icon.txt`**, mas para um
  objeto diferente (vista/lista de cartas). Não associar seus ícones,
  índices ou tamanhos à coleção `DAT_005F2FD8` sem evidência adicional.

`004194D0` executa os desenhos de status **depois** da linha
DEF/separador/ATK. O gate de chamada dos três Draws é:
`(descriptor & 0x8000) != 0` e `(descriptor & 0x3e) < 0x15`.
A condição não garante que todos renderizem um sprite; cada Draw tem
seus próprios filtros.

## Banco de índices do `card.txt`

| Índices | Origem | Papel comprovado / parcial |
| --- | --- | --- |
| 0..3 | `00418EF0` | Quadros de seleção da carta |
| 4 | `00419B70` | Indicador de alteração do atributo (`FUN_00402650`) |
| 5 | `00419B70` | Indicador de alteração do tipo (`FUN_004025D0`) |
| 6 | `0041A070` | Indicador A, flags de campo bits 2..5; animação em estado 3 |
| 7 | `0041A070` | Pass adicional do indicador A durante a saída/transição |
| 8 | `0041A3B0` | Indicador B, flags de campo bits 6..8; animação em estado 4 |
| 9 | `0041A3B0` | Pass adicional do indicador B durante a saída/transição |
| 10..11 | `004194D0` | Pulso alternado de mudança positiva de ATK/DEF |
| 12..13 | `004194D0` | Pulso alternado de mudança negativa de ATK/DEF |

Os índices 4/5 comparam tipo/atributo efetivo com a propriedade base
da carta: tipo modificado no intervalo 1..20, atributo modificado no
intervalo 1..6. O ramo de `00419B70` que trata `card ID 0x48A` e
campos excepcionais **não** está implementado em `NativeCardStatusOverlay`.

Os índices 6/7 e 8/9 têm **mais de um estágio**. O C# reproduz apenas
o seletor de frames, sem inferir o significado das flags de campo ou
construir um objeto gráfico inexistente.

### Detalhes dos contadores extraídos

- `0041A070`: campo `(flags >> 2) & 0xF`. Renderiza se o campo é
  não zero ou se o estado do objeto é 3.
  Estado 3 e contador `t < 256` selecionam no índice 6
  `floor(t/64) + 2` (frames 2, 3, 4, 5).
  Fora desse ramo, seleciona `(globalCounter >> 4) % 3`.
  Se estado 3 e `t >= 256`, acrescenta índice 7 com
  frame `(-t >> 4) & 7`.
- `0041A3B0`: campo `(flags & 0x1C0) >> 6`.
  Renderiza se o campo é não zero ou se o estado é 4.
  Estado 4 e `t < 256` selecionam no índice 8
  `floor(t/64)` (frames 0..3); fora do ramo, frame 0.
  Se estado 4 e `t >= 256`, acrescenta índice 9 com
  frame `(-t >> 4) & 7`.
- `004194D0`: ao surgir delta de ATK/DEF, o índice
  10/11 (positivo) ou 12/13 (negativo) é escolhido por
  `(globalCounter >> 2) & 1`. Esses efeitos são separados
  do atlas de números `duel_fontm`.

`0041A070` também chama `00480690`; `0041A3B0` chama
`004803C0`. O propósito exato dos algarismos e a ligação dos
campos ao `Joey.Core` permanecem em estudo. Não declarar que os
bits representam equipamento, contadores ou nível sem provar.

## Incremento no projeto (sem fingir uma Draw implementada)

`NativeCardStatusOverlay.cs`, em `Joey.LookAndFeel`, contém os índices
e as funções puras de seleção de estado/frame, com testes sintéticos:
gates de mudança de tipo/atributo, valores de bits, transições
nos limites 0x100 e escolha dos quatro flashes.

`scripts/summarize_native_card_status_dat.py` usa o resultado já
existente de `--inspect-visual-dat`, verifica as âncoras do `card.txt`
nos índices 0..3 e gera **apenas metadados** dos índices 4..13
(nomes, largura, altura, presença). Não adivinha o nome de um sprite.
Se as âncoras divergem, falha com erro; se a imagem correspondente
não aparece nos metadados, registra a lacuna.

No Windows com `data.dat` original:

```powershell
dotnet run --project src/Joey.App -- .\passion\data.dat --inspect-visual-dat
python scripts/summarize_native_card_status_dat.py
```

Resultado privado em
`scratch/visual-review/card-status-manifests.json`.
Não commitar conteúdo proprietário, manifest ou imagens.

O disassembly preciso de `00419B70`, `0041A070`,
`0041A3B0`, `004194D0`, `0041A800` e o dispatcher é
outra coleta independente: `scripts/pending/04-card-status-ghidra.sh`.
Ela é necessária para resolver x/y (Ghidra tem registradores
`unaff_*`), retângulos-fonte, blend, clipping e ordem de composição,
antes de desenhar qualquer um desses novos indicadores.

**Resultado:** família identificada e parâmetros de timeline isolados.
Os Draws novos continuam **Pending**; inventário de cobertura
permanece `19/34 Partial, 0 Verified`. Os outros indicadores de
`duel/card/icon.txt` continuam fora desse inventário.
