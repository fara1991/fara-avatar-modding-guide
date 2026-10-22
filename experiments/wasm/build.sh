#!/usr/bin/env bash
# 試作（WebAssembly + MudBlazor）を作る。本体の Content/ から JSON を書き出し、WebAssembly を publish する。
#   1. Content/ → FaraAvatarWasm/wwwroot/data/（content.json・search.json）
#   2. FaraAvatarWasm → publish-out/wwwroot（配信するのはこの中身。/wasm/ の下に置く想定）
# 現行の SSR 版は、本体で `dotnet run -- --export dist` を実行して作る（比較用）。
# 計測は measure/ を参照（serve.mjs・measure.mjs・screenshots.mjs）。
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
dotnet run --project "$here/ContentExport/ContentExport.csproj" -- "$repo" "$here/FaraAvatarWasm/wwwroot/data"
dotnet publish "$here/FaraAvatarWasm/FaraAvatarWasm.csproj" -c Release -o "$here/publish-out"
