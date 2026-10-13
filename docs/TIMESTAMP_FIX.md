# Timestamp precision correction

Changed source files:
- InspectNow.Domain/Templates/Inspection.cs
- InspectNow.Tests/Inspection/InspectionSubmissionTests.cs
- InspectNow.Tests/Inspection/InspectionTests.cs

Changed documentation: WORKFLOW_UPDATE.md and this note.

Apply the full package at the solution root, or replace only those three source
files. No migrations, dependencies, secrets or database records changed.
Rebuild and rerun all tests. Keep the exact equality assertion in
InspectionWorkflowTests: the fix makes immediate and persisted responses agree.

Validation: arithmetic checked for tick remainders 0 through 9. All earlier
migration bytes and integration assertions remain unchanged. No .NET SDK or
PostgreSQL server is available here, so no C# test execution is claimed.
