# InspectNow team workflow

Use the React app and the ASP.NET Core API together to assign an inspection to a real account. The app keeps the demo for exploring the interface.

## Start a workspace

1. In Visual Studio, set **InspectNow.Api** as the startup project and choose **InspectNow Full Stack** next to the Start button. When the React app opens, select **Sign in → Create an account**.
2. Enter an email and password. The platform owner creates your workspace and can invite its first **Admin**. See the [platform owner setup](FORMS_PLATFORM.md#set-up-the-platform-owner).
3. As workspace admin, open **Team**, select **Inspector** or **Manager**, and create a one-time invitation. Share the code privately. It expires after 48 hours.
4. The teammate creates their own account in a separate browser session and chooses **Join workspace**. They paste the code to gain the role you selected.

## Assign and complete an inspection

1. As an admin or manager, open **Sites** and add a location. Create a checklist with at least one question and publish it.
2. Open **Assignments → New assignment**. Select the site, checklist, inspector, and due date.
3. As the inspector, open **Assignments**. Select your inspection, save a Pass or Fail for each required question, and submit.
4. As the admin or manager, reopen **Assignments** to review the submitted status and answers.

The inspector's inspection list is limited to records assigned to their account. The API checks roles and workspace membership on every request. Administrators can invite members, while managers and administrators can prepare and assign work.

## Follow up on a failed answer

1. As a manager or admin, open a submitted inspection and choose **Assign corrective action** under a failed answer. Select an inspector and a due date, and describe the work.
2. As the assignee, open **Corrective actions**, select the task, describe what was fixed, and choose **Send for review**.
3. As a manager or admin, open the action and choose **Approve and close** or explain what needs to change and return it.

Each failed answer can have one corrective action. Returning it reopens the same task. Its status is separate from the original inspection and its answers.

For records already in **Legacy workspace**, a database operator can grant a verified local account membership by linking its `AspNetUsers.Id` as `OrganizationMemberships.Subject`, using issuer `inspectnow-local` and the existing workspace ID. Role values are Manager (0), Admin (1), and Inspector (2). Registering an account never claims an existing workspace.

The current onboarding uses shareable codes. Email delivery, email verification, password recovery, and post-join membership administration are outside this batch. Deploy the frontend and API behind the same HTTPS origin with durable ASP.NET Core Data Protection keys before onboarding external users.
