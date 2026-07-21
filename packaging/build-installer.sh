#!/usr/bin/env bash
# Сборка установщика «Костёр»: publish (self-contained) → ISCC.
# Использование:  bash packaging/build-installer.sh
# ISCC можно переопределить:  ISCC="/c/Program Files (x86)/Inno Setup 6/ISCC.exe" bash packaging/build-installer.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

APP_CSPROJ="Kostyor.App/Kostyor.App.csproj"
PUBLISH_DIR="publish/app-inno"
ISS="packaging/inno/Kostyor.iss"
ISCC="${ISCC:-/c/Users/$USERNAME/AppData/Local/Programs/Inno Setup 6/ISCC.exe}"

echo "== 1/3 закрываю работающий exe (BUGS.md: MSB3027) =="
powershell -NoProfile -Command "Get-Process Kostyor* -ErrorAction SilentlyContinue | Stop-Process -Force" || true

echo "== 2/3 publish self-contained -> $PUBLISH_DIR =="
rm -rf "$PUBLISH_DIR"
dotnet publish "$APP_CSPROJ" -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=false -o "$PUBLISH_DIR" --nologo

if [ ! -f "$PUBLISH_DIR/Kostyor.exe" ]; then
  echo "ОШИБКА: не найден $PUBLISH_DIR/Kostyor.exe" >&2
  exit 1
fi

echo "== 3/3 ISCC -> installer/Output =="
if [ ! -f "$ISCC" ]; then
  echo "ОШИБКА: не найден ISCC.exe: $ISCC" >&2
  echo "Установи Inno Setup 6 или задай путь через переменную ISCC=." >&2
  exit 2
fi

# MSYS_NO_PATHCONV: git-bash не должен конвертить /D-флаги ISCC в пути.
MSYS_NO_PATHCONV=1 "$ISCC" "$ISS"

echo "Готово. Установщик в installer/Output/"
