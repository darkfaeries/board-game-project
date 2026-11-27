#!/bin/sh
set -eu

project_dir=$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)
output="$project_dir/bin/publish/linux-x64"

dotnet publish "$project_dir/GameClient.csproj" -c Release -r linux-x64 --self-contained true -o "$output" "$@"

printf '\nСборка Linux: %s\nФайл запуска: GameClient\n' "$output"
