using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESD.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EsdEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeviceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EmployeeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RawData = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EsdEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EsdEvents_Device_EventType",
                table: "EsdEvents",
                columns: new[] { "DeviceName", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_EsdEvents_EmployeeId",
                table: "EsdEvents",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EsdEvents_EventTime",
                table: "EsdEvents",
                column: "EventTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EsdEvents");
        }
    }
}
