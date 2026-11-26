using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InspectNow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomRolesAndFormAudiences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid[]>(
                name: "CustomRoleIds",
                table: "OrganizationMemberships",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "AudienceRoles",
                table: "Forms",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "PublicKey",
                table: "Forms",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrganizationRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationRoles_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Forms_PublicKey",
                table: "Forms",
                column: "PublicKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationRoles_OrganizationId_NormalizedName",
                table: "OrganizationRoles",
                columns: new[] { "OrganizationId", "NormalizedName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationRoles");

            migrationBuilder.DropIndex(
                name: "IX_Forms_PublicKey",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "CustomRoleIds",
                table: "OrganizationMemberships");

            migrationBuilder.DropColumn(
                name: "AudienceRoles",
                table: "Forms");

            migrationBuilder.DropColumn(
                name: "PublicKey",
                table: "Forms");
        }
    }
}
