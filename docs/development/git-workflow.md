# Git Workflow

## Basic Policy

- `main` should stay runnable.
- Work on feature branches.
- Use one pull request per purpose.
- Commit Unity `.meta` files with their assets.
- Do not commit `Library/`, `Temp/`, `Logs/`, or local Unity cache files.
- Avoid editing the same Unity Scene or Prefab at the same time without talking to the team.

## Daily Start

```powershell
git switch main
git pull
git switch -c feature/your-work-name
```

If the branch already exists:

```powershell
git fetch
git switch feature/your-work-name
git pull
```

## Check Current State

```powershell
git branch --show-current
git status
```

In VS Code, the current branch name is also shown in the lower-left status bar and in the Source Control graph.

## Commit

```powershell
git status
git add .
git commit -m "Describe the change"
```

Before committing, check that only intended files are staged.

## Push

First push of a branch:

```powershell
git push -u origin feature/your-work-name
```

Later pushes:

```powershell
git push
```

## Pull Request

1. Push your feature branch.
2. Open GitHub.
3. Create a Pull Request into `main`.
4. Ask another team member to review when possible.
5. Merge only after checking Unity still opens and the relevant feature works.

## After Merge

```powershell
git switch main
git pull
```

Delete old feature branches when they are no longer needed.

## Clone On Another PC

For the stable current version:

```powershell
git clone https://github.com/ma2motouki-alt/opencampus2026.git
```

For a feature branch:

```powershell
git clone -b feature/branch-name https://github.com/ma2motouki-alt/opencampus2026.git
```

## Unity Notes

- Open the cloned folder with Unity Hub.
- Use Unity `6000.4.10f1`.
- Keep `.meta` files.
- If Unity regenerates many unrelated files, inspect carefully before committing.
