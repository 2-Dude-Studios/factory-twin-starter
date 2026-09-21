---
description: Start or continue the current milestone from docs/build-plan.md
---
Read `docs/STATUS.md` for the current milestone and what is done. Read the matching milestone section in `docs/build-plan.md` for its exit condition.

Then:
1. List the remaining work for this milestone as a short checklist, smallest shippable step first.
2. Do the first step. Keep edits inside `Assets/_Project/` and `docs/`.
3. If a step needs something in the Unity Editor that the CLI cannot do, write the exact Inspector steps into `docs/STATUS.md` under "Manual steps pending" instead of guessing.
4. Update `docs/STATUS.md` (done / pending / blockers) and commit with `M<n>: <what>`.

$ARGUMENTS
