using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plataforma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReleaseProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_release_manifests_Channel",
                table: "release_manifests");

            migrationBuilder.AddColumn<string>(
                name: "Product",
                table: "release_manifests",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "revit-plugin");

            migrationBuilder.CreateIndex(
                name: "IX_release_manifests_Product_Channel",
                table: "release_manifests",
                columns: new[] { "Product", "Channel" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_release_manifests_Product_Channel",
                table: "release_manifests");

            migrationBuilder.DropColumn(
                name: "Product",
                table: "release_manifests");

            migrationBuilder.CreateIndex(
                name: "IX_release_manifests_Channel",
                table: "release_manifests",
                column: "Channel");
        }
    }
}
