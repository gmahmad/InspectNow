# InspectNow project learning guide

This guide records the code decisions in InspectNow through Stage 3. Read one row per concept, then follow the named code. We will update it with each code package.

Updated 9 October 2026. Stage 3 package; runtime validation status is recorded in STAGE3_UPDATE.md.

## Foundation and inspection workflow

| What we use | Why | How we use it |
|---|---|---|
| Five projects | Separate responsibilities. | Domain holds rules; Application runs use cases; Infrastructure stores data; API handles HTTP; Tests verify behavior. |
| Dependency injection | Replace dependencies and manage lifetimes. | Program registers use cases, repositories and DbContext as scoped; one request gets one context. |
| Domain aggregates | Keep business rules in one place. | InspectionTemplate owns template questions; Inspection owns snapshot questions and answers. |
| Validation | Reject bad input and protect rules. | API annotations validate requests; domain methods protect names, publication, answers and submission. |
| Use cases and repositories | Keep HTTP and database code out of workflows. | Create, publish, start, answer and submit classes use interfaces implemented by Infrastructure. |
| PostgreSQL and EF Core | Store relational data locally. | Npgsql connects EF to PostgreSQL; configurations define fields, relationships and constraints. |
| Migrations and numeric IDs | Evolve schema while preserving records. | Entity IDs use long/bigint identity; the previous GUID conversion retained an audit mapping. |
| DTOs and async queries | Fetch only required data without blocking I/O. | Read projections shape responses; await database calls sequentially on the same DbContext. |
| Published snapshots | Keep old inspections historically accurate. | Starting an inspection copies a published template name, question text and source IDs. |
| Optimistic concurrency | Prevent a late edit overwriting newer work. | ExpectedVersion is checked; EF checks the UUID Version token on save; conflicts return 409. |
| Answers and submission | Enforce a complete, final inspection. | Required answers must be Pass or Fail; submitted inspections reject edits and repeat submission. |
| Time and precision | Make time testable and database round trips stable. | TimeProvider supplies UTC; inspection timestamps are normalized to PostgreSQL microseconds. |
| Tests and Git | Catch regressions and preserve working milestones. | xUnit checks rules; integration tests use inspectnow_tests; commit each verified stage. |

## Stage 2 API foundations

| What we use | Why | How we use it |
|---|---|---|
| Global exception handler | Return safe, predictable unexpected errors. | GlobalExceptionHandler logs exceptions and returns 500 ProblemDetails without database details. |
| ProblemDetails and trace IDs | Connect an API error to its logs. | ApiProblemDetails adds traceId and path; responses include the matching X-Trace-Id header. |
| Structured request logging | Troubleshoot requests by field. | RequestLoggingMiddleware records route template, status and duration; JSON console includes trace scope. |
| Inspection list query | Build the future inspection worklist. | GET /api/inspections filters status, template, site text and start dates through IInspectionQueries. |
| Stable pagination | Keep reads bounded and ordering deterministic. | SQL count then page; newest first, Id breaks ties; page size is 1–100; huge offsets return an empty page. |
| Search and date boundaries | Make filter behavior predictable. | Site matching ignores case and treats wildcards literally; dates use inclusive From and exclusive To in UTC. |
| Health checks | Separate a live process from database availability. | /health/live checks the process; /health/ready checks PostgreSQL connectivity and returns 503 on failure. |
| GitHub CI | Verify builds independently. | ci.yml restores, builds and tests using a disposable PostgreSQL container; TRX results are retained. |
| Configuration and SDK | Support both local development and CI. | Local User Secrets remain; tests accept environment overrides; global.json stays on stable .NET 10. |
| Regression coverage | Exercise the new behavior and earlier bug. | Tests cover error safety, trace matching, health, filters, pagination and timestamp round trips. |

## Stage 3 local containers

| What we use | Why | How we use it |
|---|---|---|
| Docker image | Package the app with its runtime. | Dockerfile builds with the SDK, then copies published output into a smaller ASP.NET runtime image. |
| Non-root API process | Reduce container permissions. | The image runs as the built-in app user and listens on port 8080. |
| Compose services | Start related components together. | compose.yaml defines db, migrate and api; dependencies wait for database health and migration success. |
| Migration job | Apply schema before accepting requests. | The same image runs with --migrate, applies pending EF migrations, then exits without starting HTTP. |
| Named volume | Keep records across container recreation. | postgres_data mounts at /var/lib/postgresql for PostgreSQL 18; normal compose down preserves it. |
| Container networking | Let services find each other. | The API connects to db:5432 internally; your laptop uses API port 18080 and database port 15432. |
| Runtime secrets | Keep passwords out of source and images. | Compose mounts ignored local password files; DatabaseConnectionString reads the application password. |
| Application database role | Avoid server-wide admin rights in the API. | init-db.sh creates inspectnow_app as database owner with no superuser, create-role or create-database rights. |
| Container CI check | Verify the packaged deployment too. | CI builds Compose, checks health, creates a record and confirms it survives container recreation. |

Container mode uses a separate new database. The original Visual Studio/local PostgreSQL mode remains available. Local Compose uses HTTP on loopback; a public deployment will need TLS and authentication.

## Read the code in this order

1. `InspectNow.Api/Program.cs` — registrations and middleware order.
2. `Api/Contracts/Inspections/ListInspectionsRequest.cs` and `Controllers/InspectionsController.cs` — request validation and HTTP response.
3. `Application/Inspections/ListInspections.cs` — query validation and normalization.
4. `Infrastructure/Persistence/Queries/InspectionQueries.cs` — SQL filters, projection and paging.
5. `Api/Diagnostics` and `Api/Health` — errors, request logs and probes.
6. `Tests/Integration/ApiFoundationTests.cs`, `InspectionListingTests.cs` and `.github/workflows/ci.yml` — local and automated verification.

The abbreviated Api, Application, Infrastructure and Tests paths above mean their InspectNow projects.

For Stage 3, read `Dockerfile`, `compose.yaml`, `docker/init-db.sh`, `Api/Configuration/DatabaseConnectionString.cs`, then the `--migrate` branch in `Program.cs`.

## Next stages

Users, organizations and authorization → sites and assignments → template revisions → responsive UI → photos and corrective actions → reports and reliable jobs → AI assistance → Azure deployment → pilot feedback and commercial release. Extract a microservice later as a focused learning exercise.

This remains one application with layered responsibilities. Tenant isolation, authentication and a customer UI are still planned. Pagination count and rows are two reads: concurrent writes can change the total between them. Readiness checks connectivity, not schema currency. Use a real deployment migration step later.

Maintenance rule: retain existing decisions, add a brief row for each new concept, and update this Markdown guide and its Word copy together in every package.
