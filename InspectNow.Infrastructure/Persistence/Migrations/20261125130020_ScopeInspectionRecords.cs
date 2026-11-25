using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InspectNow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScopeInspectionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inspections_InspectionTemplates_TemplateId",
                table: "Inspections");

            migrationBuilder.DropIndex(
                name: "IX_Inspections_TemplateId",
                table: "Inspections");

            migrationBuilder.AddColumn<long>(
                name: "OrganizationId",
                table: "InspectionTemplates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "OrganizationId",
                table: "Inspections",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql(
                """
                DO $backfill$
                DECLARE
                    legacy_organization_id bigint;
                BEGIN
                    IF EXISTS (SELECT 1 FROM "InspectionTemplates") THEN
                        INSERT INTO "Organizations" ("Name") VALUES ('Legacy workspace')
                        RETURNING "Id" INTO legacy_organization_id;

                        UPDATE "InspectionTemplates" SET "OrganizationId" = legacy_organization_id;
                        UPDATE "Inspections" SET "OrganizationId" = legacy_organization_id;
                    END IF;
                END;
                $backfill$;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "OrganizationId", table: "InspectionTemplates", type: "bigint", nullable: false,
                oldClrType: typeof(long), oldType: "bigint", oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "OrganizationId", table: "Inspections", type: "bigint", nullable: false,
                oldClrType: typeof(long), oldType: "bigint", oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_InspectionTemplates_OrganizationId_Id",
                table: "InspectionTemplates",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Inspections_OrganizationId_TemplateId",
                table: "Inspections",
                columns: new[] { "OrganizationId", "TemplateId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Inspections_InspectionTemplates_OrganizationId_TemplateId",
                table: "Inspections",
                columns: new[] { "OrganizationId", "TemplateId" },
                principalTable: "InspectionTemplates",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Inspections_Organizations_OrganizationId",
                table: "Inspections",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InspectionTemplates_Organizations_OrganizationId",
                table: "InspectionTemplates",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                LOCK TABLE "InspectionTemplates", "Inspections" IN ACCESS EXCLUSIVE MODE;
                DO $guard$
                BEGIN
                    IF (SELECT count(DISTINCT "OrganizationId") FROM "InspectionTemplates") > 1 THEN
                        RAISE EXCEPTION 'Cannot remove organization isolation while multiple organizations own inspection records.';
                    END IF;
                END;
                $guard$;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Inspections_InspectionTemplates_OrganizationId_TemplateId",
                table: "Inspections");

            migrationBuilder.DropForeignKey(
                name: "FK_Inspections_Organizations_OrganizationId",
                table: "Inspections");

            migrationBuilder.DropForeignKey(
                name: "FK_InspectionTemplates_Organizations_OrganizationId",
                table: "InspectionTemplates");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_InspectionTemplates_OrganizationId_Id",
                table: "InspectionTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Inspections_OrganizationId_TemplateId",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Inspections");

            migrationBuilder.CreateIndex(
                name: "IX_Inspections_TemplateId",
                table: "Inspections",
                column: "TemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Inspections_InspectionTemplates_TemplateId",
                table: "Inspections",
                column: "TemplateId",
                principalTable: "InspectionTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
