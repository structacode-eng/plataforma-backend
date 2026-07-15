using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plataforma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReleaseChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "release_manifests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "stable");

            migrationBuilder.CreateIndex(
                name: "IX_release_manifests_Channel",
                table: "release_manifests",
                column: "Channel");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_release_manifests_Channel",
                table: "release_manifests");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "release_manifests");
        }
    }
}
