using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InspectNow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceTypesAndAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Organizations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AllowedModules",
                table: "OrganizationMemberships",
                type: "integer",
                nullable: false,
                defaultValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                LOCK TABLE "Organizations", "OrganizationMemberships" IN ACCESS EXCLUSIVE MODE;
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Organizations" WHERE "Type" <> 0)
                       OR EXISTS (SELECT 1 FROM "OrganizationMemberships" WHERE "AllowedModules" <> 3) THEN
                        RAISE EXCEPTION 'Cannot remove workspace types while types or access restrictions are in use.';
                    END IF;
                END;
                $guard$;
                """);

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "AllowedModules",
                table: "OrganizationMemberships");
        }
    }
}
