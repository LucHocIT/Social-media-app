using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialApp.Migrations
{
    /// <inheritdoc />
    public partial class AddChatFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatConversations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    User1Id = table.Column<int>(type: "int", nullable: false),
                    User2Id = table.Column<int>(type: "int", nullable: false),
                    LastMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastMessageTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastMessageSenderId = table.Column<int>(type: "int", nullable: true),
                    MessageCount = table.Column<int>(type: "int", nullable: false),
                    User1LastRead = table.Column<DateTime>(type: "datetime2", nullable: true),
                    User2LastRead = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsUser1Active = table.Column<bool>(type: "bit", nullable: false),
                    IsUser2Active = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatConversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatConversations_Users_LastMessageSenderId",
                        column: x => x.LastMessageSenderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChatConversations_Users_User1Id",
                        column: x => x.User1Id,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChatConversations_Users_User2Id",
                        column: x => x.User2Id,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SimpleMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<int>(type: "int", nullable: false),
                    SenderId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ReplyToMessageId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimpleMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SimpleMessages_ChatConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ChatConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SimpleMessages_SimpleMessages_ReplyToMessageId",
                        column: x => x.ReplyToMessageId,
                        principalTable: "SimpleMessages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SimpleMessages_Users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_LastMessageSenderId",
                table: "ChatConversations",
                column: "LastMessageSenderId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_LastMessageTime",
                table: "ChatConversations",
                column: "LastMessageTime");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_User1Id_User2Id",
                table: "ChatConversations",
                columns: new[] { "User1Id", "User2Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_User2Id",
                table: "ChatConversations",
                column: "User2Id");

            migrationBuilder.CreateIndex(
                name: "IX_SimpleMessages_ConversationId",
                table: "SimpleMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_SimpleMessages_ConversationId_SentAt",
                table: "SimpleMessages",
                columns: new[] { "ConversationId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SimpleMessages_ReplyToMessageId",
                table: "SimpleMessages",
                column: "ReplyToMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_SimpleMessages_SenderId",
                table: "SimpleMessages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_SimpleMessages_SentAt",
                table: "SimpleMessages",
                column: "SentAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SimpleMessages");

            migrationBuilder.DropTable(
                name: "ChatConversations");
        }
    }
}
