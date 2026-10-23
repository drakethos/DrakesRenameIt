# RenameIt automated tests

DrakesTestLab suites for RenameIt. What maps to the test sheet, and what is still manual: [COVERAGE.md](COVERAGE.md).

- `suites/DrakesRenameIt/`: automated (50 tests). Runs unattended.
- `suites/DrakesRenameIt-Manual/`: needs a person (real keys, windows, language, relaunch, other mods, a second real client). Each test sets up and asks Pass / Fail.
- `suites/DrakesRenameIt/_functions/`: steps many tests share (`writePage`, `pinPage`, `craftRenamed`, `editAll`, `checkAll`, `itemOnStand`, `splitStacks`), called like steps: `"writePage(\"Shared Page\")"` or `{ "do": "pinPage", "label": ... }`.
- `suites/DrakesRenameIt/_shared/baseline.json`: required mod, the config baseline and start state; both suites `include` it (the manual suite also includes the functions).
- `gen_renameit_suite.py`: writes both suite folders (`python gen_renameit_suite.py`). Edit tests and functions there, not in the JSON.
- `../Testing/RenameItTestHooks.cs`: the `renameit.*` test hooks the suites call. They use RenameIt's real permission and apply code. No reference to DrakesTestLab; without it installed they are never called.

## Run

1. Build RenameIt (deploys to the drakeTest profile) and have DrakesTestLab in that profile.
2. Copy both suite folders (with their `_functions/` and `_shared/` subfolders) into `<profile>\BepInEx\config\DrakesTestLab\suites\`.
3. With the game closed:

```powershell
..\DrakesTestLab\tools\Run-TestLabSuites.ps1 -Suites DrakesRenameIt -Screenshots Always
```

Or in game: F9 > Tests > DrakesRenameIt > Run suite. The manual suite only runs from the Tests tab.

## Results

`<profile>\BepInEx\config\DrakesTestLab\results\latest.html` (open in a browser): every test, each check, the step log and a screenshot of each test's end state. `latest.json` has the same for scripts.

## Notes

- The suite baseline is the sheet's INITIAL TEST config, except: unlock cost off by default (its own tests turn it on) and **AllowAdminOverride off**, because the single-player host counts as an admin and would bypass every rule.
- Owned items are crafted through the game (`craft` step); other players are simulated with play-as (Alice, Bob, Nacho-VIP).
- Test names start with the sheet id (SM-xxx, TC-xxx); X-xx tests are extra coverage not in the sheet.
