# Course Evaluation API (.NET)

A small, independent, AI-assisted interview demonstration using fictional course-evaluation data. It is **not GAP Technologies/SmartEvals source code**, a reconstruction of a client system, or evidence of historical work in .NET 8. Review and understand the code before discussing it as a demonstration.

## What it demonstrates

- C# / ASP.NET Core 8 minimal REST API with dependency injection and async EF Core queries.
- SQLite persistence, database constraints and a composite reporting index.
- Server-side validation, HTTP status codes and Problem Details.
- An idempotency key: retrying an identical submission returns the original record; reusing its ID for changed data returns HTTP 409.
- Correct weighted reporting and half-open UTC date filtering.
- Integration tests against the actual HTTP pipeline and a real temporary SQLite database, not an in-memory EF substitute.

This is intentionally one service, not unnecessary microservices. No account, paid service, Azure resource, Mac-specific dependency or production deployment is required.

## Run locally

Install the official [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0). The pinned SDK is 8.0.426; newer patches in that feature band are allowed.

```sh
dotnet restore
dotnet test
dotnet run --project src/Evaluation.Api
```

The service binds to http://127.0.0.1:5080. Five synthetic records are seeded once into a local SQLite file. Stop with Ctrl+C. Do not expose this unauthenticated demo to the internet or put real student information in it.

```sh
curl 'http://127.0.0.1:5080/api/reports?institutionId=1&year=2025'
curl -i 'http://127.0.0.1:5080/api/evaluations' \
  -H 'Content-Type: application/json' \
  -d '{"id":"48b911a7-71f6-4119-b86c-f5961751d652","institutionId":1,"courseId":101,"rating":4}'
```

Run the POST twice: expect 201 then 200. Keep the same ID and change the rating: expect 409. A cross-institution course or rating outside 1–5 returns 400. The GET location returned from the first POST retrieves that response.

## Read the code in this order

1. `Models.cs`: domain objects and the two fictional institutions' course catalog.
2. `Program.cs`: routes, dependency injection, validation and HTTP results.
3. `SubmissionService.cs`: replay/conflict semantics and the database uniqueness race.
4. `ReportService.cs`: filtering, grouping and weighted aggregation.
5. `EvaluationDb.cs`: persistent schema, integrity checks and index.
6. `ApiTests.cs`: executable examples and edge cases.

## Report walkthrough

Institution 1 has ratings 5 and 3 for course 101, and 1 for course 102 in 2025. Its overall average is (5 + 3 + 1) / 3 = **3.0**, not (4 + 1) / 2 = 2.5. The record at exactly January 1, 2026 is excluded from 2025 and included in 2026. An empty result reports a null average, not a misleading zero.

## Deliberate boundaries

- **No authentication or authorization.** Institution ID is a query filter, not a security boundary. Production would derive institution membership from verified identity and enforce object-level authorization, including the response-by-ID route.
- **Not an anonymous survey system.** UUID retry protection does not establish one response per enrolled student, prevent abuse or guarantee anonymity. Real privacy rules, invitation/enrollment tokens, retention and minimum cohort sizes need explicit design.
- **SQLite is the tested provider.** SQL Server and Azure are next-step designs, not implemented or verified claims. Provider changes require migrations, SQL translation and concurrency testing.
- **EnsureCreated is demo bootstrapping, not schema migration management.** Production needs reviewed versioned migrations and separate release ownership.
- A static course catalog keeps the example understandable. Real institutions, enrollments, evaluation windows and questions need normalized domain tables.
- A single-instance startup seed is used. Multi-instance initialization, load tests, backup/restore, rate limiting and observability are not claimed complete.
- Request cancellation is forwarded to EF Core. Cancellation after a committed write can still leave a saved record; retry with the same ID to discover it safely.
- The health route proves the HTTP process responds; it is not a database readiness probe.

See [interview walkthrough](docs/INTERVIEW-WALKTHROUGH.md) and [verification record](docs/VERIFICATION.md). The CI workflow builds and tests only, with no deployment or secrets.
