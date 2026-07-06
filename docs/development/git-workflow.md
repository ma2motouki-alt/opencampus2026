# Git Workflow

This repository uses a small pull request workflow for two-person development.

## Branch Policy

- `main` should stay playable.
- Do not work directly on `main`.
- Create one `feature/...` branch per task.
- Keep each pull request focused on one purpose.
- Commit Unity `.meta` files with the assets or scripts they belong to.
- Do not commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `work/`, or `outputs/`.

Recommended branches:

- `feature/realsense-udp-input` for RealSense / UDP input work.
- `feature/world-behavior-*` for little-people behavior changes.
- `feature/visual-*` for visual or animation changes.

## Daily Flow

Start from the latest `main`:

```powershell
git switch main
git pull
git switch -c feature/realsense-udp-input
```

Work normally in Unity or Python, then check the diff:

```powershell
git status
git diff
```

Commit your work:

```powershell
git add .
git commit -m "Add UDP input provider"
git push -u origin feature/realsense-udp-input
```

Open a Pull Request on GitHub, ask the other developer to review it, then merge it into `main`.

After a PR is merged, both developers update their local copy:

```powershell
git switch main
git pull
```

## Pull Request Checklist

Before opening a PR:

- Unity opens with the expected editor version.
- Play Mode still works with mouse input.
- New scripts have their `.meta` files.
- The PR does not contain generated Unity folders.
- The PR has a short explanation of what changed and how it was tested.

## Conflict Notes

Unity scenes and prefabs can be hard to merge. If both developers need to edit the same scene or prefab, decide who owns that file for the task before starting.

Code and docs are usually easier to merge, but still keep PRs small.
