# InspectNow

InspectNow is a backend for teams that run repeatable site inspections. A team can publish a checklist, start an inspection from it, record results, and submit a completed inspection. Each inspection keeps a snapshot of its checklist so the original questions remain visible in the inspection record.

## What works today

- Create a template, add questions, and publish it for use in inspections.
- Start an inspection at a site, save answers, and submit once all required questions have a Pass or Fail result.
- Browse inspections by status, template, site, or start date. Concurrent edits use a version token to prevent silent overwrites.

The solution uses ASP.NET Core on .NET 10, EF Core, and PostgreSQL. The [project guide](docs/PROJECT_GUIDE.md) walks through the domain rules, use cases, persistence, and API. The [CI workflow](.github/workflows/ci.yml) defines PostgreSQL integration tests and a container smoke test.

## Run the local demo

The React frontend in `InspectNow.Web` opens directly into a working demo with browser-local data. See the [frontend guide](InspectNow.Web/README.md) for how to open it and connect to the API.

With Docker and Compose available, set up the two [local database password files](docker/secrets/README.md), then run these commands from the repository root.

```powershell
docker compose up --build -d
curl.exe --fail http://localhost:18080/health/ready
```

If the readiness check fails during startup, run it again after the migration service completes. The API exposes its local OpenAPI document at `http://localhost:18080/openapi/v1.json`.

1. Follow the [organization access setup](docs/ORGANIZATION_ACCESS.md) to configure an identity provider and grant the demo user a membership.
2. In [InspectNow.Api.http](InspectNow.Api/InspectNow.Api.http), set `baseUrl` to `http://localhost:18080` and select the **local** environment. Run the requests to create a draft, add a required question, and publish the template. Use the ID returned by the create request.
3. In [InspectionWorkflow.http](InspectNow.Api/InspectionWorkflow.http), use the same `baseUrl` and environment. Start an inspection using the published template ID, then run the read, answer, and submit requests using the returned IDs and version tokens.

`docker compose down` stops the stack and preserves its database volume. The local API is intended for loopback use.

## Next product milestone

The [development log](docs/DEVELOPMENT_LOG.md) records each code update and the reason for it.

Build a browser demo for one inspection workflow. A manager signs in, publishes a checklist, and assigns it to an inspector at a site. The inspector completes it on a phone, and the manager can review the submitted record.

The API now authenticates users through an external identity provider and verifies membership before granting access to a workspace. Next, add sites, assignments, and the mobile workflow to complete the browser demo.
