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

    public virtual DbSet<Like> Likes { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Post> Posts { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserFollower> UserFollowers { get; set; }

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

        modelBuilder.Entity<Like>(entity =>
        {
            entity.HasIndex(e => e.CommentId, "IX_Likes_CommentId");

            entity.HasIndex(e => e.PostId, "IX_Likes_PostId");

            entity.HasIndex(e => e.UserId, "IX_Likes_UserId");

            entity.HasOne(d => d.Comment).WithMany(p => p.Likes).HasForeignKey(d => d.CommentId);

            entity.HasOne(d => d.Post).WithMany(p => p.Likes).HasForeignKey(d => d.PostId);

            entity.HasOne(d => d.User).WithMany(p => p.Likes).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasIndex(e => e.ReceiverId, "IX_Messages_ReceiverId");

            entity.HasIndex(e => e.SenderId, "IX_Messages_SenderId");

            entity.Property(e => e.Content).HasMaxLength(1000);

            entity.HasOne(d => d.Receiver).WithMany(p => p.MessageReceivers)
                .HasForeignKey(d => d.ReceiverId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Sender).WithMany(p => p.MessageSenders)
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

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
        });

        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_Posts_UserId");

            entity.Property(e => e.Content).HasMaxLength(500);

            entity.HasOne(d => d.User).WithMany(p => p.Posts).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email, "IX_Users_Email").IsUnique();

            entity.HasIndex(e => e.Username, "IX_Users_Username").IsUnique();

            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Username).HasMaxLength(50);
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

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
