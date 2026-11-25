# InspectNow development log

Add a short entry in the same change as each code update. Record what changed, why it matters, and how it was checked. The [project guide](PROJECT_GUIDE.md) covers the earlier inspection workflow and container setup.

## 25 November 2026 — Organization and membership storage

Added an `Organization` model and a membership that links an organization to a user identity. The membership uses the identity provider’s issuer and subject together because two providers can issue the same subject value. A composite primary key prevents duplicate memberships within one organization while allowing a user to belong to multiple organizations.

The EF migration adds two tables alongside the inspection tables. Assigning existing records to an organization and enforcing access requires a later change. The API still uses its current access behavior.

Start with `InspectNow.Domain/Organizations`, then read the organization configurations in `InspectNow.Infrastructure/Persistence/Configurations`. `OrganizationModelTests` checks the membership relationship, migration SQL, and model snapshot without connecting to PostgreSQL.

The Release build and all 53 non-integration tests passed. EF reported no pending model changes. `AddOrganizations` was applied to the restored local `inspectnow` database. PostgreSQL confirmed the migration history entry and both new tables, each with zero rows after migration.

## 25 November 2026 — Organization access for templates and inspections

Added JWT authentication and a membership check for the selected organization. `GET /api/organizations` lets a signed-in user discover their memberships. The existing template and inspection endpoints use that selection for reads and writes, so clients cannot choose a different organization by changing a record ID.

The `ScopeInspectionRecords` migration assigns existing records to **Legacy workspace** and requires an organization for new records. A composite foreign key also keeps an inspection and its source template in the same organization at the database level.

The integration suite uses signed tokens and two organizations to exercise permitted requests, rejected membership, and cross-organization reads and writes. See [organization access setup](ORGANIZATION_ACCESS.md) for the provider settings and membership setup.

The Release build and all 67 tests passed against a disposable PostgreSQL 19 instance. Applied `ScopeInspectionRecords` to local `inspectnow` and verified that all existing templates and inspections belong to **Legacy workspace**, with the original records and questions preserved. Local sign-in requires an identity provider and a membership for the intended user.

## 25 November 2026 — React inspection workspace

Added a responsive React app with an overview, searchable inspections, and a checklist library. Users can publish a checklist, start an inspection, save individual answers, and submit when every required question has a Pass or Fail. The UI keeps the API's version token on writes so an older view cannot silently overwrite a newer answer.

The app starts with a browser-local demo so you can try the workflow before configuring sign-in. Connecting with an existing API token switches the app to real organization data. See the [frontend guide](../InspectNow.Web/README.md) for startup and connection details. This batch requires no database migration.

The production build and both frontend tests passed. Browser checks covered publishing a checklist, submitting an inspection, finding it after a reload, and navigating at phone width. API headers and pagination were verified with simulated responses. Live sign-in still requires an account with an organization membership.
