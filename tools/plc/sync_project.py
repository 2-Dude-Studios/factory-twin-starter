"""Writes the OpenPLC Editor project's main.st from cell.st, so cell.st stays the one source of the PLC program.

Usage: python sync_project.py [project_dir]   (default: ./openplc_project)
The Editor reads pous/programs/main.st when the project is opened; reopen it after running this.
"""
import pathlib
import sys

here = pathlib.Path(__file__).resolve().parent
project = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else here / "openplc_project"
source = (here / "cell.st").read_text(encoding="utf-8").replace("\r\n", "\n")

start = source.index("PROGRAM cell")
end = source.index("END_PROGRAM") + len("END_PROGRAM")
program = source[start:end].replace("PROGRAM cell", "PROGRAM main", 1)

target = project / "pous" / "programs" / "main.st"
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text(program + "\n", encoding="utf-8", newline="\n")
print(f"wrote {target}")
