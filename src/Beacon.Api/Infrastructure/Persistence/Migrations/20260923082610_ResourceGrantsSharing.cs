using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beacon.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResourceGrantsSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workspace_members");

            migrationBuilder.AddColumn<string>(
                name: "created_by_oid",
                table: "folders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "resource_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grantee_oid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    grantee_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    grantee_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    granted_by_oid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_grants", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_workspaces_created_by_oid",
                table: "workspaces",
                column: "created_by_oid");

            migrationBuilder.CreateIndex(
                name: "ix_resource_grants_grantee_oid",
                table: "resource_grants",
                column: "grantee_oid");

            migrationBuilder.CreateIndex(
                name: "ix_resource_grants_resource_type_resource_id_grantee_oid",
                table: "resource_grants",
                columns: new[] { "resource_type", "resource_id", "grantee_oid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resource_grants");

            migrationBuilder.DropIndex(
                name: "ix_workspaces_created_by_oid",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "created_by_oid",
                table: "folders");

            migrationBuilder.CreateTable(
                name: "workspace_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    user_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    user_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    user_oid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_workspace_members_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_workspace_members_workspace_id_user_oid",
                table: "workspace_members",
                columns: new[] { "workspace_id", "user_oid" },
                unique: true);
        }
    }
}
