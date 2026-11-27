#!/bin/sh
set -eu

if [ "$(uname -s)" != "Darwin" ]; then
    printf '%s\n' 'Сборка приложения macOS запускается на Mac.' >&2
    exit 1
fi

project_dir=$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)
app="$project_dir/bin/Каменный век.app"
contents="$app/Contents"

mkdir -p "$contents/Resources"
dotnet publish "$project_dir/GameClient.csproj" -c Release -o "$contents/MacOS" "$@"
cp "$project_dir/assets/launcher.icns" "$contents/Resources/launcher.icns"
# Finder on newer macOS uses the Icon Composer catalog. Older versions use the ICNS.
cp "$project_dir/assets/launcher-macos.car" "$contents/Resources/Assets.car"

cat > "$contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleIdentifier</key>
    <string>org.darkfaeries.board-game-project</string>
    <key>CFBundleName</key>
    <string>Каменный век</string>
    <key>CFBundleDisplayName</key>
    <string>Каменный век</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleExecutable</key>
    <string>GameClient</string>
    <key>CFBundleIconFile</key>
    <string>launcher.icns</string>
    <key>CFBundleIconName</key>
    <string>Launcher</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST

printf '\nПриложение собрано: %s\n' "$app"
