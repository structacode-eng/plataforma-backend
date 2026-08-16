using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plataforma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserProductAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_product_access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Product = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FirstSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LoginCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_product_access", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_product_access_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_product_access_Product_LastSeenAtUtc",
                table: "user_product_access",
                columns: new[] { "Product", "LastSeenAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_user_product_access_UserId_Product",
                table: "user_product_access",
                columns: new[] { "UserId", "Product" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_product_access");
        }
    }
}
