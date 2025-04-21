using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardPaySystem.Migrations
{
    /// <inheritdoc />
    public partial class FixedUserRoleFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_Roles_RoleId",
                table: "users");

            migrationBuilder.DropForeignKey(
                name: "FK_users_Roles_RoleId1",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_RoleId1",
                table: "users");

            migrationBuilder.DropColumn(
                name: "RoleId1",
                table: "users");

            migrationBuilder.RenameColumn(
                name: "RoleId",
                table: "users",
                newName: "roleID");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "users",
                newName: "username");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "users",
                newName: "userID");

            migrationBuilder.RenameIndex(
                name: "IX_users_RoleId",
                table: "users",
                newName: "IX_users_roleID");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Roles",
                newName: "roleID");

            migrationBuilder.AddForeignKey(
                name: "FK_users_Roles_roleID",
                table: "users",
                column: "roleID",
                principalTable: "Roles",
                principalColumn: "roleID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_Roles_roleID",
                table: "users");

            migrationBuilder.RenameColumn(
                name: "roleID",
                table: "users",
                newName: "RoleId");

            migrationBuilder.RenameColumn(
                name: "username",
                table: "users",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "userID",
                table: "users",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_users_roleID",
                table: "users",
                newName: "IX_users_RoleId");

            migrationBuilder.RenameColumn(
                name: "roleID",
                table: "Roles",
                newName: "Id");

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
                name: "FK_users_Roles_RoleId",
                table: "users",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_users_Roles_RoleId1",
                table: "users",
                column: "RoleId1",
                principalTable: "Roles",
                principalColumn: "Id");
        }
    }
}
