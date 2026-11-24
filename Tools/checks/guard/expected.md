# Guard checks: the checks must be seen to bite

The other scripts show that the product passes its checks. This one shows that the checks fail when the product is wrong. It builds, in its scratch folder, three stand-ins for the program, each a small shell script with a `#!/bin/sh` line (the hook line and the scripts start the program by path, so such a script works; the script confirms this on the machine it runs on before anything else). Each stand-in answers `--version`, `install --print`, `install` and `init` the way the program does, so that `Tools/smoke.sh` gets as far as its cases, and answers `review` with the first line the program prints (`CommitFence <version> (<folder>)`), nothing else, and a fixed exit code:

- `always-0` exits 0 for every review: it passes everything.
- `always-1` exits 1 for every review: it blocks everything.
- `always-2` exits 2 for every review: it calls everything incomplete.

`Tools/smoke.sh` runs against each one, with its scratch parent inside this script's scratch folder. Smoke prints one `PASS` or `FAIL` line per case. The cases below are named by the title smoke prints. The `rules` and `pushed` scripts of this folder's sibling folders are run against `always-0` the same way, with their output kept in a file of the scratch folder, so that only this script's own `PASS` and `FAIL` lines and its one total line are printed.

| Check | Verdict |
| --- | --- |
| the stand-in runs as a program | each stand-in, started by its path, prints the version line and exits with its own code |
| smoke ends non-zero against each stand-in | exit code is not 0, not 64 and not 70 (a usage or setup error), and the log holds smoke's `TOTAL:` line (it got to its cases) |
| always-0 | `FAIL` is printed for: `a blocking change (judged from the commit)`, `a change that cannot be reviewed`, `wrong settings (exit code 3)`, `staged differs from the folder (staged bad)`, `several refs in one push`, `missing evidence`, `a program older than required`, `rule kind: a compile error`, `rule kind: a code rule (TEAM011)` |
| always-1 | `FAIL` is printed for the clean cases: `a clean change`, `staged differs from the folder (staged ok)`, `a program newer than required`, `install and init: push through init's line` |
| always-2 | `FAIL` is printed for the blocked cases `a blocking change (judged from the commit)`, `rule kind: a compile error`, `rule kind: a code rule (TEAM011)` and for the clean cases `a clean change`, `staged differs from the folder (staged ok)`, `a program newer than required` |
| rules against always-0 | `Tools/checks/rules/run.sh` run against the `always-0` stand-in ends non-zero (not 64 and not 70) and its total line says at least 40 checks failed (measured: 87 of 105; the checks that pass are those about the settings file alone, which no program answer can break) |
| pushed against always-0 | `Tools/checks/pushed/run.sh` run against the `always-0` stand-in ends non-zero (not 64 and not 70) and its total line says at least 50 checks failed (measured: 127 of 278; a program that passes everything accepts every push, so every case that expects a refusal fails) |
| whole against always-0 | `Tools/checks/whole/run.sh` run against the `always-0` stand-in ends non-zero (not 64 and not 70) and its total line says at least 60 checks failed (measured: 160 of 345, in 27 seconds; a program that passes everything passes every check that expects exit 0 or no finding, and fails every check that expects a finding, an exit code of 1, 2 or 3, or a printed note) |
| filerules against always-0 | `Tools/checks/filerules/run.sh` run against the `always-0` stand-in ends non-zero (not 64 and not 70) and its total line says at least 20 checks failed (measured: 51 of 56, in 10 seconds; the checks that pass are those that expect no output or exit 0 from a program that prints nothing) |
| time | the whole script ends in under ten minutes; smoke is not weakened to get there. Measured: the `pushed` script alone takes about 275 seconds, and the guard alone about 330 seconds |

Smoke against a stand-in is fast because no case builds anything: the program is the stand-in, and the builds of the real program are what makes smoke take two minutes.
