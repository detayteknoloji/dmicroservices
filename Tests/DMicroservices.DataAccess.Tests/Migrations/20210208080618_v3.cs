using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DMicroservices.DataAccess.Tests.Migrations
{
    public partial class v3 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Search",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IntValue = table.Column<int>(type: "int", nullable: false),
                    IntNullableValue = table.Column<int>(type: "int", nullable: true),
                    StringValue = table.Column<string>(type: "longtext CHARACTER SET utf8mb4", nullable: true),
                    DecimalValue = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SmallIntValue = table.Column<short>(type: "smallint", nullable: false),
                    BigIntValue = table.Column<long>(type: "bigint", nullable: false),
                    ByteValue = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    DateTimeValue = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    BoolValue = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DoubleValue = table.Column<double>(type: "double", nullable: false),
                    EnumValue = table.Column<short>(type: "smallint", nullable: false),
                    GuidValue = table.Column<Guid>(type: "char(36)", nullable: false),
                    GuidNullableValue = table.Column<Guid>(type: "char(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Search", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Search");
        }
    }
}
