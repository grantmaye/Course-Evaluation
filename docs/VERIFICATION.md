# Verification record

Checked October 21, 2026 in a Linux cloud workspace, without a Mac or database server.

## Passed

- Official SDK 8.0.426, ASP.NET Core runtime 8.0.32.
- NuGet dependency restore from nuget.org; lockfiles generated.
- API and test compilation with warnings treated as errors.
- 20 integration test cases, zero failed and zero skipped: weighted reporting, year boundaries, institution filtering, empty results, invalid/missing inputs, malformed JSON, creation/replay/conflict, simultaneous requests, database check constraints, UTC timestamp persistence and application restart without duplicate seed rows.
- Real Kestrel HTTP startup on loopback, health response, weighted report and HTTP 413 for a body over 16,384 bytes.
- Owned HTTP process shut down after smoke checks.

## Issues found and corrected

Initial tests exposed malformed-input responses becoming HTTP 500 in Development. Explicit route-binding configuration now keeps those at HTTP 400. SQLite DateTime materialization lost the UTC kind; a value converter now restores UTC, with a restart assertion.

## Not yet verified / intentionally absent

- Remote GitHub Actions result is tracked separately in the pull request. Merely including a workflow is not a passing run.
- No SQL Server, Azure deployment, real institutional data, authentication, authorization, production load, accessibility UI or browser layout testing is claimed.
- Concurrency tests validate observable outcomes, not deterministic scheduling of every possible database race.
- No claim that this practice project describes historical employer implementation details.

## Reproduce

```sh
dotnet restore --locked-mode
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release --logger trx
```

The constrained cloud workspace used single-process MSBuild (`-m:1 -nodeReuse:false -p:UseSharedCompilation=false`) and an isolated temporary SDK/NuGet home. Ordinary development machines should not require these environment-specific flags.
