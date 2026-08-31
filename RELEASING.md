# Releasing

Checklist for cutting a SevenZipSharper release. `scripts/release.sh` only handles the
version bump + tag + push — everything else here is manual and easy to forget.

## Before running the script

- [ ] All PRs for this release are merged to `main`; CI is green on `main`.
- [ ] `CHANGELOG.md`: rename `[Unreleased]` to `[X.Y.Z] - YYYY-MM-DD`, leave a fresh empty
      `[Unreleased]` heading above it, and add the compare link at the bottom
      (`[X.Y.Z]: .../compare/vPREV...vX.Y.Z`, and update `[Unreleased]`'s link to
      `compare/vX.Y.Z...HEAD`).
- [ ] `README.md`: update anything version-specific — supported RID list, "all N RID
      targets" wording, any behavior described in Quick Start that changed.
- [ ] `SevenZipSharper.Native.csproj` `<Description>`: update if platform/RID coverage
      changed.
- [ ] If the release bundles multiple issues/PRs, consider whether it's a MAJOR bump
      (breaking change) per SemVer — see `/Development/CLAUDE.md` → Versioning.
- [ ] These doc changes land as their own PR *first*, merged to `main`, before tagging —
      don't bundle them into the release commit itself (`release.sh` requires a clean
      tree on `main`, and doc review shouldn't block the tag).

## Running the script

```bash
git checkout main && git pull --ff-only
./scripts/release.sh X.Y.Z
```

This bumps `<Version>` in both `SevenZipSharper.csproj` and `SevenZipSharper.Native.csproj`
(skipped if already at that version — e.g. if you bumped it manually in the docs PR above),
commits, tags `vX.Y.Z`, and pushes both. The tag push triggers `publish.yml`, which packs
and pushes both nupkgs to NuGet.org via Trusted Publishing.

Preconditions the script enforces: valid semver, on `main`, clean tree, tag doesn't already
exist locally or on origin, and the matching `natives-v<7zip-version>` GitHub release exists
(run `gh workflow run build-natives.yml --ref main` first if not — the natives version comes
from `scripts/7zip-version`, not the SevenZipSharper version).

## After the script — do NOT forget these, they are not automated

- [ ] **Watch `publish.yml`** (`gh run watch <id>` or the Actions tab) — confirm both
      packages pushed to NuGet.org successfully.
- [ ] **Create the GitHub Release.** The script only pushes the tag — it does **not**
      create a Release. Do it manually:
      ```bash
      awk '/^## \[X\.Y\.Z\]/{flag=1; next} /^## \[PREV\]/{flag=0} flag' CHANGELOG.md > /tmp/notes.md
      gh release create vX.Y.Z --title "vX.Y.Z" --notes-file /tmp/notes.md
      ```
- [ ] **Close the GitHub milestone** if this release completes one:
      ```bash
      gh api repos/Dura-IT/sevenzipsharper/milestones --jq '.[] | {number, title, open_issues}'
      gh api repos/Dura-IT/sevenzipsharper/milestones/<number> -X PATCH -f state=closed
      ```
      Only close it if `open_issues` is 0 — don't close a milestone with unfinished work.
- [ ] Spot-check the NuGet.org listing pages render correctly (README, icon, version).