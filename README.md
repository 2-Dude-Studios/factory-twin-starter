# factory-twin

Factory digital-twin demo for 2 Dude Studios' Manufacturing page. Unity 6000.3 · URP · Windows.

Start here: `CLAUDE.md` (rules for Claude Code), `docs/build-plan.md` (plan and milestones), `docs/STATUS.md` (where we are), `docs/demo-script.md` (what the video shows), `docs/LICENSES.md` (what we may ship).

## First run
1. Unity Hub → New project → 3D (URP) → Unity 6000.3.x → location: this folder (keep the existing files).
2. Follow the M0 checklist in `docs/STATUS.md`.
3. `docker compose -f tools/broker/docker-compose.yml up -d`
4. `pip install -r tools/replay/requirements.txt && python tools/replay/replay.py`
5. `claude` in this folder, then `/milestone`.

Claude Code slash commands: `/milestone`, `/licence-check`, `/test`.
