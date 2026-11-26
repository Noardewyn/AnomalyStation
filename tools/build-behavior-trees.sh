#!/bin/sh
# Compiles tg's *.bt.json behaviour trees with tg's own tools/build_bt.py (it resolves the DM defines in them) and copies the output to
# src/Core/Tg/BehaviorTrees/, embedded in Anomaly.Core.Tg (BehaviorTreeFiles), so the server needs no tg clone at run time (DV251).
# Rerun only when tg's snapshot changes. tg's clone: $ANOMALY_TGSTATION, else ../tgstation beside the repository or beside a worktree's main checkout.
set -eu
root=$(git -C "$(dirname "$0")" rev-parse --show-toplevel)
main=$(dirname "$(git -C "$root" rev-parse --path-format=absolute --git-common-dir)")
tg=${ANOMALY_TGSTATION:-}
[ -n "$tg" ] || { [ -d "$root/../tgstation" ] && tg=$root/../tgstation; } || tg=$main/../tgstation
scratch=$(mktemp -d)
trap 'rm -rf "$scratch"' EXIT
ln -s "$(cd "$tg" && pwd)/code" "$scratch/code"
python3 "$tg/tools/build_bt.py" --repo-root "$scratch"
out=$root/src/Core/Tg/BehaviorTrees
rm -rf "$out"
mkdir -p "$out"
cp -R "$scratch/build/behavior_trees/code" "$out/code"
find "$out" -name '*.json' | wc -l
