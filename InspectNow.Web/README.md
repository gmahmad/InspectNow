# InspectNow web app

The React workspace includes an overview, an inspection worklist, and a checklist library. Use it to publish a checklist, start a site inspection, save answers, and submit the result.

## Run from Visual Studio

Open `InspectNow.slnx` and set **InspectNow.Api** as the startup project. Choose **InspectNow Full Stack** in the toolbar and click **Start**. The launcher starts the API and Vite, then redirects the browser to the frontend at `http://127.0.0.1:5176/`. Use the existing **http** or **https** profiles to start just the API. Expand **src → InspectNow.Web → src** to edit React files alongside the API.

On Windows, the launcher can use either Node.js on your PATH or the copy installed with Visual Studio. On a fresh checkout, install the frontend dependencies once before using the launch profile.

## Run Vite separately

Use Node.js 24 and pnpm 11. Open the `InspectNow.Web` folder in Visual Studio or your editor. In its terminal, run `pnpm install` once, then `pnpm dev`. Open the local URL printed by Vite.

The app starts in demo mode. You can complete the entire workflow without starting PostgreSQL or setting up sign-in. Demo records and changes stay in the browser you use.

## Connect to your restored workspace

For real users and workspaces, follow the [team workflow guide](../docs/TEAM_WORKFLOW.md). Use **Sign in** in the frontend to create an InspectNow account. The API token dialog remains available for external identity providers.

1. Start `InspectNow.Api` from Visual Studio with the **https** profile. The frontend dev server proxies `/api` to `https://localhost:7145`. If you use another port or profile, set `INSPECTNOW_API_URL` in `InspectNow.Web/.env.local` and restart Vite.
2. Open **Connect to API** and paste an access token issued for InspectNow. Select an organization from the returned memberships. Tokens stay in memory for the session. See the [organization access setup](../docs/ORGANIZATION_ACCESS.md).
3. Open the inspection list or checklist library to see the organization’s records. Writes go to the API while connected. **Disconnect** switches back to the separate browser demo.

For production, serve the frontend and route `/api` through the same HTTPS origin. The current connection dialog supports manual integration testing. A browser sign-in flow can replace it once the account model is decided.

Use `pnpm test` for the frontend data tests and `pnpm build` for the production bundle.
