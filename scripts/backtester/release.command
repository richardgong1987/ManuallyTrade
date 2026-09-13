#!/bin/bash
set -euo pipefail

# Derived from this script's own location (repo/scripts/backtester/), so double-clicking the
# file works on any machine regardless of the shell's working directory.
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SOLUTION="$REPO_ROOT/ManuallyTrade.sln"
ROBOT_SOURCE="$REPO_ROOT/ManuallyTrade/ManuallyTrade.cs"

cd "$REPO_ROOT"

git pull --all
git reset --hard origin/main

# Users installing the cBot must not be asked to grant full machine access. The cBot touches no
# files, so the source itself declares AccessRights.None; refuse to ship anything else.
if ! grep -q 'AccessRights = AccessRights\.None' "$ROBOT_SOURCE"; then
    echo "ERROR: $ROBOT_SOURCE does not declare 'AccessRights = AccessRights.None'." >&2
    echo "Refusing to build a release with unverified access rights." >&2
    exit 1
fi

dotnet build "$SOLUTION" -c Release
