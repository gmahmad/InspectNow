using InspectNow.Domain.Inspections;
using InspectNow.Domain.Templates;

namespace InspectNow.Tests.Support;

internal static class InspectionFixture
{
    internal static Inspection Create()
    {
        var template = new InspectionTemplate("Office inspection");
        var required = template.AddQuestion("Are the floors clean?", true);
        var optional = template.AddQuestion("Is the carpet clean?", false);
        PersistedEntityFixture.AssignId(template, 101);
        PersistedEntityFixture.AssignId(required, 201);
        PersistedEntityFixture.AssignId(optional, 202);
        template.Publish();
        var inspection = Inspection.Start(template, "Lahore",
            new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero));
        PersistedEntityFixture.AssignId(inspection, 301);
        PersistedEntityFixture.AssignId(inspection.Questions[0], 401);
        PersistedEntityFixture.AssignId(inspection.Questions[1], 402);
        return inspection;
    }
}
