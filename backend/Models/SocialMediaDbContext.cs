using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace SocialApp.Models;

public partial class SocialMediaDbContext : DbContext
{
    public SocialMediaDbContext(DbContextOptions<SocialMediaDbContext> options)
        : base(options)
    {
    }
    public virtual DbSet<Comment> Comments { get; set; }

    public virtual DbSet<CommentReport> CommentReports { get; set; }
    public virtual DbSet<Reaction> Reactions { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }
    public virtual DbSet<Post> Posts { get; set; }

    public virtual DbSet<PostMedia> PostMedias { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserFollower> UserFollowers { get; set; }    public virtual DbSet<EmailVerificationCode> EmailVerificationCodes { get; set; }

    // Simple Chat models
    public virtual DbSet<ChatConversation> ChatConversations { get; set; }
    public virtual DbSet<SimpleMessage> SimpleMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasIndex(e => e.ParentCommentId, "IX_Comments_ParentCommentId");

            entity.HasIndex(e => e.PostId, "IX_Comments_PostId");

            entity.HasIndex(e => e.UserId, "IX_Comments_UserId");

            entity.Property(e => e.Content).HasMaxLength(300);

            entity.HasOne(d => d.ParentComment).WithMany(p => p.InverseParentComment).HasForeignKey(d => d.ParentCommentId);

            entity.HasOne(d => d.Post).WithMany(p => p.Comments).HasForeignKey(d => d.PostId);

            entity.HasOne(d => d.User).WithMany(p => p.Comments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Reaction>(entity =>
        {
            entity.HasIndex(e => e.PostId, "IX_Reactions_PostId");

            entity.HasIndex(e => e.UserId, "IX_Reactions_UserId");

            entity.Property(e => e.ReactionType).HasMaxLength(20).HasDefaultValue("like");

            entity.HasOne(d => d.Post).WithMany(p => p.Reactions).HasForeignKey(d => d.PostId);

            entity.HasOne(d => d.User).WithMany(p => p.Reactions).HasForeignKey(d => d.UserId);        }); 
        
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(e => e.CommentId, "IX_Notifications_CommentId");

            entity.HasIndex(e => e.FromUserId, "IX_Notifications_FromUserId");

            entity.HasIndex(e => e.PostId, "IX_Notifications_PostId");

            entity.HasIndex(e => e.UserId, "IX_Notifications_UserId");

            entity.Property(e => e.Content).HasMaxLength(300);

            entity.HasOne(d => d.Comment).WithMany(p => p.Notifications).HasForeignKey(d => d.CommentId);

            entity.HasOne(d => d.FromUser).WithMany(p => p.NotificationFromUsers).HasForeignKey(d => d.FromUserId);

            entity.HasOne(d => d.Post).WithMany(p => p.Notifications).HasForeignKey(d => d.PostId);

            entity.HasOne(d => d.User).WithMany(p => p.NotificationUsers).HasForeignKey(d => d.UserId);
        }); modelBuilder.Entity<Post>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_Posts_UserId");

            entity.Property(e => e.Content).HasMaxLength(500);

            entity.HasOne(d => d.User).WithMany(p => p.Posts).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<PostMedia>(entity =>
        {
            entity.HasIndex(e => e.PostId, "IX_PostMedias_PostId");

            entity.Property(e => e.MediaType).HasMaxLength(20);
            entity.Property(e => e.MediaMimeType).HasMaxLength(100);
            entity.Property(e => e.MediaFilename).HasMaxLength(255);

            entity.HasOne(d => d.Post).WithMany(p => p.MediaFiles)
                .HasForeignKey(d => d.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email, "IX_Users_Email").IsUnique();

            entity.HasIndex(e => e.Username, "IX_Users_Username").IsUnique();

            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Username).HasMaxLength(50);
            entity.Property(e => e.Role).HasMaxLength(20).HasDefaultValue("User");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
        });

        modelBuilder.Entity<UserFollower>(entity =>
        {
            entity.HasIndex(e => e.FollowerId, "IX_UserFollowers_FollowerId");

            entity.HasIndex(e => e.FollowingId, "IX_UserFollowers_FollowingId");

            entity.HasOne(d => d.Follower).WithMany(p => p.UserFollowerFollowers)
                .HasForeignKey(d => d.FollowerId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Following).WithMany(p => p.UserFollowerFollowings)
                .HasForeignKey(d => d.FollowingId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<CommentReport>(entity =>
        {
            entity.HasIndex(e => e.CommentId, "IX_CommentReports_CommentId");

            entity.HasIndex(e => e.ReporterId, "IX_CommentReports_ReporterId");

            entity.Property(e => e.Reason).HasMaxLength(300);
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Pending");
            entity.HasOne(d => d.Comment).WithMany(p => p.CommentReports)
                .HasForeignKey(d => d.CommentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Reporter).WithMany(p => p.CommentReports)
                .HasForeignKey(d => d.ReporterId)                .OnDelete(DeleteBehavior.ClientSetNull);        });

        // Configure Simple Chat models
        modelBuilder.Entity<ChatConversation>(entity =>
        {
            entity.HasIndex(e => new { e.User1Id, e.User2Id }, "IX_ChatConversations_User1Id_User2Id").IsUnique();
            entity.HasIndex(e => e.LastMessageTime, "IX_ChatConversations_LastMessageTime");
            
            entity.HasOne(d => d.User1).WithMany()
                .HasForeignKey(d => d.User1Id)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.User2).WithMany()
                .HasForeignKey(d => d.User2Id)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.LastMessageSender).WithMany()
                .HasForeignKey(d => d.LastMessageSenderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SimpleMessage>(entity =>
        {
            entity.HasIndex(e => e.ConversationId, "IX_SimpleMessages_ConversationId");
            entity.HasIndex(e => e.SenderId, "IX_SimpleMessages_SenderId");
            entity.HasIndex(e => e.SentAt, "IX_SimpleMessages_SentAt");
            entity.HasIndex(e => new { e.ConversationId, e.SentAt }, "IX_SimpleMessages_ConversationId_SentAt");

            entity.HasOne(d => d.Conversation).WithMany(p => p.Messages)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Sender).WithMany()
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.ClientSetNull);            entity.HasOne(d => d.ReplyToMessage).WithMany()
                .HasForeignKey(d => d.ReplyToMessageId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
