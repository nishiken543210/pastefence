#!/usr/bin/env bash
# SVG から src/PasteFence/pastefence.ico を作り直す。
# 必要なもの: node（playwright と Chromium が入っていること）と python3。
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

node "$here/render.mjs" "$work"
python3 "$here/pack-ico.py" "$work" "$repo/src/PasteFence/pastefence.ico"
