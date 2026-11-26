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

## 26 November 2026 — Team onboarding and assigned inspections

Added InspectNow accounts with ASP.NET Core Identity, cookie sessions, and workspace roles. Admins create workspaces and invite teammates through expiring, single-use codes. Managers create sites and assign published checklists. Inspectors open assigned work and submit it using the existing answer workflow.

The React app adds sign-in, a workspace picker, and Team, Sites, and Assignments screens. The PostgreSQL migration adds Identity tables, role data, sites, invitations, and optional assignment fields so existing inspections keep their history. The [team workflow guide](TEAM_WORKFLOW.md) walks through the new screens.

Removed the unused SQL Server provider from the PostgreSQL-only application. It pulled in a cryptography package that NuGet flagged during restore.

The Release build passed without warnings. All 68 backend tests passed against disposable PostgreSQL 19, including the signed-in admin and inspector workflow. All three frontend tests and the production bundle passed, and the registration form opened in the local preview. Applied `AddTeamWorkflow` to local `inspectnow` and verified that the existing workspace, templates, inspections, and questions were preserved.

Added the React source and configuration files to Solution Explorer so the frontend and backend can be edited side by side in Visual Studio. Vite still runs the frontend.

Added the **InspectNow Full Stack** Visual Studio launch profile. It uses the ASP.NET Core SPA proxy to start Vite and open the app from the same Start button as the API. This makes the entire local workflow runnable from Visual Studio.

## 26 November 2026 — Corrective actions

A failed answer can now become a follow-up task after an inspection is submitted. A manager assigns the task with instructions and a due date. The assignee records what was fixed, then a manager closes it or returns it with a note.

The React **Corrective actions** screen shows assigned work and its review status. The API scopes actions to the workspace and limits inspectors to their own tasks. It uses version checks to protect updates from stale browser tabs. The migration adds a separate table so corrective work has its own lifecycle alongside submitted inspections.

The 68 backend tests passed against disposable PostgreSQL, including assignment, review, return for changes, duplicate prevention, and rejection of stale updates. The three frontend tests and production bundle passed. Applied `AddCorrectiveActions` to local `inspectnow` and verified the new table and the existing record counts.

## 26 November 2026 — Forms platform and response library

Added reusable forms for surveys, assessments, inspections, intake, requests, audits, and other paper workflows. A form can have sections and ten answer types. Published definitions are locked so answers keep their original meaning. The phone layout moves through a section at a time and saves progress so respondents can return later.

The response library shows submitted records to workspace managers and admins. Drafts are private to their author, and the API checks workspace membership and answer types on every write. The database stores form definitions and responses separately from the existing inspection workflow.

Workspace provisioning now requires an explicitly configured platform owner. The owner can invite an organization admin, and workspace admins can invite managers and inspectors. The [forms platform guide](FORMS_PLATFORM.md) explains the use cases, setup, and capabilities to build next.

All 68 backend tests and four frontend tests passed. The browser demo verified building a two-section survey, saving and resuming at phone width, submitting, and opening the response. Applied `AddFormLibrary` to local `inspectnow` and checked the new tables and existing record counts.

## Typed workspaces and team access

Added General Business, Field Operations, Education and Training, and Client Services workspaces. The selected type shapes the landing screen and enabled modules. Education opens Assessments and Course Feedback, Client Services opens Intake and Requests, and Field Operations opens the inspection worklist and related tools.

Workspace admins can edit a team member’s role and access to forms or inspections. The API checks these permissions even when a caller goes directly to an endpoint. Assignment and access changes lock the same membership row so a permission change cannot leave newly assigned work inaccessible.

Added a local demo initializer for three sample workspaces with fictional accounts, published forms, and submitted records. The Field Operations sample also has a site and assigned inspection. See the [forms platform guide](FORMS_PLATFORM.md#try-the-local-business-demo) for the sample sign-ins.

Applied `AddWorkspaceTypesAndAccess` to local `inspectnow`. Verified three demo workspaces, six accounts, three forms, three submitted responses, and one assigned inspection. All three local demo admins signed in and could open their response library. The Education and Client Services accounts received a forbidden response from the inspection API.

The Release build, all 68 backend tests, four frontend tests, and production bundle passed. The test suite verifies that revoking inspection access is refused while assigned work is open. It also verifies that forms access can be restored independently and that seeding twice does not duplicate demo records.

## Dedicated workflow pages

Moved form building, filling, and submitted-response review out of dialogs. The desktop builder has a field outline for navigating long forms. The phone view gives the page to the current section, with saved progress and navigation available at the bottom.

Inspection and checklist records, assignment creation, and corrective-action work also open as pages. They have URL routes and explicit back links. The response library keeps the selected filters in the URL, and a signed-in user can refresh in their selected workspace. Dialogs remain for short interactions such as signing in, switching workspaces, and editing member access. This is a frontend change with no database migration.

The production bundle and all five frontend tests passed. Browser checks covered the builder outline, save and resume at phone width, response details after refresh, returning to filtered results, and inspection creation and detail pages.

## Drag-and-drop form builder

Added a palette for every supported question type and section headings. Drag a palette grip to an insertion point or select the field name to append it. Existing fields can be dragged, moved with the arrow buttons, or duplicated with their settings. Moving a section heading changes where its section starts. The builder keeps the field outline for navigating longer forms.

The responsive palette stays available at phone widths. With a grip focused, press Space or Enter to pick up a field, use Up or Down to choose its position, and press Space or Enter to drop it. Escape cancels the move. Preview uses the same input controls as the published form and lets authors try answers before saving or publishing.

Verified pointer insertion, keyboard insertion and reordering, duplication, preview, and saved field order in the local demo. Checked that the palette fits and fields can be added at phone width. The production bundle and all five frontend tests passed. No database migration was required.

## Custom roles and form audiences

Organization admins can create custom roles in Team and assign multiple roles to existing members. Custom roles control form response eligibility. Built-in roles and module access continue to govern workspace administration and access to the response library.

Form authors can allow everyone with Forms access, require at least one selected role, or share a public link that accepts submissions without sign-in. Authors can change the audience after publishing. Switching to an internal audience revokes the public link. The API enforces the audience on form browsing, direct access, draft saving, and submission. Guests complete responses in one visit.

Applied `AddCustomRolesAndFormAudiences` to local `inspectnow`. Existing forms keep their workspace audience. All 68 backend tests passed in an isolated PostgreSQL instance, including custom role assignment, access by one of multiple roles, organization isolation, anonymous submissions, and link revocation. All five frontend tests and the production bundle passed.
