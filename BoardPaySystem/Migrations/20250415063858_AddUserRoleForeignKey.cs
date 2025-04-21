using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardPaySystem.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRoleForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoleId1",
                table: "users",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_RoleId1",
                table: "users",
                column: "RoleId1");

            migrationBuilder.AddForeignKey(
                name: "FK_users_Roles_RoleId1",
                table: "users",
                column: "RoleId1",
                principalTable: "Roles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_Roles_RoleId1",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_RoleId1",
                table: "users");

            migrationBuilder.DropColumn(
                name: "RoleId1",
                table: "users");
        }
    }
}
