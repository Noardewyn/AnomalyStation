#!/usr/bin/env bash
# Makes a release (docs/versioning.md): tools/release.sh patch|minor|major counts the next number from the current one by
# what the release brings (D330: patch for bug fixes and config changes only, minor for a new feature or a rework, major
# when the owner declares it); a full version is still taken, for a new phase: tools/release.sh 0.0.0-alpha.
# Writes the version and the wire protocol's version (src/Protocol/ProtocolVersion.cs) into Version.props, moves
# CHANGELOG.md's Unreleased section under the new version with today's date (with a line saying that the protocol
# changed, when it did), commits both files as "Release X.Y.Z" and tags the commit vX.Y.Z. Pushes nothing.
set -euo pipefail

cd "$(dirname "$0")/.."

new="${1:-}"
version_re='^[0-9]+\.[0-9]+\.[0-9]+(-(pre-alpha|alpha|beta))?$'
usage() {
    echo "usage: tools/release.sh patch|minor|major (D330), or tools/release.sh MAJOR.MINOR.PATCH[-pre-alpha|-alpha|-beta]" >&2
    exit 2
}
case "$new" in
    patch|minor|major) ;;
    *) [[ "$new" =~ $version_re ]] || usage ;;
esac

# A sortable key: the phase first (the 0.x versions before D280, pre-alpha, alpha, beta, the release; docs/versioning.md),
# then the numbers: each phase counts again from 0.0.0.
sort_key() {
    local rank=4
    case "$1" in
        *-pre-alpha) rank=1 ;;
        *-alpha) rank=2 ;;
        *-beta) rank=3 ;;
        0.*) rank=0 ;;
    esac
    echo "$rank.${1%%-*}"
}

current=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Version.props)
if [[ -z "$current" ]]; then
    echo "error: no <Version> in Version.props" >&2
    exit 1
fi

# patch, minor or major: the next number of the current phase (D330)
if [[ ! "$new" =~ $version_re ]]; then
    numbers=${current%%-*}
    phase=${current#"$numbers"}
    IFS=. read -r major minor patch <<<"$numbers"
    case "$new" in
        patch) patch=$((patch + 1)) ;;
        minor) minor=$((minor + 1)); patch=0 ;;
        major) major=$((major + 1)); minor=0; patch=0 ;;
    esac
    new="$major.$minor.$patch$phase"
fi
higher=$(printf '%s\n%s\n' "$(sort_key "$current")" "$(sort_key "$new")" | sort -t. -k1,1n -k2,2n -k3,3n -k4,4n | tail -1)
if [[ "$new" == "$current" || "$higher" != "$(sort_key "$new")" ]]; then
    echo "error: $new is not higher than the current version $current" >&2
    exit 1
fi
if git rev-parse -q --verify "refs/tags/v$new" >/dev/null; then
    echo "error: the tag v$new exists already" >&2
    exit 1
fi
if ! git diff --quiet -- Version.props CHANGELOG.md || ! git diff --cached --quiet; then
    echo "error: Version.props or CHANGELOG.md has uncommitted changes, or something is staged; commit it first" >&2
    exit 1
fi

protocol=$(sed -n 's/.*const ushort Current = \([0-9]*\);.*/\1/p' src/Protocol/ProtocolVersion.cs)
old_protocol=$(sed -n 's:.*<AnomalyProtocolVersion>\(.*\)</AnomalyProtocolVersion>.*:\1:p' Version.props)
if [[ -z "$protocol" || -z "$old_protocol" ]]; then
    echo "error: no protocol version in src/Protocol/ProtocolVersion.cs or no <AnomalyProtocolVersion> in Version.props" >&2
    exit 1
fi
protocol_line=""
if [[ "$protocol" != "$old_protocol" ]]; then
    protocol_line="- Network protocol $protocol: clients and servers older than $new cannot connect to this version."
fi

# The Unreleased section: the lines between its heading and the next version heading, blank lines aside.
unreleased=$(awk '{ sub(/\r$/, "") } /^## \[Unreleased\]/{on=1; next} /^## \[/{on=0} on && NF' CHANGELOG.md)
if [[ -z "$unreleased" && -z "$protocol_line" ]]; then
    echo "error: CHANGELOG.md's Unreleased section is empty; write what changed first" >&2
    exit 1
fi

today=$(date +%F)
awk -v heading="## [$new] - $today" -v protocol_line="$protocol_line" '
    /^## \[Unreleased\]/ {
        print; print ""; print heading
        if (protocol_line != "") { print ""; print "### Protocol"; print ""; print protocol_line }
        next
    }
    { print }
' CHANGELOG.md > CHANGELOG.md.tmp
mv CHANGELOG.md.tmp CHANGELOG.md
# perl, not sed -i: macOS's BSD sed takes -i's next argument as a backup suffix
perl -pi -e "s:<Version>\Q$current\E</Version>:<Version>$new</Version>:" Version.props
perl -pi -e "s:<AnomalyProtocolVersion>$old_protocol</AnomalyProtocolVersion>:<AnomalyProtocolVersion>$protocol</AnomalyProtocolVersion>:" Version.props

git add Version.props CHANGELOG.md
git commit -q -m "Release $new"
git tag -a "v$new" -m "Anomaly Station $new"
echo "released $new (was $current, protocol $protocol): commit $(git rev-parse --short=8 HEAD), tag v$new; push with: git push --follow-tags"
