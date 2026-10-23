"""Writes the OpenPLC Editor project's main.st from cell.st (or another program file), so cell.st stays the one source.

Usage: python sync_project.py [program.st] [project_dir]   (defaults: cell.st, ./openplc_project)
       python sync_project.py cell_buggy.st                 loads the seeded-bug program for the M8 suite
The Editor reads pous/programs/main.st when the project is opened; reopen it after running this.
"""
import pathlib
import sys

here = pathlib.Path(__file__).resolve().parent
program_file = sys.argv[1] if len(sys.argv) > 1 else "cell.st"
project = pathlib.Path(sys.argv[2]) if len(sys.argv) > 2 else here / "openplc_project"
source = (here / program_file).read_text(encoding="utf-8").replace("\r\n", "\n")

start = source.index("PROGRAM cell")
end = source.index("END_PROGRAM") + len("END_PROGRAM")
program = source[start:end].replace("PROGRAM cell", "PROGRAM main", 1)

target = project / "pous" / "programs" / "main.st"
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text(program + "\n", encoding="utf-8", newline="\n")
print(f"wrote {target} from {program_file}")
