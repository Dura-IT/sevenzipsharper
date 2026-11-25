#!/usr/bin/env bash
# release.sh — Cut a new SevenZipSharper release.
#
# Usage:
#   ./scripts/release.sh 0.1.1
#
# What it does:
#   1. Validates the version (semver) and that the v<version> tag doesn't already exist.
#   2. Confirms the matching natives-v<7zip-version>-norar GitHub release exists (publish.yml needs it).
#   3. Bumps <Version> in both SevenZipSharper and SevenZipSharper.Native csprojs.
#   4. Commits the bump.
#   5. Tags v<version> at HEAD.
#   6. Pushes commit + tag → publish.yml runs → NuGet.org.
#
# Requirements: bash, gh CLI authenticated to the repo, clean working tree on main.

set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 <version>     e.g. $0 0.1.1" >&2
  exit 1
fi

VERSION="$1"
TAG="v$VERSION"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

# ---------------------------------------------------------------------------
# Validation
# ---------------------------------------------------------------------------
if ! [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[A-Za-z0-9.-]+)?$ ]]; then
  echo "Error: version '$VERSION' is not valid semver (e.g. 0.1.1 or 1.0.0-beta.1)" >&2
  exit 1
fi

BRANCH="$(git rev-parse --abbrev-ref HEAD)"
if [[ "$BRANCH" != "main" ]]; then
  echo "Error: must be on 'main' branch (currently on '$BRANCH')" >&2
  exit 1
fi

if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "Error: working tree is dirty. Commit or stash first." >&2
  git status --short >&2
  exit 1
fi

if git rev-parse "$TAG" >/dev/null 2>&1; then
  echo "Error: tag '$TAG' already exists locally. Delete it first: git tag -d $TAG" >&2
  exit 1
fi

if git ls-remote --tags origin "refs/tags/$TAG" | grep -q "$TAG"; then
  echo "Error: tag '$TAG' already exists on origin. Delete it first: git push origin :refs/tags/$TAG" >&2
  exit 1
fi

SEVENZIP_VERSION="$(cat scripts/7zip-version)"
NATIVES_TAG="natives-v$SEVENZIP_VERSION-norar"

if ! command -v gh >/dev/null; then
  echo "Error: gh CLI is required" >&2
  exit 1
fi

if ! gh release view "$NATIVES_TAG" >/dev/null 2>&1; then
  echo "Error: GitHub release '$NATIVES_TAG' does not exist." >&2
  echo "Run: gh workflow run build-natives.yml --ref main" >&2
  echo "Then wait for it to complete and re-run this script." >&2
  exit 1
fi

# ---------------------------------------------------------------------------
# Bump csproj versions
# ---------------------------------------------------------------------------
MANAGED_CSPROJ="src/SevenZipSharper/SevenZipSharper.csproj"
NATIVE_CSPROJ="src/SevenZipSharper.Native/SevenZipSharper.Native.csproj"

CURRENT_VERSION="$(grep -oE '<Version>[^<]+</Version>' "$MANAGED_CSPROJ" | head -1 | sed -E 's|</?Version>||g')"

if [[ "$CURRENT_VERSION" == "$VERSION" ]]; then
  echo "Note: csproj already at $VERSION — skipping bump, will tag at HEAD."
else
  for csproj in "$MANAGED_CSPROJ" "$NATIVE_CSPROJ"; do
    # Replace the first <Version>…</Version> occurrence (the PropertyGroup one, not PackageReferences).
    # Use a sed pattern anchored on the indentation to avoid touching PackageReference Version="…".
    sed -i.bak -E "s|<Version>[^<]+</Version>|<Version>$VERSION</Version>|" "$csproj"
    rm "$csproj.bak"
  done

  git add "$MANAGED_CSPROJ" "$NATIVE_CSPROJ"
  git commit -m "Release v$VERSION"
fi

# ---------------------------------------------------------------------------
# Tag and push
# ---------------------------------------------------------------------------
git tag -a "$TAG" -m "Release $TAG"
git push origin main
git push origin "$TAG"

echo ""
echo "Released $TAG."
echo "publish.yml: https://github.com/Dura-IT/SevenZipSharper/actions/workflows/publish.yml"
echo "Natives source: $NATIVES_TAG (7-Zip $SEVENZIP_VERSION)"
