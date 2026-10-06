using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Beacon.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "deleted_by_oid",
                table: "links",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_on_utc",
                table: "links",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_links_deleted_on_utc",
                table: "links",
                column: "deleted_on_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_links_deleted_on_utc",
                table: "links");

            migrationBuilder.DropColumn(
                name: "deleted_by_oid",
                table: "links");

            migrationBuilder.DropColumn(
                name: "deleted_on_utc",
                table: "links");
        }
    }
}
