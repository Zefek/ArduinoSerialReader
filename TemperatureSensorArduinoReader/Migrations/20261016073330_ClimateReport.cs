using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TemperatureSensorArduinoReader.Migrations
{
    /// <inheritdoc />
    public partial class ClimateReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClimateDailyMetrics",
                columns: table => new
                {
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Room = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Metric = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClimateDailyMetrics", x => new { x.Day, x.Room, x.Metric });
                });

            migrationBuilder.CreateTable(
                name: "ClimateDailyReports",
                columns: table => new
                {
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Facts = table.Column<string>(type: "jsonb", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    CommentModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CommentTokensIn = table.Column<int>(type: "integer", nullable: true),
                    CommentTokensOut = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClimateDailyReports", x => x.Day);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClimateDailyMetrics");

            migrationBuilder.DropTable(
                name: "ClimateDailyReports");
        }
    }
}
