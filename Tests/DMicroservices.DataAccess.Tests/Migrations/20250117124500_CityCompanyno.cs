using Microsoft.EntityFrameworkCore.Migrations;

namespace DMicroservices.DataAccess.Tests.Migrations
{
    public partial class CityCompanyno : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyNo",
                table: "City",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyNo",
                table: "City");
        }
    }
}
