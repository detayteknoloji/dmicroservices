using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DMicroservices.DataAccess.Tests.Migrations
{
    public partial class v5 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateTimeNullable",
                table: "Search",
                type: "datetime(6)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateTimeNullable",
                table: "Search");
        }
    }
}
