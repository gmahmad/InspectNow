# InspectNow forms platform

InspectNow helps businesses replace recurring paper forms with reusable digital forms, a phone-friendly filling experience, and a record library. Inspections are one workflow within that platform. The existing inspection module also handles site assignments, Pass or Fail checks, and corrective actions.

## Use cases

| Workflow | Examples | Often needs |
|---|---|---|
| Inspections and field checks | Equipment checks, vehicle walkarounds, property condition reports, cleaning checks | Site context, evidence, offline capture, corrective actions |
| Surveys and evaluation | Customer feedback, employee pulse checks, event follow-up, service evaluation | Public or private distribution, ratings, optional anonymity, summary charts |
| Tests and assessments | Induction knowledge checks, training assessments, competency evaluations | Answer keys, scoring, attempts, feedback, and reviewer sign-off |
| Intake and registration | Client onboarding, service enrollment, volunteer or event registration | Contact details, conditional sections, consent, and reusable records |
| Internal requests | Maintenance, equipment, procurement, IT access, travel, and reimbursements | Routing, limits, approvals, and status notifications |
| Audits and compliance records | Control reviews, inventory audits, supplier qualification | Immutable revisions, evidence, reviewer notes, retention, and exports |
| Operations and incident records | Shift handovers, incident reports, inventory counts, quality logs | Repeated entries, timestamps, attachments, review and follow-up |

Forms use these categories, with starters for surveys, inspections, assessments, intake, requests, and audits. A category groups records and provides a starting point. Specialized behavior such as assessment grading and request approvals needs its own rules.

## Build and collect a form

1. Start **InspectNow Full Stack** in Visual Studio and open **Forms**.
2. As a manager or workspace admin, choose **New form**. The builder opens on its own page. Pick a category and optionally load a starter. Add sections, choose question types, and mark required answers. On a wide screen, use the form outline to jump to a question.
3. Under **Who can respond?**, choose workspace members, selected roles, or anyone with the link. For selected roles, a member needs at least one of the checked roles and access to the Forms module.
4. Save a draft or publish. Publication locks the questions. Open a published form and expand **Form audience and sharing** to change its audience or copy its public link. Switching away from public sharing revokes that link.
5. Open the published form on a phone-width browser. Signed-in responders can save and resume private drafts. Guests with a public link can submit without signing in and complete their response in one visit.
6. A manager or workspace admin can open **Response library**, filter by form, and open a full-page response grouped by section. **Back to response library** keeps the previous filter and page.

The builder supports short and long text, email, numbers, dates, times, single and multiple choice, yes or no, and a five-point rating. In Demo mode, changes stay in browser storage. Signed-in workspaces store definitions, drafts, and responses in PostgreSQL.

## Set up the platform owner

A platform owner is an existing InspectNow account explicitly selected through deployment configuration. After creating that account, find its `Id` in `AspNetUsers` using a database administrator session. Add `Platform:OwnerUserId` with that ID in **InspectNow.Api → Manage User Secrets** and restart the API. For a deployed instance, supply `Platform__OwnerUserId` through its configuration. A registered email address alone grants no ownership.

The owner creates an organization from the workspace picker, then opens **Team** and creates a one-time **Admin** invitation for the organization’s lead. That person registers or signs in, then redeems the code under **Join workspace**. The lead can send Manager and Inspector invitations and assign custom roles after the invitees join.

## Workspace types and access

| Organization type | What the team sees |
|---|---|
| General business | Form and response libraries, plus the inspection workflow |
| Field operations | Inspection overview, sites, assignments, corrective actions, and forms |
| Education and training | Assessments, course feedback, and the general forms library |
| Client services | Client intake, service requests, and the general forms library |

The platform owner selects a type when creating the workspace. Within that type, its admin can open **Team → Edit access** to choose Manager or Inspector and grant the available modules to each member. The API enforces the same rules as the navigation. Members with open assigned inspections or corrective actions must finish that work before their inspection access is removed.

An organization admin can use **Team → Custom roles** to create roles such as Safety Reviewer or Shift Lead, then use **Assign roles** beside a member to add or remove any combination. Custom roles control response eligibility for selected forms. Built-in roles and module access continue to control administrative actions and response-library access.

## Try the local business demo

Each demo workspace contains an admin account, a member account, a published form, and a submitted response. The Field Operations workspace also has a site and an inspection assignment.

| Workspace | Admin email | Member email |
|---|---|---|
| Demo · Atlas Facilities | admin@atlas.inspectnow.example | member@atlas.inspectnow.example |
| Demo · Cedar Training | admin@cedar.inspectnow.example | member@cedar.inspectnow.example |
| Demo · Harbor Client Services | admin@harbor.inspectnow.example | member@harbor.inspectnow.example |

The shared local demo password is in the ignored `InspectNow.DemoAccounts.local.txt` file at the repository root after local seeding. Run **InspectNow Full Stack** and sign in as an admin to see that workspace’s screens and edit member access, or sign in as a member to fill a form. The demo accounts use fictional addresses and only belong to their sample workspaces.

## Product priorities

1. **Collect and distribute.** Add QR sharing, conditionally relevant questions, file and photo fields, and better navigation for long forms.
2. **Use the records.** Add published revisions, searchable response fields, exports, approvals, assessment scoring, and audit history.
3. **Operate the service.** Add account recovery, retention and deletion controls, backup and restore verification, key management, per-tenant limits, and billing.

Keep the mobile workflow focused on finishing one section at a time. For hosted records, verify encryption, retention, backups, and recovery before promising data protection to customers.

Market references show common expectations for form and quiz creation, sections, branching, analysis, and mobile collection. [Microsoft Forms](https://support.microsoft.com/en-us/forms/what-is-microsoft-forms) · [Microsoft Forms branching](https://support.microsoft.com/en-us/forms/use-branching-logic-in-your-form) · [SafetyCulture mobile checklist example](https://wp-website.safetyculture.com/wp-content/uploads/sites/3/2025/02/OSHA-PPE-Standard-PDF-Checklist.pdf)
