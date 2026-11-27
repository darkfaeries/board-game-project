#!/bin/sh
set -eu

project_dir=$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)
output="$project_dir/bin/publish/win-x64"

dotnet publish "$project_dir/GameClient.csproj" -c Release -r win-x64 --self-contained true -o "$output" "$@"

printf '\nСборка Windows: %s\nФайл запуска: GameClient.exe\n' "$output"
