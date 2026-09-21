---
description: Audit new third-party files against docs/LICENSES.md
---
Compare `git diff --name-only HEAD~5` (or the paths in $ARGUMENTS) against `docs/LICENSES.md`.

For every added file under `Assets/` that is not our own code (models, textures, audio, CSV, DLLs, imported packages):
- confirm it has a matching row in `docs/LICENSES.md` with source URL, licence and a commercial verdict of OK or Conditional;
- if Conditional, confirm the condition is satisfied (attribution line present in `Assets/_Project/UI/Credits.uxml`, or compiled-only note, etc.);
- if it is missing, list it and stop — do not invent a licence.

Report as a table: file · source · licence · verdict · action needed.
