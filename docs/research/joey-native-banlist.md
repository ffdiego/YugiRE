# Lista proibida / limitada do Joey — tabela candidata em EXE

Status: **endereço inferido de fonte externa**; nenhuma extração do `joey_pc.exe`
local nem comparação Ghidra realizada nesta investigação. NÃO classificar como
mapeamento nativo Verified antes do gate de fingerprint e cross-reference.

## Descoberta

O `docs/evidence/data.dat-index.json` da build do projeto contém os
recursos de interface `j\\deck_c\\banned1.txt`,
`j\\deck_c\\banned2.txt`, `j\\deck_c\\icon\\icon_seigen.bmp`,
os avisos `w_bannedcard*` e `j\\title\\op_limited.bmp`.
Esses dados **não** constituem a tabela de limites: no índice existente,
os recursos `bin#` são atributos, IDs, packs, textos, descrições, etc.

Fonte externa rastreável (PoC Mod Loader da variante No-CD/Fixed):
https://github.com/bitemydusto/YGO-PoC-Toolset/blob/main/src/PoCModLoader/HookManager.cpp

Em `HookManager::InstallHooks` (linhas 233–244), o mod lê:

- VA candidato `0x005EFBCC` como 38 entradas `uint32` em memória;
- `resourceId = word & 0xFFF`;
- `maxCopies = (word >> 16) & 0xFFFF` (esperado 0, 1, 2, 3);
- limite padrão de 3 para as demais identidades;
- converte `resourceId` ao índice interno com `FUN::GetCardIntID`;
- hook candidato em `0x005BE206`, retornando a `0x005BE234`.

O mesmo código (linhas 640–655) lê a flag `0x00A54DAE`.
**Segundo a implementação externa, quando a opção permite cartas
proibidas, uma carta com limite zero passa a valer como uma cópia.**
Isso é comportamento atribuído pelo mod; ainda exige confirmação no
nosso binário. Não confundir banlist com `card_pack.bin` (presença
no jogo) nem com `system.dat` (quantidade de cartas possuídas).

Outra pesquisa independente classifica a lista Forbidden/Limited como
hardcoded no EXE:
https://github-wiki-see.page/m/derplayer/YuGiOh-PoC-ModTools/wiki/PoC:-Information-corner

## Extração estática segura

A utilidade `scripts/extract_joey_banlist.py` mapeia a VA pelo cabeçalho
PE32, sem assumir que o offset em arquivo seja a VA menos image base,
verifica por padrão SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`,
lê exatamente 38 entradas e exporta somente IDs, limites e as palavras
de 32 bits. Não modifica EXE, DAT ou saves.

```powershell
python -m unittest discover -s tests -p test_native_banlist_extractor.py -v
python scripts/extract_joey_banlist.py --exe ..\\passion\\joey_pc.exe --output scratch\\joey-banlist.json
```

Se SHA ou RVA não baterem, abortar sem afirmar paridade.
`--allow-unverified` é somente diagnóstico de variantes.

## Validação pendente no Ghidra

1. Conferir se a imagem hashada contém 38 palavras válidas na
   VA `0x005EFBCC`; documentar bytes, IDs, limites e offsets com
   hash exato; manter bytes proprietários fora do Git.
2. Seguir xrefs à tabela e à função de consulta em `0x005BE206`;
   confirmar que consulta usa índice interno e que IDs especiais/variantes
   têm tratamento correto.
3. Verificar flag de opções `0x00A54DAE` no Joey original e se 0→1
   quando proibidas são permitidas.
4. Exercitar via tela de deck original ao menos um exemplar de cada
   categoria 0/1/2/3, confirmando efeito no deck principal/side/fusion.
5. Somente depois implementar `DeckRestrictionRules` em Core e
   validação do editor/importação. Guardar política como configuração,
   **não** alterar `Card.Properties` ou catálogo por adivinhação.

## Relação com P006 / P164

Restrições controlam **construção/legalidade do deck**, não a lista de
cartas já presentes nele que Sangan, Witch ou um recrutador podem
selecionar. Não usar `maxCopies` como filtro de elegibilidade durante
o duelo. Diferenciar igualmente disponibilidade em `card_pack.bin`
e número de cópias no save.
