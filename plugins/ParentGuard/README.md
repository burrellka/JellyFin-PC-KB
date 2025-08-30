ParentGuard (Jellyfin Parental Controls)

Overview
This plugin adds server-side parental controls for Jellyfin:
- Screen-time schedules and daily budgets per profile
- Rate limits for seeking/fast-forward and rapid item switching
- Temporary unlocks with parent/admin approvals (PIN optional)
- Admin UI: policies, live request queue, and quick unlock/lock actions

Status
- This repository currently contains initial scaffolding. Implementation will proceed in stages so it can run on Jellyfin 10.10.x.

Structure
- src/ParentGuard: .NET project for the plugin
- web/ (planned): admin UI assets served by the plugin
- docs/: usage and configuration

Build (placeholder)
The project targets .NET 8. Exact Jellyfin package references will be aligned to 10.10.x. A solution file and CI will be added once the core compiles.


