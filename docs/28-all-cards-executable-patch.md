# Patch experimental de coleção completa

O script `scripts/patch_all_cards.py` gera um executável separado que garante
ao menos três cópias das cartas do catálogo compartilhado, sem modificar o
original. Aceita exclusivamente o SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Não distribui binários nem grava diretamente no save ou no registro.
**Executar a cópia pode persistir as cartas pelo salvamento normal do jogo.**
Faça uma cópia da pasta Common antes do teste. Retornar ao EXE original
remove o patch de código, mas não desfaz alterações já persistidas no save.

## Uso

Da raiz do repositório, no Windows com Python:

```powershell
py scripts\patch_all_cards.py ..\passion\joey_pc.exe ..\passion\joey_pc_allcards.exe
```

No Fedora:

```sh
python scripts/patch_all_cards.py ../passion/joey_pc.exe ../passion/joey_pc_allcards.exe
```

O destino precisa ser novo: o script recusa sobrescrever qualquer arquivo.
Coloque a cópia na mesma pasta dos arquivos do jogo e execute-a normalmente.
Uma cópia já foi gerada no workspace em `passion/joey_pc_allcards.exe`.

## Mecanismo e evidência

Endereços VA, image base `0x00400000`. `0x005BEA80` reconcilia o deck com a
coleção. Callers comprovados `0x0044A550` e `0x0044AA00` executam esse fluxo
ao carregar dados de deck; o startup chama `0x0044AA00`. A leitura direta
da coleção pela Card List impede resolver tudo apenas com um getter falso.

O patch substitui cinco bytes inteiros do prólogo de `0x005BEA80` por um
salto para uma nova seção PE `.cards`, VA `0x00A5E000`. O stub de 79 bytes:

1. Preserva os registradores e flags.
2. Percorre índices 1–1114 e consulta `card_pack` por `0x004024E0`.
3. Quando `card_pack & 7 != 0`, eleva o byte de quantidade para três apenas
   se estiver abaixo disso. Não altera o byte superior de flags.
4. Reconta as cópias em `0x00A53CCA` e marca `0x00A54F42` como alterado.
5. Restaura os registradores/flags, reproduz o prólogo original e retorna
   a `0x005BEA85`, continuando a validação normal do deck.

A máscara representa o catálogo de 771 cartas já identificado. Não altera
as listas de cartas limitadas/proibidas, a máscara de tela corrente (Joey
continua filtrando 350) nem torna jogáveis registros com `card_pack == 0`.
Trata-se de uma modificação deliberada de comportamento, não de uma regra
original descoberta por engenharia reversa.

## Validação e limites

`python -m unittest discover -s tests`: 35 testes passaram. Os quatro novos
casos conferem recusa de outro binário, saltos, correspondência do stub com
o fonte assembly e estrutura PE/preservação das seções do original.
O disassembly confirma as fronteiras de instrução e o salto de retorno.
O alvo tem relocations stripped e DllCharacteristics zero; o código
injetado depende desse endereço de carga fixo e desse fingerprint.

Registro de hashes e offsets em `evidence/all-cards-patch.json`;
funções consultadas em `evidence/all-cards-patch-candidates.txt`.
O hash do original foi conferido novamente após a geração. Não executamos
o EXE original nem o modificado no Fedora. O patch permanece experimental
até o teste no Windows: conferir Card List em All, adicionar cartas antes
ausentes no Deck Construction, jogar e reabrir para conferir persistência.
