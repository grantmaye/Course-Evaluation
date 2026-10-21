# Interview walkthrough

## Honest 60-second introduction

“This is a new, AI-assisted practice project in the course-evaluation domain, not former employer code. I use it to explain an ASP.NET Core API, persistence and reporting tradeoffs. It accepts a rating, validates it, persists it, and summarizes responses by institution and year. I kept it as one small service so I could focus on correctness rather than add distributed-system complexity.”

Only use this wording after personally reviewing the demonstration. Do not imply you independently authored unfamiliar code.

## Five-minute demonstration

1. Run the test suite and API. Describe what actually passed in the verification record.
2. GET the 2025 report. Explain why response-count weighting matters.
3. POST a new evaluation. Inspect HTTP 201 and its Location header.
4. Replay the same payload. Explain the 200 response and unchanged timestamp.
5. Change its rating but reuse its ID. Explain the 409 conflict.
6. Submit course 201 under institution 1. Explain API validation plus database constraints.
7. Open the code and follow route → service → DbContext → SQLite. No repository wrapper was added solely to wrap EF Core.

## Questions to prepare for

**Why async?** With a truly asynchronous database provider, database waits need not occupy a request thread. Microsoft.Data.Sqlite does not provide true asynchronous I/O, so these async-shaped EF calls are not proof of nonblocking SQLite execution. They preserve an API shape that works with asynchronous providers. The cancellation token is forwarded; this is not a promise to interrupt an already-running SQLite operation.

**What happens if two requests arrive together?** Both may initially see no existing ID. The primary-key constraint is the final uniqueness authority. The losing insert checks the persisted record and returns replay or conflict. SQLite's write serialization is a limitation, not proof of distributed high-throughput behavior.

**Why not average the course averages?** Courses may have different response counts. Aggregate the rating totals and divide by total responses. Otherwise a tiny course can receive the same influence as a large one.

**Why this date predicate?** SubmittedAtUtc >= January 1 and < next January 1 avoids end-of-day precision errors. It also avoids applying a year function to every timestamp in the query. The actual database query plan still needs measurement before claiming performance gains.

**Why SQLite instead of SQL Server?** A portable local demonstration avoids requiring a database server or credentials. EF Core preserves a useful separation, but provider behavior is not interchangeable without tests.

**How would this become enterprise-ready?** Verified identity, institution membership and authorization, real survey/enrollment rules, privacy review, durable migration/release process, SQL Server integration tests, rate limits, structured telemetry, readiness checks, backup/restore and load tests. Add these intentionally, not by claiming the demo already has them.

**Where do microservices fit?** Only when independently deployable capabilities and organizational/scale boundaries justify messaging, distributed tracing, consistency and operational costs. A modular service is a reasonable starting point.

**What would CI/CD do?** Restore dependencies, compile, run tests, publish a versioned artifact, scan it, then deploy through environment approval gates. This repository's workflow covers build/test only; Azure deployment is not implemented.

## Connect to the role without rewriting history

The job asks for C#, ASP.NET Core, REST APIs, source control, CI/CD, troubleshooting and collaboration. This new demonstration gives concrete code for those technical discussions. For GAP Technologies, use only verified résumé details and personal recollection. Modern .NET 8, EF Core, SQLite and this idempotency design are demo choices, not claims about GAP's historical architecture.
