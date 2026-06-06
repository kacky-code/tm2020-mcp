#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
docs_dir="$repo_root/docs/openplanet"
raw_dir="$docs_dir/raw"

mkdir -p "$raw_dir"

while IFS= read -r url; do
  [[ -z "$url" || "$url" == \#* ]] && continue
  name="${url#https://}"
  name="${name//\//.}"
  name="${name//[^A-Za-z0-9._-]/_}"
  echo "Fetching $url"
  curl -fsSL "$url" -o "$raw_dir/$name.html"
done < "$docs_dir/sources.txt"

echo "Raw snapshots written to $raw_dir"
