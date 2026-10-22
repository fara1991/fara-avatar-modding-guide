#!/usr/bin/env bash
# 検索の島（islands/search）を publish し、本体サイトの wwwroot/_framework と wwwroot/_content に入れる（試作）。
# 使い方（リポジトリのルートで）: bash islands/search/copy-to-site.sh
# 本体の dotnet build / dotnet run -- --export dist より前に実行する（静的ファイルとして dist に入るように）。
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
island="$root/islands/search"
out="$island/publish"

# 前回の出力（別の設定の残り）を混ぜないように、publish の出力先を消してから publish する
rm -rf "$out"
export DOTNET_ROLL_FORWARD="${DOTNET_ROLL_FORWARD:-Major}"

# EXTRA_PUBLISH_ARGS（計測の variant 用。例: -p:InvariantGlobalization=false -p:PublishTrimmed=false）
dotnet publish "$island/FaraSearchIsland.csproj" -c Release -o "$out" --nologo ${EXTRA_PUBLISH_ARGS:-}

# _framework（Blazor の本体）と _content（パッケージの JS。CustomElements の lib.module.js など）を両方入れる。
# 片方でも欠けると、起動時に blazor.boot.json を読めずに失敗する。
for dir in _framework _content; do
  target="$root/wwwroot/$dir"
  rm -rf "$target"
  mkdir -p "$target"
  # 圧縮版（.br / .gz）と index.html は入れない（Workers の静的アセットは生のファイルを配るため）
  cp -r "$out/wwwroot/$dir/." "$target/"
  find "$target" -name '*.br' -delete
  find "$target" -name '*.gz' -delete
  echo "copied $(find "$target" -type f | wc -l) files to wwwroot/$dir"
done
