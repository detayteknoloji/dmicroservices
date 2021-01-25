using Microsoft.EntityFrameworkCore.Migrations;

namespace DMicroservices.DataAccess.Tests.Migrations
{
    public partial class v2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "City",
                type: "varchar(5) CHARACTER SET utf8mb4",
                maxLength: 5,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext CHARACTER SET utf8mb4",
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "City",
                type: "longtext CHARACTER SET utf8mb4",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(5) CHARACTER SET utf8mb4",
                oldMaxLength: 5,
                oldNullable: true);
        }
    }
}
