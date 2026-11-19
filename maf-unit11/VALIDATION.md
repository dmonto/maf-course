# Local validation

Validated on 7 November 2026 with .NET SDK 10.0.100 on Linux and PowerShell. The runtime output is checked by the programs and by `Tests/Unit11.Checks`.

* All eleven solution projects restored and built with zero warnings and zero errors. Exact transitive package versions are retained in `packages.lock.json` files.
* v01 accepted one durable envelope, suppressed a matching duplicate, rejected an identity conflict and committed a claimed job.
* v02 emitted fixture text incrementally and produced a final explanation with first-text and total processing timings.
* v04 completed the transient event on attempt 2, stored no failed-attempt text and quarantined the poison event on attempt 1.
* v05 completed eight independent events and measured a maximum of two active generation attempts.
* v06 seeded, processed and verified a saved envelope in three separate operating-system processes, retaining the original result and duplicate identity.
* v07 completed the durable event-to-explanation path in fixture mode.

* Eight integration check groups passed: simultaneous deduplication and conflict; expired owner fencing and durable completion; abandoned final attempt; three-attempt transport retry bound; generated-output bound; HTTP contracts and limits; stale HTTP evidence rejection; atomic HTTP 429 admission with duplicate priority and later retry.
* Every PowerShell script passed parser validation. The real PowerShell event client submitted a fresh event and replayed its exact saved envelope from another working directory, using a filename containing spaces; the result remained on attempt 1 and the JSON file had no UTF-8 BOM.
* The ZIP was extracted into a new directory containing spaces. The complete solution restored in locked mode with `-m:1`, built with zero warnings or errors, and the PowerShell restart example successfully launched its three child processes from that extracted copy.

Azure inference and real Service Bus send/receive were compiled against the pinned APIs but were not executed with external resources. The fixture is an explicit execution mode and never a fallback after a failed live call.

Reproduce with `Scripts/Test-Examples.ps1`. The suite sends only loopback HTTP requests and makes no Azure or Service Bus calls.
