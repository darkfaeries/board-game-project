#!/bin/sh
set -eu

count=${1:-4}
if [ "$#" -gt 1 ] || { [ "$count" != 3 ] && [ "$count" != 4 ]; }; then
    printf '%s\n' 'Использование: sh GameClient/run-local-windows.sh [3|4]' >&2
    exit 1
fi

project_dir=$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)
system=$(uname -s)
case "$system" in
    Darwin)
        sh "$project_dir/package-macos.sh"
        app="$project_dir/bin/Каменный век.app"
        ;;
    Linux)
        sh "$project_dir/package-linux.sh"
        app="$project_dir/bin/publish/linux-x64/GameClient"
        ;;
    MINGW*|MSYS*|CYGWIN*)
        sh "$project_dir/package-windows.sh"
        app="$project_dir/bin/publish/win-x64/GameClient.exe"
        ;;
    *)
        printf 'Система %s не поддерживается этим скриптом.\n' "$system" >&2
        exit 1
        ;;
esac

if [ "$system" != "Darwin" ]; then
    logs=$(mktemp -d "$project_dir/bin/local-windows.XXXXXX")
    printf 'Журналы окон: %s\n' "$logs"
fi

window=1
while [ "$window" -le "$count" ]; do
    if [ "$system" = "Darwin" ]; then
        open -n "$app" --args --windowed "--player-window=$window"
    else
        nohup "$app" --windowed "--player-window=$window" > "$logs/player-$window.log" 2>&1 < /dev/null &
    fi
    window=$((window + 1))
done
