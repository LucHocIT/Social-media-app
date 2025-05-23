using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialApp.Migrations
{
    /// <summary>
    /// Migration to add columns for media type and public ID to the Post table
    /// </summary>
    public partial class AddPostMediaColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MediaType",
                table: "Posts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
                
            migrationBuilder.AddColumn<string>(
                name: "MediaPublicId",
                table: "Posts",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "Posts");
                
            migrationBuilder.DropColumn(
                name: "MediaPublicId",
                table: "Posts");
        }
    }
}
