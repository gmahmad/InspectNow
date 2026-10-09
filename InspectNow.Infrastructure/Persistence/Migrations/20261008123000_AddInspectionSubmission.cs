using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InspectNow.Infrastructure.Persistence.Migrations;

public partial class AddInspectionSubmission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "SubmittedAtUtc",
            table: "Inspections",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Refuse to discard submission history or leave Submitted rows without a timestamp.
        migrationBuilder.Sql(
            """
            LOCK TABLE public."Inspections" IN ACCESS EXCLUSIVE MODE;
            DO $guard$
            BEGIN
                IF EXISTS (SELECT 1 FROM public."Inspections"
                           WHERE "SubmittedAtUtc" IS NOT NULL OR "Status" = 'Submitted') THEN
                    RAISE EXCEPTION 'Cannot remove submission support while submitted inspections exist.';
                END IF;
            END;
            $guard$;
            """);
        migrationBuilder.DropColumn(name: "SubmittedAtUtc", table: "Inspections");
    }
}
