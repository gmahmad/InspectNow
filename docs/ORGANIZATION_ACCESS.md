# Organization access setup

This guide covers external JWT access. To sign in with an InspectNow-managed account in the React app, use the [team workflow guide](TEAM_WORKFLOW.md).

InspectNow accepts signed access tokens from an OpenID Connect provider. Configure an API audience and a client that requests an access token for that audience. The API verifies the token’s signature, issuer, audience, and expiry.

## Configure the API

In Visual Studio, right-click **InspectNow.Api → Manage User Secrets**. Add `Authentication:Authority` with the provider’s HTTPS authority URL and `Authentication:Audience` with the API audience configured at the provider. Keep the existing database connection settings. For Compose, set `INSPECTNOW_AUTHORITY` and `INSPECTNOW_AUDIENCE` in the environment that starts Compose.

## Grant access

Use pgAdmin on `inspectnow`. Apply any pending migrations before provisioning a membership. The `ScopeInspectionRecords` migration places existing templates and inspections in **Legacy workspace** if there are records to preserve. For a fresh database, insert an organization name in `public.Organizations` using **View/Edit Data → All Rows** and let PostgreSQL generate its ID.

In `public.OrganizationMemberships`, add a row with that `OrganizationId` and the exact `Issuer` and `Subject` values from the intended user’s verified identity (`iss` and `sub`). Use the identity provider’s administrative view to confirm the identity before granting access. Users without memberships have no access to inspection records.

## Verify in Visual Studio

In **Manage User Secrets**, set `HttpClient:AccessToken` to the access token. Start the API with the **https** profile and open `InspectNow.Api.http`. Select **local** in the environment dropdown and send **List my organizations**. Copy one of the returned IDs into `HttpClient:OrganizationId` in User Secrets. The template and inspection requests read the token and ID from there. Refresh the token there when it expires. [Visual Studio HTTP editor and User Secrets](https://learn.microsoft.com/en-us/aspnet/core/test/http-files?view=aspnetcore-10.0#aspnet-core-user-secrets)

Expect `401` without a valid token, `403` without a valid organization selection or membership, and `404` when a record ID belongs to another organization. The health endpoints are available without a token.
