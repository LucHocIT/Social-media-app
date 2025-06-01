using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialApp.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMediaSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "SimpleMessages",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<long>(
                name: "MediaFileSize",
                table: "SimpleMessages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaFilename",
                table: "SimpleMessages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaMimeType",
                table: "SimpleMessages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaPublicId",
                table: "SimpleMessages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaType",
                table: "SimpleMessages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaUrl",
                table: "SimpleMessages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MessageType",
                table: "SimpleMessages",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MediaFileSize",
                table: "SimpleMessages");

            migrationBuilder.DropColumn(
                name: "MediaFilename",
                table: "SimpleMessages");

            migrationBuilder.DropColumn(
                name: "MediaMimeType",
                table: "SimpleMessages");

            migrationBuilder.DropColumn(
                name: "MediaPublicId",
                table: "SimpleMessages");

            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "SimpleMessages");

            migrationBuilder.DropColumn(
                name: "MediaUrl",
                table: "SimpleMessages");

            migrationBuilder.DropColumn(
                name: "MessageType",
                table: "SimpleMessages");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "SimpleMessages",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }
    }
}
