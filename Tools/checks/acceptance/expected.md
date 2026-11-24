# Acceptance checks: expected results

The acceptance test, through the real hook and the published program. With the
private copy already warm on a solution that targets two frameworks, a push renames a source file that a project
picks up by wildcard and removes a public member that an untouched project uses on only one of the frameworks.

| Claim | Verdict |
| --- | --- |
| 1 | the hook exits 1 and the remote does not move |
| 2 | the output names the project `Consumer` and the framework `net9.0` in a CS0117 error at `Consumer/Use.cs` (the line of `Library.Greeter.Old()`), and does not name `net11.0` for it |
| 3 | the findings of the hook run equal the findings of a review of the same two commits made with an empty data folder (nothing kept, everything rebuilt); and they are not empty |
| 4 | the warm run is the second review of the repository, so the private copy and the kept answers exist: the warm-up push passed (its hook exit code is 0) and its review went through the hook |

The time of the run is printed in the output of the script.
