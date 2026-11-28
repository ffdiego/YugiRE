# Validação do Joey original no Proton

O piloto usou Fedora, Proton 9.0, Xvfb a 800×600, llvmpipe e GDB.
O mesmo executável abriu o menu, um duelo individual e a lista do
cemitério adversário. A demonstração automática também produziu uma
reação do Joey que pôde ser inspecionada do primeiro Draw ao término.
O setup abaixo usa uma cópia privada dos arquivos locais do jogo e um
prefixo descartável.

## Preparar o display e o prefixo

São necessários Proton com Wine de 32 bits, Xvfb, xauth, Python 3 com
Pillow e suporte XCB, e GDB para inspecionar o processo. Execute da raiz
do repositório em Bash. Ajuste os caminhos iniciais e escolha um número
de display livre. O piloto usou o Xvfb extraído de um RPM do Fedora em
`scratch/`, o que também permite passar seu caminho em `YUGIRE_XVFB`.

```bash
set -e
joey_source="$(realpath ../passion)"
proton="$HOME/.local/share/Steam/steamapps/common/Proton 9.0 (Beta)/files"
xvfb="${YUGIRE_XVFB:-Xvfb}"
display_number=91
test ! -e "/tmp/.X${display_number}-lock"
test ! -S "/tmp/.X11-unix/X${display_number}"

printf '%s  %s\n' \
  db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de \
  "$joey_source/joey_pc.exe" | sha256sum --check

mkdir -p scratch
run="$(mktemp -d "$PWD/scratch/proton-joey.XXXXXX")"
export DISPLAY=":$display_number" XAUTHORITY="$run/xauthority"
export LIBGL_ALWAYS_SOFTWARE=1
export WINEPREFIX="$run/prefix" WINEARCH=win64
export WINESERVER="$proton/bin/wineserver"
export WINEDLLPATH="$proton/lib64/wine:$proton/lib/wine"
export WINEDLLOVERRIDES='winemenubuilder.exe=d;mscoree=d;mshtml=d'
export PATH="$proton/bin:$PATH"

touch "$XAUTHORITY"
chmod 600 "$XAUTHORITY"
xauth -f "$XAUTHORITY" add "$DISPLAY" MIT-MAGIC-COOKIE-1 \
  "$(python3 -c 'import secrets; print(secrets.token_hex(16))')"
"$xvfb" "$DISPLAY" -screen 0 800x600x24 -nolisten tcp \
  -auth "$XAUTHORITY" >"$run/xvfb.log" 2>&1 &
xvfb_pid=$!
trap '"$WINESERVER" -k 2>/dev/null || true; kill "$xvfb_pid" 2>/dev/null || true' EXIT

for attempt in {1..50}; do
  test -S "/tmp/.X11-unix/X${display_number}" && break
  sleep 0.1
done
test -S "/tmp/.X11-unix/X${display_number}"

wine="$proton/bin/wine"
"$wine" wineboot.exe -u >"$run/wineboot.log" 2>&1
unlink "$WINEPREFIX/dosdevices/z:" 2>/dev/null || true
find -P "$WINEPREFIX/drive_c/users" -mindepth 2 -maxdepth 2 -type l -delete

# The standalone Proton loader needs its bundled vkd3d DLLs in the prefix.
for dll in libvkd3d-1.dll libvkd3d-shader-1.dll; do
  cp -- "$proton/lib/vkd3d/$dll" "$WINEPREFIX/drive_c/windows/syswow64/"
  cp -- "$proton/lib64/vkd3d/$dll" "$WINEPREFIX/drive_c/windows/system32/"
done

game_dir="$WINEPREFIX/drive_c/joey"
mkdir -p "$game_dir"
cp --reflink=auto -- "$joey_source"/{joey_pc.exe,data.dat,Region.dat,Voice.dat} "$game_dir/"
"$wine" reg.exe add 'HKCU\Software\KONAMI\Yu-Gi-Oh! Power Of Chaos' \
  /v InstallDirJ /t REG_SZ /d 'C:\joey' /f >>"$run/wineboot.log" 2>&1
joey_args=()  # Use joey_args=(-speedy) ou joey_args=(-nosound) na sessão correspondente.
(cd "$game_dir" && exec "$wine" joey_pc.exe "${joey_args[@]}") >"$run/game.log" 2>&1 &
game_pid=$!
printf 'Sessão: %s\nLauncher PID: %s\n' "$run" "$game_pid"
```

Mantenha essa sessão de shell aberta. O prefixo registra e grava seus
próprios arquivos de configuração e baralho. Caso a janela fique preta,
consulte `game.log`. O primeiro piloto falhou ao carregar
`wined3d.dll` porque faltavam as DLLs vkd3d da arquitetura correta.

## Interagir e capturar

Espere a introdução chegar ao menu. No display de teste, entre em
**Duel Mode**, depois **Single Duel**, escolha uma mão no
pedra-papel-tesoura e prossiga até o tabuleiro. No cenário registrado,
Mystical Space Typhoon destruiu uma armadilha adversária baixada.
O clique seguinte na pilha do cemitério adversário abriu a lista.

Em Xvfb, use entrada XTest no display e na autoridade exportados acima.
Por exemplo, com `xdotool` disponível, `xdotool mousemove 400 344 click 1`
aciona o primeiro botão no menu observado. Confira a tela antes de
repetir o clique. A introdução, as transições e a demonstração automática
podem absorver comandos precoces. O piloto enviou os cliques via
`libXtst` em Python.

Capture com o display já exportado. Defina um nome diferente para cada
quadro e mantenha o mouse fora da região que pretende comparar.

```bash
python3 - "$run/original-graveyard.png" <<'PY'
import sys
from PIL import ImageGrab

canvas = ImageGrab.grab().convert("RGB")
assert canvas.size == (800, 600), canvas.size
canvas.save(sys.argv[1])
PY
```

## Confirmar o processo e executar os probes

O PID do launcher pode diferir do processo que carrega o PE. Liste apenas
os processos com o nome do jogo e confira o diretório da cópia de teste
e o mapeamento antes de anexar.

```bash
joey_test_pid=
for pid in $(pgrep -x joey_pc.exe); do
  if [ "$(readlink "/proc/$pid/cwd")" = "$game_dir" ] &&
      grep '^00400000-.*joey_pc\.exe' "/proc/$pid/maps"; then
    joey_test_pid="$pid"
    printf 'PID de teste: %s\n' "$joey_test_pid"
    break
  fi
done
test -n "$joey_test_pid"
```

Use o PID confirmado nos [roteiros de regressão com GDB, de 41 a 47](../../scripts/regression/README.md#41--v17-formato-da-superfície-da-cena-em-wine).
O 41 lê três chamadas de cena. O 42 deve estar anexado antes do
clique na pilha do cemitério e desanexa após o primeiro blit estável.
Para o [43](../../scripts/regression/README.md#43--v31-quadrilátero-base-da-reação-em-proton),
anexe ainda no menu e deixe a demonstração começar sem cliques.
O roteiro espera a primeira reação que atinja o contador 8, registra
os argumentos do Draw base e desanexa no término do objeto.
Em uma nova execução da demonstração, o
[44](../../scripts/regression/README.md#44--v31-camadas-da-reação-em-proton)
registra a escolha e a posição das camadas de boca e olhos.
O [45](../../scripts/regression/README.md#45--v31-reação-com-duas-falas-em-proton)
aguarda uma reação com duas falas e registra a troca do ID ativo.
O [46](../../scripts/regression/README.md#46--v17-despacho-da-cena-em-proton)
registra 360 entradas da cena no preset escolhido, com ou sem despacho.
O [47](../../scripts/regression/README.md#47--v17-entrega-do-buffer-em-proton)
acompanha a chamada ao DirectDraw com o tabuleiro já em cena.
O [48](../../scripts/regression/README.md#48--v17-notificações-de-desenho-em-xvfb)
observa a saída no servidor X por dez segundos sem anexar ao processo.
Os [49 e 50](../../scripts/regression/README.md#49-e-50--v05-atributos-no-campo-em-proton)
registram os argumentos dos números de ATK/DEF e a cor-chave
do atlas no executável.
O [51](../../scripts/regression/README.md#51--v31-reação-com-áudio-desativado-em-proton)
verifica o término da reação com `-nosound`.
O [52](../../scripts/regression/README.md#52--v05-composição-dos-atributos-em-proton)
registra os pesos das superfícies no compositor dos números.
O [53](../../scripts/regression/README.md#53--v05-transição-dos-atributos-em-proton)
acompanha a mudança de um atributo até o número atingir o novo valor.
O [54](../../scripts/regression/README.md#54--v04-despacho-da-espada-em-proton)
registra a troca de variante da espada e o compositor usado a cada passo.
O [59](../../scripts/regression/README.md#59--v04v17-recorte-do-impacto-em-proton)
mede as faixas de escrita quando o impacto ultrapassa a borda do buffer.
O [60](../../scripts/regression/README.md#60--v17-pixels-do-compositor-10-para-3-em-proton)
compara o resultado de pixels amostrados no compositor com o modelo .NET.
O [61](../../scripts/regression/README.md#61--v17-pixels-do-compositor-base-10-para-3)
repete a comparação no compositor base.
O [62](../../scripts/regression/README.md#62--v17-seleção-de-texels-no-impacto-em-proton)
compara as coordenadas dos texels do impacto com o modelo .NET.
O [63](../../scripts/regression/README.md#63--v17-seleção-de-texels-em-quadrilátero-inclinado)
faz a comparação no quadrilátero inclinado desenhado durante o voo.
O [64](../../scripts/regression/README.md#64--v16-avanço-da-cena-de-resultado)
acompanha a transição de resultado até a saída da cena.
O [67](../../scripts/regression/README.md#67--v31-gatilho-e-duração-do-busto-de-abertura)
mede o busto de abertura após escolher Single Duel. O
[68](../../scripts/regression/README.md#68--v31-duração-do-busto-de-resultado)
mede o busto de resultado após o banner e a decisão de salvar o replay.
Confira a mensagem de desanexação no log. Feche o jogo e saia do shell
para encerrar o servidor Wine desse prefixo e o Xvfb da sessão.

Registre a versão do Proton, o backend, o fingerprint, o cenário e os
resultados. Preserve os PNGs, logs, DATs e saves em `scratch/`.
O GDB pode alterar a cadência observada. Para cadência e diferenças de
backend, repita a [coleta 34 em Windows](../../scripts/pending/README.md#v01v04v11v12v16v17v31--34-attack-draw-runtimepy-aguardando-windows).
