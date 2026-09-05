#!/usr/bin/env bash
# Mirror the TM gamemode reference repos locally, and record what was pinned.
# Re-run to update; MANIFEST.md is regenerated with each repo's current HEAD.
set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

REPOS=(
  maniaplanet/game-modes            # Nadeo official modes AND Libs/Nadeo sources (ManiaPlanet-era)
  maniaplanet/game-modes-mp3        # older Nadeo generation
  AmazingBeu/TM2020-Gamemodes       # richest TM2020 community collection (27 modes)
  reaby/TMModeTemplate              # mode dev scaffold + SetModeScriptText hot-reload tooling
  The-Jonsey/tm-gamemodes
  XertroV/tm-archivist              # uses C_TimeGapMode_Hidden
  Ouhouuhu/Revive-KO                # knockout - reference for KOTD
  reaby/Revive-KO
  yannpradel/game-modes
  zai-tm/maniascripts
  Ze-Rax/TrackmaniaFlagRush
  Plambt/Puzzle2020
  ArkadySK/TMNextPlatform
  Plambt/TM-SMScripts
  Rxelux/TrackmaniaChaseAlliance
  nightwolf93/TMKartMode
  dassschaf/tm2020scripts
  escapemania/mmomania
  guerro323/maniascript
  Selene0623/ManiaScript
  ObstacleSM/documentation
  xAt0mZ/trackmania-speedrun
  MLEPP/shootmania
  domino54/title-packs
  Geekid812/TrackmaniaBingo
  BigBang1112/maniascript-sharp     # ManiaScript tooling - may carry API definitions
  clankercode/lsp-openplanet        # ManiaScript LSP - may carry API definitions
)

for r in "${REPOS[@]}"; do
  d="${r##*/}"
  if [ -d "$d/.git" ]; then
    echo "== updating $r"; git -C "$d" fetch --quiet --all && git -C "$d" pull --quiet --ff-only 2>/dev/null || echo "   (pull skipped)"
  else
    echo "== cloning $r"; git clone --quiet "https://github.com/$r.git" "$d" || echo "   FAILED $r"
  fi
done

# Asset-heavy repos (tens/hundreds of MB of media for a handful of scripts).
# Blobless + sparse: fetch only ManiaScript files, not the media.
SPARSE_REPOS=(
  SM-Obstacle/Titlepack
  domino54/TMAll
  AreaFiftyLAN/lancie-tooling
  ObstacleSM/Titlepack
  escapemania/escapemania
  tmservers/tm-server-manager
)

for r in "${SPARSE_REPOS[@]}"; do
  d="${r##*/}"
  if [ -d "$d/.git" ]; then
    echo "== updating (sparse) $r"; git -C "$d" fetch --quiet --all 2>/dev/null || echo "   (fetch skipped)"
  else
    echo "== cloning (sparse) $r"
    if git clone --quiet --filter=blob:none --sparse "https://github.com/$r.git" "$d"; then
      git -C "$d" sparse-checkout set --no-cone '*.Script.txt' '*.script.txt' '*.md' 2>/dev/null \
        || echo "   sparse-checkout failed for $r"
    else
      echo "   FAILED $r"
    fi
  fi
done

ALL_REPOS=("${REPOS[@]}" "${SPARSE_REPOS[@]}")

{
  echo "# TM gamemode reference mirrors"
  echo
  echo "Local copies of the repos used to verify ManiaScript APIs, because TM2020's"
  echo "\`Libs/Nadeo/*\` are published nowhere and several of these are dormant."
  echo "Regenerate with \`./refresh.sh\`. Nothing here is ours - do not edit, just read."
  echo
  echo "| repo | pinned commit | last upstream commit |"
  echo "|---|---|---|"
  for r in "${ALL_REPOS[@]}"; do
    d="${r##*/}"
    if [ -d "$d/.git" ]; then
      printf "| [%s](https://github.com/%s) | \`%s\` | %s |\n" "$r" "$r" \
        "$(git -C "$d" rev-parse --short HEAD)" "$(git -C "$d" log -1 --format=%ad --date=short)"
    fi
  done
} > MANIFEST.md
echo; echo "wrote MANIFEST.md"
