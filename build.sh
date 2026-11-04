#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
dotnet_cmd="${DOTNET_CMD:-dotnet}"
if ! command -v "$dotnet_cmd" >/dev/null 2>&1 ||
  ! "$dotnet_cmd" --list-sdks 2>/dev/null | awk '$1 ~ /^8\./ { found = 1 } END { exit !found }'; then
  install_dir="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"
  installer="$(mktemp)"
  trap 'rm -f "$installer"' EXIT
  if command -v curl >/dev/null 2>&1; then
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$installer"
  elif command -v wget >/dev/null 2>&1; then
    wget -q https://dot.net/v1/dotnet-install.sh -O "$installer"
  else
    echo "Install curl or wget to download the .NET 8 SDK installer." >&2
    exit 1
  fi
  bash "$installer" --channel 8.0 --install-dir "$install_dir"
  dotnet_cmd="$install_dir/dotnet"
fi

output_dir="$root_dir/ReleaseBundles/windows-x64"
mkdir -p "$output_dir"

"$dotnet_cmd" publish "$root_dir/SantexnikaSRM.csproj" \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  -p:EnableWindowsTargeting=true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$output_dir"

echo "Windows x64 EXE created: $output_dir/SantexnikaSRM.exe"
