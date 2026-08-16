using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plataforma.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UsageDaily : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "usage_daily",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Product = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Command = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_daily", x => x.Id);
                    table.ForeignKey(
                        name: "FK_usage_daily_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_usage_daily_Day_Product",
                table: "usage_daily",
                columns: new[] { "Day", "Product" });

            migrationBuilder.CreateIndex(
                name: "IX_usage_daily_UserId_Product_Command_Day",
                table: "usage_daily",
                columns: new[] { "UserId", "Product", "Command", "Day" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "usage_daily");
        }
    }
}
