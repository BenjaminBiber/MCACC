#!/usr/bin/env bash
# Builds MCACC.app for macOS, signs it ad-hoc, zips it and packs a DMG.
#
#   ./scripts/build-macos.sh --arch universal --version 0.1.0
#
# The app is not notarized, so the first start needs a right-click "Open" or
#   xattr -dr com.apple.quarantine MCACC.app
set -euo pipefail

ARCH="arm64"
VERSION="0.1.0"
BUNDLE_ID="de.benjaminbiber.mcacc"

while [ $# -gt 0 ]; do
  case "$1" in
    --arch) ARCH="$2"; shift 2 ;;
    --version) VERSION="$2"; shift 2 ;;
    --bundle-id) BUNDLE_ID="$2"; shift 2 ;;
    -h|--help) sed -n '2,8p' "$0"; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

case "$ARCH" in
  arm64|x64|universal) ;;
  *) echo "--arch must be arm64, x64 or universal." >&2; exit 2 ;;
esac

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ARTIFACTS="$REPO_ROOT/artifacts"
APP="$ARTIFACTS/MCACC.app"
PROJECT="$REPO_ROOT/src/Mcacc.App/Mcacc.App.csproj"

say() { printf '==> %s\n' "$1"; }

publish() {
  local rid="$1" out="$2" r2r="$3"
  say "Publish $rid"
  rm -rf "$out"
  dotnet publish "$PROJECT" \
    -c Release \
    -r "$rid" \
    --self-contained true \
    -p:PublishReadyToRun="$r2r" \
    -p:DebugType=None \
    -p:Version="$VERSION" \
    -o "$out" \
    --nologo
}

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

if [ "$ARCH" = "universal" ]; then
  # ReadyToRun is per architecture, so a universal build stays IL-only.
  publish osx-arm64 "$ARTIFACTS/publish/osx-arm64" false
  publish osx-x64 "$ARTIFACTS/publish/osx-x64" false
  cp -R "$ARTIFACTS/publish/osx-arm64/." "$APP/Contents/MacOS/"
  say "lipo"
  (cd "$ARTIFACTS/publish/osx-arm64" && find . -type f) | while read -r relative; do
    arm="$ARTIFACTS/publish/osx-arm64/$relative"
    intel="$ARTIFACTS/publish/osx-x64/$relative"
    [ -f "$intel" ] || continue
    file "$arm" | grep -q 'Mach-O' || continue
    # Some NuGet natives (HarfBuzzSharp, SkiaSharp) already ship as universal
    # binaries; lipo refuses to merge those again, so keep them as they are.
    arm_archs=" $(lipo -archs "$arm") "
    intel_archs=" $(lipo -archs "$intel") "
    if [[ "$arm_archs" == *" x86_64 "* && "$arm_archs" == *" arm64 "* ]]; then
      continue
    elif [[ "$intel_archs" == *" x86_64 "* && "$intel_archs" == *" arm64 "* ]]; then
      cp "$intel" "$APP/Contents/MacOS/$relative"
    else
      lipo -create "$arm" "$intel" -output "$APP/Contents/MacOS/$relative"
    fi
  done
else
  publish "osx-$ARCH" "$ARTIFACTS/publish/osx-$ARCH" true
  cp -R "$ARTIFACTS/publish/osx-$ARCH/." "$APP/Contents/MacOS/"
fi

chmod +x "$APP/Contents/MacOS/MCACC"

say "Icon"
ICONSET="$ARTIFACTS/MCACC.iconset"
rm -rf "$ICONSET"
mkdir -p "$ICONSET"
MASTER="$REPO_ROOT/assets/icon/mcacc-1024.png"
for size in 16 32 64 128 256 512; do
  sips -z "$size" "$size" "$MASTER" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
  double=$((size * 2))
  sips -z "$double" "$double" "$MASTER" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/MCACC.icns"
rm -rf "$ICONSET"

say "Info.plist"
sed -e "s|@VERSION@|$VERSION|g" -e "s|@BUNDLE_ID@|$BUNDLE_ID|g" \
  "$REPO_ROOT/build/macos/Info.plist.template" > "$APP/Contents/Info.plist"

say "Sign"
xattr -cr "$APP"
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict --verbose=2 "$APP"

say "Zip"
ZIP="$ARTIFACTS/MCACC-$VERSION-macos-$ARCH.zip"
rm -f "$ZIP"
ditto -c -k --sequesterRsrc --keepParent "$APP" "$ZIP"

say "DMG"
DMG="$ARTIFACTS/MCACC-$VERSION-macos-$ARCH.dmg"
STAGE="$ARTIFACTS/dmg-stage"
rm -rf "$STAGE" "$DMG"
mkdir -p "$STAGE"
ditto "$APP" "$STAGE/MCACC.app"
ln -s /Applications "$STAGE/Applications"
hdiutil create -volname "MCACC $VERSION" -srcfolder "$STAGE" -fs HFS+ -format UDZO -ov "$DMG" >/dev/null
rm -rf "$STAGE"

printf '\n%s\n%s\n%s\n' "$APP" "$ZIP" "$DMG"
du -sh "$APP" "$ZIP" "$DMG"
