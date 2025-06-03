using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialApp.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePostPrivacyFromBooleanToLevel : Migration
    {        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Thêm cột PrivacyLevel mới
            migrationBuilder.AddColumn<int>(
                name: "PrivacyLevel",
                table: "Posts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Migrate dữ liệu từ IsPrivate sang PrivacyLevel
            // IsPrivate = false (0) -> PrivacyLevel = 0 (Public)
            // IsPrivate = true (1) -> PrivacyLevel = 1 (Private)
            migrationBuilder.Sql(@"
                UPDATE Posts 
                SET PrivacyLevel = CASE 
                    WHEN IsPrivate = 1 THEN 1 
                    ELSE 0 
                END");

            // Xóa cột IsPrivate cũ
            migrationBuilder.DropColumn(
                name: "IsPrivate",
                table: "Posts");
        }        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Thêm lại cột IsPrivate
            migrationBuilder.AddColumn<bool>(
                name: "IsPrivate",
                table: "Posts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Migrate dữ liệu ngược lại từ PrivacyLevel sang IsPrivate
            // PrivacyLevel = 0 (Public) -> IsPrivate = false
            // PrivacyLevel = 1,2 (Private/Secret) -> IsPrivate = true
            migrationBuilder.Sql(@"
                UPDATE Posts 
                SET IsPrivate = CASE 
                    WHEN PrivacyLevel > 0 THEN 1 
                    ELSE 0 
                END");

            // Xóa cột PrivacyLevel
            migrationBuilder.DropColumn(
                name: "PrivacyLevel",
                table: "Posts");
        }
    }
}
