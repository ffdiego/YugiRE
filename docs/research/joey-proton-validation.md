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
(cd "$game_dir" && exec "$wine" joey_pc.exe) >"$run/game.log" 2>&1 &
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

Use o PID confirmado nos [roteiros de regressão 41 a 43](../../scripts/regression/README.md#41--v17-formato-da-superfície-da-cena-em-wine).
O 41 lê três chamadas de cena. O 42 deve estar anexado antes do
clique na pilha do cemitério e desanexa após o primeiro blit estável.
Para o [43](../../scripts/regression/README.md#43--v31-quadrilátero-base-da-reação-em-proton),
anexe ainda no menu e deixe a demonstração começar sem cliques.
O roteiro espera a primeira reação que atinja o contador 8, registra
os argumentos do Draw base e desanexa no término do objeto.
Confira a mensagem de desanexação no log. Feche o jogo e saia do shell
para encerrar o servidor Wine desse prefixo e o Xvfb da sessão.

Registre a versão do Proton, o backend, o fingerprint, o cenário e os
resultados. Preserve os PNGs, logs, DATs e saves em `scratch/`.
O GDB pode alterar a cadência observada. Para cadência e diferenças de
backend, repita a [coleta 34 em Windows](../../scripts/pending/README.md#v01v04v11v12v16v17v31--34-attack-draw-runtimepy-aguardando-windows).
