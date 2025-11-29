## ParentGuard (Jellyfin) — Project Transfer Guide

This document is a complete handoff for the ParentGuard plugin so a new developer can assume ownership quickly in a corporate Cursor workspace. It covers project context, goals, local and homelab setup, architecture, CI/CD, known issues, and immediate next steps.


### 1) What ParentGuard Is

ParentGuard is a Jellyfin server plugin that provides parental controls tailored for a child with autism and ADHD. It enforces:
- Fast-forward rate limits (e.g., max 3 seeks within a 30-minute window).
- Episode switching limits over a window.
- Downtime schedules and per-day time budgets.
- Real-time enforcement that can actively stop playback if limits are exceeded.
- A web-based configuration UI for parents to manage policies and administer approvals/unlocks.

The plugin targets Jellyfin 10.10 with a hands-off CI release process (GitHub Actions) that builds, packages, computes checksums, and opens a PR to update the plugin `repository.json`. The plugin is installable via the Jellyfin Catalog with a custom logo.


### 2) Direction and Goals

Core product goals:
- Enforce limits in real time on Jellyfin 10.10 (seek/switch, schedules, daily budgets).
- Provide a simple UI for parents to configure per-profile policies, approvers, and unlocks.
- Maintain a fully automated release flow that bumps versions, creates artifacts, updates checksums, and proposes catalog updates via PR.
- Keep owner/developer metadata as “burrellka” and maintain a catalog icon.

Immediate engineering goals (short-term):
- Confirm the hosted enforcement service loads and stops playback at start when disallowed; record consumption time against budgets.
- Finish seek and episode switch detection within Jellyfin 10.10’s event model.
- Harden UI and API mapping for the XML-safe configuration model (lists instead of dictionaries).
- Keep the CI workflow stable (MD5 checksums, `repository.json` top-level array JSON, PR-only updates).


### 3) Environment Overview

Homelab:
- Jellyfin 10.10.7 running as a container app on TrueNAS SCALE.
- Server base URL example: `http://192.168.13.5:8096` (replace with your network).
- Jellyfin plugin configuration path (inside container): `/config/plugins/ParentGuard`.
- Logs: `/config/log/*.log` (tail and grep for plugin logs).

Client/admin:
- Windows 10/11 dev machine using PowerShell.
- Note: PowerShell doesn’t support `&&`. Use new lines or `;` between commands.

Jellyfin/Catalog:
- Target ABI: `10.10.0.0` (for Jellyfin 10.10.x).
- Plugin repository URL (catalog feed) points to `plugins/ParentGuard/repository.json` in this repo’s main branch.
- Owner/developer shown as `burrellka`.

Repo:
- GitHub: `https://github.com/burrellka/JellyFin-PC-KB`
- Plugin source: `plugins/ParentGuard/src/ParentGuard`
- Workflows: `.github/workflows/release-parentguard.yml` (primary), `.github/workflows/release.yml` (secondary/legacy).


### 4) Local Dev Setup

Prerequisites:
- .NET 8 SDK
- Git + GitHub access
- Editor: Cursor (VS Code also works)

Build locally:
```
dotnet --info
dotnet restore plugins/ParentGuard/src/ParentGuard/ParentGuard.csproj
dotnet build -c Release plugins/ParentGuard/src/ParentGuard/ParentGuard.csproj
```

PowerShell tip (no `&&`):
```
git add -A
git commit -m "Your message"
git push
```

Manual container deploy (fallback only; prefer Catalog):
```
# Inside Jellyfin container shell
mkdir -p /config/plugins/ParentGuard
cd /config/plugins/ParentGuard
curl -L -o Jellyfin.Plugin.ParentGuard.dll "https://github.com/OWNER/REPO/releases/download/vX.Y.Z/Jellyfin.Plugin.ParentGuard.dll"
curl -L -o manifest.json "https://github.com/OWNER/REPO/releases/download/vX.Y.Z/manifest.json"
```

Clean reinstall (if stuck on old version):
```
# Stop Jellyfin service, then delete ALL ParentGuard plugin folders:
rm -rf /config/plugins/ParentGuard
rm -rf /config/plugins/ParentGuard_*
# Restart Jellyfin and install ParentGuard from the Catalog
```


### 5) Repository Structure (key areas)

```
plugins/ParentGuard/
  ├─ src/ParentGuard/
  │  ├─ Controllers/
  │  │  ├─ LabelsController.cs        # Admin/child labels
  │  │  ├─ PolicyController.cs        # CRUD + mapping for policies
  │  │  ├─ RequestsController.cs      # Approvals, unlocks, locks
  │  │  ├─ StateController.cs         # Current state (budgets, cooldowns)
  │  │  └─ UsersController.cs         # List Jellyfin users
  │  ├─ Services/
  │  │  ├─ EnforcementHostedService.cs # Real-time enforcement (10.10 event model)
  │  │  ├─ EnforcementService.cs       # Decisions: allow/deny start/seek/switch
  │  │  ├─ PolicyService.cs            # Load/save policies (XML-safe)
  │  │  ├─ StateService.cs             # Per-user runtime state, events history
  │  │  ├─ DailyResetService.cs        # Midnight resets as needed
  │  │  └─ ServiceHub.cs               # Fallback singleton to avoid DI gaps
  │  ├─ Web/
  │  │  └─ parentguard.html            # Fully inlined JS + CSS configuration UI
  │  ├─ Defaults.cs
  │  ├─ Plugin.cs                      # Plugin metadata + IHasWebPages
  │  ├─ PluginConfiguration.cs         # Persistent config model (lists, not dicts)
  │  ├─ Startup.cs                     # DI registrations + hosted service
  │  ├─ StartupServices.cs             # Helper to add services
  │  └─ ParentGuard.csproj
  ├─ manifest.json                     # Plugin metadata (owner/version/targetAbi)
  ├─ repository.json                   # Catalog entry (top-level array format)
  └─ icon.png                          # Plugin tile logo

.github/workflows/
  ├─ release-parentguard.yml           # Primary CI: build, zip, MD5, open PR
  └─ release.yml                       # Secondary/legacy CI
```


### 6) Architecture Overview

Core plugin pieces:
- `Plugin.cs` implements `IHasWebPages` to register `Web/parentguard.html` as the config page, with `EnableInMainMenu = true` and lowercase page key (“parentguard”). Version and owner metadata are set here and in `manifest.json`.
- `Startup.cs` registers services into Jellyfin’s DI and wires `EnforcementHostedService` to run. This was previously disabled; it is now enabled.
- `ServiceHub.cs` provides fallback singletons in controllers/services where DI may not be available early in plugin load, preventing 500s from null services.

Configuration model (XML-safe):
- Replaced dictionaries with lists to satisfy XML serialization constraints.
  - `PluginConfiguration.Profiles`: `List<ProfileEntry>`
  - `PluginConfiguration.Admins`: `List<AdminEntry>`
  - `ProfilePolicy.Budgets` (list of `{ Day, Minutes }`) replaces `BudgetsByDow` dict.
  - `ProfilePolicy.Schedules` (list of `{ Label, Windows[] }`) replaces schedules dict.
- `PolicyController` maps internal lists <-> UI’s dictionary-shaped JSON so the UI remains simple.

Enforcement:
- `EnforcementHostedService` subscribes to `ISessionManager` events (`SessionStarted`, `SessionEnded`, `PlaybackStart`, `PlaybackProgress`, `PlaybackStopped`). On Jellyfin 10.10, user and session IDs are available via `e.Session.UserId` and `e.Session.Id`.
- `EnforcementService` computes allow/deny decisions using policy + runtime state. It supports unlock bypass (`ActiveUnlockUntilUtc`) and windowed rate-limit checks for seeks/switches using lists of recent events stored in `UserState`.

UI:
- `Web/parentguard.html` is a fully inlined page (HTML + JS + CSS) to avoid 404s for external assets. It renders Approvers/Children pickers, per-day budgets, allowed hours, and rate limits. All API calls include the Jellyfin API key in both header and query string.

API endpoints (high level):
- `GET /ParentGuard/policy` — Get all policies (dictionary-shaped JSON for UI).
- `POST /ParentGuard/policy` — Upsert a child’s policy (UI JSON -> internal lists).
- `POST /ParentGuard/labels` — Set approver/child labels.
- `POST /ParentGuard/requests/approve` — Approve/authorize requested action.
- `POST /ParentGuard/requests/unlock` — Time-bound unlock with reason.
- `POST /ParentGuard/requests/lock` — Lock immediately.
- `GET /ParentGuard/state?userId=...` — Current state view (minutes used, cooldown, unlock status).
- `GET /ParentGuard/users` — List Jellyfin users (uses `Username` field).


### 7) CI/CD and Release Flow (Hands-Off)

Tag-driven releases:
1) Commit changes and push to `main` (or a branch). Then create an annotated tag:
```
git tag -a v0.1.21 -m "ParentGuard v0.1.21"
git push origin v0.1.21
```
2) The `release-parentguard.yml` workflow will:
   - Build and package `ParentGuard.zip` containing `Jellyfin.Plugin.ParentGuard.dll` and `manifest.json` at the zip root.
   - Compute MD5 checksum of the zip.
   - Update `plugins/ParentGuard/repository.json` (top-level array) by inserting/updating the version entry with `version`, `targetAbi`, `sourceUrl`, `checksum` (MD5), and `timestamp`.
   - Open a Pull Request with just the `repository.json` change (does not push directly to `main`).
   - Uses `permissions: pull-requests: write` and unique PR branch naming to avoid non-fast-forward or ref-exists errors.
3) Merge the PR to publish the catalog update.
4) In Jellyfin, refresh the plugin catalog and install/update ParentGuard.

Important CI details:
- `repository.json` must be a top-level JSON array (Jellyfin 10.10 requirement), not an object with `.packages`.
- Use MD5, not SHA256, for `checksum` (for catalog compatibility).
- Make sure `manifest.json` shows `owner: "burrellka"`, correct `guid`, `version`, and `targetAbi: "10.10.0.0"`.


### 8) Install/Upgrade via Catalog

Jellyfin Dashboard → Plugins → Repositories:
- Add ParentGuard repository URL pointing to raw `plugins/ParentGuard/repository.json` in this repo’s `main` branch.
- After merging the CI PR, refresh Catalog and install/update “ParentGuard”.

If uninstall/update issues occur (common when switching deployment sources):
- Stop Jellyfin, delete all ParentGuard plugin folders in `/config/plugins` (including version-suffixed ones), restart, and install from Catalog again.


### 9) Known Issues Encountered (and fixes already applied)

Plugin not showing or “Malfunctioned”:
- Cause: Missing DI registrations and/or page registration. Wrong version fields.
- Fix: Enable DI in `Startup.cs`; implement `IHasWebPages` in `Plugin.cs`. Set `AssemblyVersion`, `FileVersion`, and `InformationalVersion` in `ParentGuard.csproj` to match the plugin version. Ensure `targetAbi: "10.10.0.0"`.

Configure page wouldn’t open / 404s on assets:
- Cause: DOM expectations and missing external assets.
- Fix: Conform HTML structure and inline JS/CSS into `parentguard.html`. Use lowercase page name key. `EnableInMainMenu = true`.

500 Internal Server Errors on `/ParentGuard/policy`:
- Cause: XML serializer can’t handle `IDictionary`. Logs showed `NotSupportedException` for `ProfilePolicy.BudgetsByDow`.
- Fix: Convert all persistent dictionaries to lists (`PluginConfiguration`, `ProfilePolicy`). Add mapping in `PolicyController` between UI dict-shaped JSON and internal list model.

GitHub Actions failures (non-fast-forward, `jq`, PR permissions):
- Cause: Direct pushes to `main` from CI, incorrect `jq` transformations, insufficient permissions.
- Fix: CI now uses `peter-evans/create-pull-request` to open PRs, adds `pull-requests: write`, corrects `jq` for top-level array, switches to MD5 checksums, and uses unique PR branch names.

Plugin stuck on old version (e.g., 0.1.15) or “Repository: Unknown”:
- Fix: Fully remove old plugin directories under `/config/plugins`, restart Jellyfin, then install from Catalog.

Compilation errors on Jellyfin 10.10 (`IPlaybackManager` missing, `PlaybackProgressEventArgs` fields missing):
- Fix: Use `ISessionManager` events and retrieve user/session via `e.Session.UserId` / `e.Session.Id`. Remove references to non-existent properties. Fixed Guid nullability issues (`Guid` is a value type).


### 10) Current Open Items

- Seek/switch enforcement on 10.10: Implement robust detection using only event data available in `PlaybackProgressEventArgs.Session` and other feasible hooks. If needed, correlate position deltas or state transitions to infer seeks/switches.
- Confirm `EnforcementHostedService` reliably receives events and calls `EnforcementService` decisions (start/stop/time tracking). Add targeted logging with user/session context.
- Tighten `PolicyService` and `StateService` persistence/rotation (clear daily windows; ensure midnight resets via `DailyResetService`).
- UI polish: Eliminate “Translation key is missing from dictionary” warnings; finalize form validation; better error banners with HTTP code and response text (already partly done).
- End-to-end tests: Basic smoke tests for config CRUD and enforcement guardrails.


### 11) Handoff Checklist

- Access to repo and CI workflows (permission to push tags and merge PRs).
- Confirm `.github/workflows/release-parentguard.yml` runs on tag push and opens PRs successfully.
- Verify `plugins/ParentGuard/repository.json` remains a top-level array; entries include `imageUrl` for the icon; checksum is MD5.
- Validate `manifest.json` owner (`burrellka`), version, and `targetAbi`.
- Install latest ParentGuard from Catalog on a Jellyfin 10.10.7 test server and verify the Configure page loads and can save/read policies.
- Start playback in a child profile and confirm start enforcement and budget accounting; then add seek/switch tests.


### 12) Useful Commands and Examples

Logs (inside container):
```
tail -n 1000 /config/log/*.log | grep -Ei 'ParentGuard|PolicyController|RequestsController|StateController|LabelsController|Exception|NullReference|Invalid|Serializer'
```

API token usage (replace placeholders):
```
BASE="http://YOUR_JELLYFIN:8096"
TOKEN="YOUR_API_TOKEN"

curl -sS -H "X-Emby-Token: ${TOKEN}" "${BASE}/ParentGuard/policy"

curl -sS -X POST \
  -H "Content-Type: application/json" \
  -H "X-Emby-Token: ${TOKEN}" \
  "${BASE}/ParentGuard/requests/unlock?api_key=${TOKEN}" \
  -d '{
        "userId":"{childUserGuid}",
        "minutes":30,
        "reason":"Homework done"
      }'
```

PowerShell notes:
```
# Do NOT use && in PowerShell. Use new lines or semicolons:
git add -A
git commit -m "ParentGuard: change note"
git push
```


### 13) File-by-File: High-Signal Details

- `Services/EnforcementService.cs`
  - Parameterless constructor uses `NullLogger` as DI fallback.
  - `ShouldAllowSeek` / `ShouldAllowSwitch` check rate limits using event lists (`SeekEvents`, `SwitchEvents` in `UserState`).
  - Unlock bypass: if `ActiveUnlockUntilUtc > now`, enforcement returns allowed.

- `Services/EnforcementHostedService.cs`
  - Subscribes to `ISessionManager` events only (Jellyfin 10.10).
  - Avoids `IPlaybackManager` and non-existent event args fields; uses `e.Session.*`.
  - Enforces on `PlaybackStart` and tracks time; refine seek/switch detection next.

- `Controllers/*Controller.cs`
  - All controllers include fallback constructors using `ServiceHub` (or `Plugin.Instance`) to avoid 500s when DI is not fully wired yet.
  - `UsersController`: use `Username` instead of `Name` to list users.
  - `PolicyController`: maps UI dict-shaped JSON <-> internal XML-safe list model.

- `PluginConfiguration.cs`
  - `Profiles: List<ProfileEntry>` and `Admins: List<AdminEntry>`.
  - `ProfilePolicy` uses `List<BudgetByDay>` and `List<ScheduleByLabel>`.
  - Helpers for dictionary projections and upserts are provided.

- `Web/parentguard.html`
  - Inlined JS/CSS; includes error handling and explicit API token in header and query string. Renders approvers/children selectors and per-profile configuration.

- `manifest.json`
  - Owner `burrellka`. Version matches plugin assembly version. `targetAbi = "10.10.0.0"`.

- `repository.json`
  - Top-level array format (not `{ packages: [] }`). Includes `imageUrl` for the icon. MD5 checksums.

- CI (`release-parentguard.yml`)
  - Zips DLL + manifest at zip root.
  - Computes MD5 checksum.
  - Uses `jq` to update top-level array and opens a PR via `peter-evans/create-pull-request@v5`.
  - Grants `pull-requests: write` and uses a unique PR branch name.


### 14) Next Steps for the New Owner

1) Build locally and tag a canary release (e.g., `v0.1.22`) to verify CI opens a PR updating `repository.json` with MD5 and correct URLs.
2) Merge PR, refresh the Catalog in a test Jellyfin 10.10.7 server; install ParentGuard.
3) Confirm Configure page loads and saves policies; verify approver/child labels.
4) Start playback with a child profile to confirm start enforcement; review logs for decisions.
5) Implement/finish seek and switch detection compatible with Jellyfin 10.10 events.
6) Address UI translation warnings and finalize form validations.


### 15) Contact and Ownership

- Plugin owner/developer: `burrellka` (as shown in plugin manifest/catalog).
- Repository: `https://github.com/burrellka/JellyFin-PC-KB`

If you need additional environment details (TrueNAS SCALE app configuration, Docker compose, firewall, or reverse proxy specifics), capture them in an internal runbook alongside this guide.


