using Microsoft.EntityFrameworkCore.Migrations;

namespace TslWebApp.Migrations
{
    public partial class SmsMessagemodelchanged : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "Messages",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string));

            migrationBuilder.AddColumn<int>(
                name: "DriverId",
                table: "Messages",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DriverId",
                table: "Messages");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "Messages",
                nullable: false,
                oldClrType: typeof(string),
                oldMaxLength: 1024);
        }
    }
}
