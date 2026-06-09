using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using UrbanService.DAL.Entities;

namespace UrbanService.DAL.Data;

public partial class UrbanServiceDbContext : DbContext
{
    public UrbanServiceDbContext(DbContextOptions<UrbanServiceDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AiConversation> AiConversations { get; set; }

    public virtual DbSet<AiKnowledgeSource> AiKnowledgeSources { get; set; }

    public virtual DbSet<AiMessage> AiMessages { get; set; }

    public virtual DbSet<AnalysisResult> AnalysisResults { get; set; }

    public virtual DbSet<Channel> Channels { get; set; }

    public virtual DbSet<Feedback> Feedbacks { get; set; }

    public virtual DbSet<FeedbackAssignment> FeedbackAssignments { get; set; }

    public virtual DbSet<FeedbackAttachment> FeedbackAttachments { get; set; }

    public virtual DbSet<FeedbackComment> FeedbackComments { get; set; }

    public virtual DbSet<FeedbackResolution> FeedbackResolutions { get; set; }

    public virtual DbSet<FeedbackResolutionAttachment> FeedbackResolutionAttachments { get; set; }

    public virtual DbSet<FeedbackResolutionReview> FeedbackResolutionReviews { get; set; }

    public virtual DbSet<FeedbackStatusHistory> FeedbackStatusHistories { get; set; }

    public virtual DbSet<FeedbackSupport> FeedbackSupports { get; set; }

    public virtual DbSet<InteractionMessage> InteractionMessages { get; set; }

    public virtual DbSet<MessageAttachment> MessageAttachments { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Service> Services { get; set; }

    public virtual DbSet<ServiceOperator> ServiceOperators { get; set; }

    public virtual DbSet<ServicePayment> ServicePayments { get; set; }

    public virtual DbSet<UrbanServiceCategory> UrbanServiceCategories { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiConversation>(entity =>
        {
            entity.HasKey(e => e.AiConversationId).HasName("ai_conversations_pkey");

            entity.ToTable("ai_conversations");

            entity.Property(e => e.AiConversationId).HasColumnName("ai_conversation_id");
            entity.Property(e => e.EndedAt).HasColumnName("ended_at");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.StartedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("started_at");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Active'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Feedback).WithMany(p => p.AiConversations)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_ai_conversation_feedback");

            entity.HasOne(d => d.User).WithMany(p => p.AiConversations)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_ai_conversation_user");
        });

        modelBuilder.Entity<AiKnowledgeSource>(entity =>
        {
            entity.HasKey(e => e.KnowledgeSourceId).HasName("ai_knowledge_sources_pkey");

            entity.ToTable("ai_knowledge_sources");

            entity.Property(e => e.KnowledgeSourceId).HasColumnName("knowledge_source_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .HasColumnName("file_url");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.SourceType)
                .HasMaxLength(50)
                .HasColumnName("source_type");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Category).WithMany(p => p.AiKnowledgeSources)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_knowledge_source_category");
        });

        modelBuilder.Entity<AiMessage>(entity =>
        {
            entity.HasKey(e => e.AiMessageId).HasName("ai_messages_pkey");

            entity.ToTable("ai_messages");

            entity.Property(e => e.AiMessageId).HasColumnName("ai_message_id");
            entity.Property(e => e.AiConversationId).HasColumnName("ai_conversation_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.MessageText).HasColumnName("message_text");
            entity.Property(e => e.SenderType)
                .HasMaxLength(50)
                .HasColumnName("sender_type");

            entity.HasOne(d => d.AiConversation).WithMany(p => p.AiMessages)
                .HasForeignKey(d => d.AiConversationId)
                .HasConstraintName("fk_ai_message_conversation");
        });

        modelBuilder.Entity<AnalysisResult>(entity =>
        {
            entity.HasKey(e => e.AnalysisResultId).HasName("analysis_results_pkey");

            entity.ToTable("analysis_results");

            entity.Property(e => e.AnalysisResultId).HasColumnName("analysis_result_id");
            entity.Property(e => e.ConfidenceScore)
                .HasPrecision(5, 4)
                .HasColumnName("confidence_score");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DetectedCategoryId).HasColumnName("detected_category_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.Keywords)
                .HasMaxLength(500)
                .HasColumnName("keywords");
            entity.Property(e => e.ModelName)
                .HasMaxLength(100)
                .HasColumnName("model_name");
            entity.Property(e => e.RawResponse)
                .HasColumnType("jsonb")
                .HasColumnName("raw_response");
            entity.Property(e => e.Sentiment)
                .HasMaxLength(50)
                .HasColumnName("sentiment");
            entity.Property(e => e.Summary)
                .HasMaxLength(500)
                .HasColumnName("summary");
            entity.Property(e => e.UrgencyLevel)
                .HasMaxLength(50)
                .HasColumnName("urgency_level");

            entity.HasOne(d => d.DetectedCategory).WithMany(p => p.AnalysisResults)
                .HasForeignKey(d => d.DetectedCategoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_analysis_result_category");

            entity.HasOne(d => d.Feedback).WithMany(p => p.AnalysisResults)
                .HasForeignKey(d => d.FeedbackId)
                .HasConstraintName("fk_analysis_result_feedback");
        });

        modelBuilder.Entity<Channel>(entity =>
        {
            entity.HasKey(e => e.ChannelId).HasName("channels_pkey");

            entity.ToTable("channels");

            entity.Property(e => e.ChannelId).HasColumnName("channel_id");
            entity.Property(e => e.ChannelName)
                .HasMaxLength(100)
                .HasColumnName("channel_name");
            entity.Property(e => e.ExternalConversationId)
                .HasMaxLength(200)
                .HasColumnName("external_conversation_id");
            entity.Property(e => e.ExternalMessageId)
                .HasMaxLength(200)
                .HasColumnName("external_message_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.ReceivedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("received_at");
            entity.Property(e => e.SourceUserExternalId)
                .HasMaxLength(200)
                .HasColumnName("source_user_external_id");

            entity.HasOne(d => d.Feedback).WithMany(p => p.Channels)
                .HasForeignKey(d => d.FeedbackId)
                .HasConstraintName("fk_channel_feedback");
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.HasKey(e => e.FeedbackId).HasName("feedbacks_pkey");

            entity.ToTable("feedbacks");

            entity.Property(e => e.FeedbackId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("feedback_id");
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
            entity.Property(e => e.ApprovedByManagerId).HasColumnName("approved_by_manager_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DueDate).HasColumnName("due_date");
            entity.Property(e => e.IsMasterTicket)
                .HasDefaultValue(false)
                .HasColumnName("is_master_ticket");
            entity.Property(e => e.Latitude)
                .HasPrecision(10, 7)
                .HasColumnName("latitude");
            entity.Property(e => e.LocationText)
                .HasMaxLength(255)
                .HasColumnName("location_text");
            entity.Property(e => e.Longitude)
                .HasPrecision(10, 7)
                .HasColumnName("longitude");
            entity.Property(e => e.ParentTicketId).HasColumnName("parent_ticket_id");
            entity.Property(e => e.Priority)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Medium'::character varying")
                .HasColumnName("priority");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Submitted'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.ApprovedByManager).WithMany(p => p.FeedbackApprovedByManagers)
                .HasForeignKey(d => d.ApprovedByManagerId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_feedback_manager");

            entity.HasOne(d => d.Category).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_category");

            entity.HasOne(d => d.ParentTicket).WithMany(p => p.InverseParentTicket)
                .HasForeignKey(d => d.ParentTicketId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_feedback_parent");

            entity.HasOne(d => d.User).WithMany(p => p.FeedbackUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_user");
        });

        modelBuilder.Entity<FeedbackAssignment>(entity =>
        {
            entity.HasKey(e => e.AssignmentId).HasName("feedback_assignments_pkey");

            entity.ToTable("feedback_assignments");

            entity.Property(e => e.AssignmentId).HasColumnName("assignment_id");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("assigned_at");
            entity.Property(e => e.AssignedByUserId).HasColumnName("assigned_by_user_id");
            entity.Property(e => e.AssignmentStatus)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Assigned'::character varying")
                .HasColumnName("assignment_status");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.Note)
                .HasMaxLength(500)
                .HasColumnName("note");
            entity.Property(e => e.OperatorId).HasColumnName("operator_id");

            entity.HasOne(d => d.AssignedByUser).WithMany(p => p.FeedbackAssignments)
                .HasForeignKey(d => d.AssignedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_assignment_assigned_by");

            entity.HasOne(d => d.Feedback).WithMany(p => p.FeedbackAssignments)
                .HasForeignKey(d => d.FeedbackId)
                .HasConstraintName("fk_feedback_assignment_feedback");

            entity.HasOne(d => d.Operator).WithMany(p => p.FeedbackAssignments)
                .HasForeignKey(d => d.OperatorId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_assignment_operator");
        });

        modelBuilder.Entity<FeedbackAttachment>(entity =>
        {
            entity.HasKey(e => e.AttachmentId).HasName("feedback_attachments_pkey");

            entity.ToTable("feedback_attachments");

            entity.Property(e => e.AttachmentId).HasColumnName("attachment_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.FileType)
                .HasMaxLength(50)
                .HasColumnName("file_type");
            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .HasColumnName("file_url");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("uploaded_at");

            entity.HasOne(d => d.Feedback).WithMany(p => p.FeedbackAttachments)
                .HasForeignKey(d => d.FeedbackId)
                .HasConstraintName("fk_feedback_attachment_feedback");
        });

        modelBuilder.Entity<FeedbackComment>(entity =>
        {
            entity.HasKey(e => e.CommentId).HasName("feedback_comments_pkey");

            entity.ToTable("feedback_comments");

            entity.Property(e => e.CommentId).HasColumnName("comment_id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Feedback).WithMany(p => p.FeedbackComments)
                .HasForeignKey(d => d.FeedbackId)
                .HasConstraintName("fk_feedback_comment_feedback");

            entity.HasOne(d => d.User).WithMany(p => p.FeedbackComments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_comment_user");
        });

        modelBuilder.Entity<FeedbackResolution>(entity =>
        {
            entity.HasKey(e => e.ResolutionId).HasName("feedback_resolutions_pkey");

            entity.ToTable("feedback_resolutions");

            entity.Property(e => e.ResolutionId).HasColumnName("resolution_id");
            entity.Property(e => e.ActionTaken).HasColumnName("action_taken");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.OperatorId).HasColumnName("operator_id");
            entity.Property(e => e.ResolutionSummary)
                .HasMaxLength(500)
                .HasColumnName("resolution_summary");
            entity.Property(e => e.ResolvedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("resolved_at");
            entity.Property(e => e.ResolvedByUserId).HasColumnName("resolved_by_user_id");
            entity.Property(e => e.ResultNote).HasColumnName("result_note");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'SubmittedForApproval'::character varying")
                .HasColumnName("status");

            entity.HasOne(d => d.Feedback).WithMany(p => p.FeedbackResolutions)
                .HasForeignKey(d => d.FeedbackId)
                .HasConstraintName("fk_feedback_resolution_feedback");

            entity.HasOne(d => d.Operator).WithMany(p => p.FeedbackResolutions)
                .HasForeignKey(d => d.OperatorId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_resolution_operator");

            entity.HasOne(d => d.ResolvedByUser).WithMany(p => p.FeedbackResolutions)
                .HasForeignKey(d => d.ResolvedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_resolution_user");
        });

        modelBuilder.Entity<FeedbackResolutionAttachment>(entity =>
        {
            entity.HasKey(e => e.ResolutionAttachmentId).HasName("feedback_resolution_attachments_pkey");

            entity.ToTable("feedback_resolution_attachments");

            entity.Property(e => e.ResolutionAttachmentId).HasColumnName("resolution_attachment_id");
            entity.Property(e => e.FileType)
                .HasMaxLength(50)
                .HasColumnName("file_type");
            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .HasColumnName("file_url");
            entity.Property(e => e.ResolutionId).HasColumnName("resolution_id");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("uploaded_at");

            entity.HasOne(d => d.Resolution).WithMany(p => p.FeedbackResolutionAttachments)
                .HasForeignKey(d => d.ResolutionId)
                .HasConstraintName("fk_resolution_attachment_resolution");
        });

        modelBuilder.Entity<FeedbackResolutionReview>(entity =>
        {
            entity.HasKey(e => e.ReviewId).HasName("feedback_resolution_reviews_pkey");

            entity.ToTable("feedback_resolution_reviews");

            entity.Property(e => e.ReviewId).HasColumnName("review_id");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.IsSatisfied).HasColumnName("is_satisfied");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Feedback).WithMany(p => p.FeedbackResolutionReviews)
                .HasForeignKey(d => d.FeedbackId)
                .HasConstraintName("fk_resolution_review_feedback");

            entity.HasOne(d => d.User).WithMany(p => p.FeedbackResolutionReviews)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_resolution_review_user");
        });

        modelBuilder.Entity<FeedbackStatusHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("feedback_status_histories_pkey");

            entity.ToTable("feedback_status_histories");

            entity.Property(e => e.HistoryId).HasColumnName("history_id");
            entity.Property(e => e.ChangedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("changed_at");
            entity.Property(e => e.ChangedByUserId).HasColumnName("changed_by_user_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.NewStatus)
                .HasMaxLength(50)
                .HasColumnName("new_status");
            entity.Property(e => e.Note)
                .HasMaxLength(500)
                .HasColumnName("note");
            entity.Property(e => e.OldStatus)
                .HasMaxLength(50)
                .HasColumnName("old_status");

            entity.HasOne(d => d.ChangedByUser).WithMany(p => p.FeedbackStatusHistories)
                .HasForeignKey(d => d.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_status_history_user");

            entity.HasOne(d => d.Feedback).WithMany(p => p.FeedbackStatusHistories)
                .HasForeignKey(d => d.FeedbackId)
                .HasConstraintName("fk_status_history_feedback");
        });

        modelBuilder.Entity<FeedbackSupport>(entity =>
        {
            entity.HasKey(e => e.SupportId).HasName("feedback_supports_pkey");

            entity.ToTable("feedback_supports");

            entity.HasIndex(e => new { e.FeedbackId, e.UserId }, "uq_feedback_support_user").IsUnique();

            entity.Property(e => e.SupportId).HasColumnName("support_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Feedback).WithMany(p => p.FeedbackSupports)
                .HasForeignKey(d => d.FeedbackId)
                .HasConstraintName("fk_feedback_support_feedback");

            entity.HasOne(d => d.User).WithMany(p => p.FeedbackSupports)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_support_user");
        });

        modelBuilder.Entity<InteractionMessage>(entity =>
        {
            entity.HasKey(e => e.InteractionMessageId).HasName("interaction_messages_pkey");

            entity.ToTable("interaction_messages");

            entity.Property(e => e.InteractionMessageId).HasColumnName("interaction_message_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.IsInternal)
                .HasDefaultValue(false)
                .HasColumnName("is_internal");
            entity.Property(e => e.MessageText).HasColumnName("message_text");
            entity.Property(e => e.SenderType)
                .HasMaxLength(50)
                .HasColumnName("sender_type");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Feedback).WithMany(p => p.InteractionMessages)
                .HasForeignKey(d => d.FeedbackId)
                .HasConstraintName("fk_interaction_message_feedback");

            entity.HasOne(d => d.User).WithMany(p => p.InteractionMessages)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_interaction_message_user");
        });

        modelBuilder.Entity<MessageAttachment>(entity =>
        {
            entity.HasKey(e => e.MessageAttachmentId).HasName("message_attachments_pkey");

            entity.ToTable("message_attachments");

            entity.Property(e => e.MessageAttachmentId).HasColumnName("message_attachment_id");
            entity.Property(e => e.FileType)
                .HasMaxLength(50)
                .HasColumnName("file_type");
            entity.Property(e => e.FileUrl)
                .HasMaxLength(500)
                .HasColumnName("file_url");
            entity.Property(e => e.InteractionMessageId).HasColumnName("interaction_message_id");
            entity.Property(e => e.UploadedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("uploaded_at");

            entity.HasOne(d => d.InteractionMessage).WithMany(p => p.MessageAttachments)
                .HasForeignKey(d => d.InteractionMessageId)
                .HasConstraintName("fk_message_attachment_interaction_message");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("notifications_pkey");

            entity.ToTable("notifications");

            entity.Property(e => e.NotificationId).HasColumnName("notification_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.IsRead)
                .HasDefaultValue(false)
                .HasColumnName("is_read");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.TargetUrl)
                .HasMaxLength(500)
                .HasColumnName("target_url");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasColumnName("type");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_notification_user");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("roles_pkey");

            entity.ToTable("roles");

            entity.HasIndex(e => e.RoleName, "roles_role_name_key").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.Description)
                .HasMaxLength(255)
                .HasColumnName("description");
            entity.Property(e => e.RoleName)
                .HasMaxLength(100)
                .HasColumnName("role_name");
        });

        modelBuilder.Entity<ServiceOperator>(entity =>
        {
            entity.HasKey(e => e.OperatorId).HasName("service_operators_pkey");

            entity.ToTable("service_operators");

            entity.Property(e => e.OperatorId).HasColumnName("operator_id");
            entity.Property(e => e.Address)
                .HasMaxLength(255)
                .HasColumnName("address");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.ContactEmail)
                .HasMaxLength(150)
                .HasColumnName("contact_email");
            entity.Property(e => e.ContactPhone)
                .HasMaxLength(20)
                .HasColumnName("contact_phone");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.OperatorName)
                .HasMaxLength(200)
                .HasColumnName("operator_name");

            entity.HasOne(d => d.Category).WithMany(p => p.ServiceOperators)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_service_operator_category");
        });

        modelBuilder.Entity<Service>(entity =>
        {
            entity.HasKey(e => e.ServiceId).HasName("services_pkey");

            entity.ToTable("services");

            entity.HasIndex(e => new { e.OperatorId, e.ServiceName }, "uq_service_operator_name").IsUnique();

            entity.Property(e => e.ServiceId).HasColumnName("service_id");
            entity.Property(e => e.BasePrice)
                .HasPrecision(18, 2)
                .HasColumnName("base_price");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .HasDefaultValueSql("'VND'::character varying")
                .HasColumnName("currency");
            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .HasColumnName("description");
            entity.Property(e => e.ExternalServiceUrl)
                .HasMaxLength(500)
                .HasColumnName("external_service_url");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsSystemService).HasColumnName("is_system_service");
            entity.Property(e => e.OperatorId).HasColumnName("operator_id");
            entity.Property(e => e.ServiceName)
                .HasMaxLength(200)
                .HasColumnName("service_name");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Category).WithMany(p => p.Services)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_service_category");

            entity.HasOne(d => d.Operator).WithMany(p => p.Services)
                .HasForeignKey(d => d.OperatorId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_service_operator");
        });

        modelBuilder.Entity<ServicePayment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("service_payments_pkey");

            entity.ToTable("service_payments");

            entity.HasIndex(e => e.TransactionReference, "uq_service_payment_transaction_reference")
                .IsUnique();

            entity.Property(e => e.PaymentId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("payment_id");
            entity.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Currency)
                .HasMaxLength(3)
                .HasDefaultValueSql("'VND'::character varying")
                .HasColumnName("currency");
            entity.Property(e => e.PaidAt).HasColumnName("paid_at");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(50)
                .HasColumnName("payment_method");
            entity.Property(e => e.ServiceId).HasColumnName("service_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Pending'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.TransactionReference)
                .HasMaxLength(200)
                .HasColumnName("transaction_reference");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Service).WithMany(p => p.ServicePayments)
                .HasForeignKey(d => d.ServiceId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_service_payment_service");

            entity.HasOne(d => d.User).WithMany(p => p.ServicePayments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_service_payment_user");
        });

        modelBuilder.Entity<UrbanServiceCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("urban_service_categories_pkey");

            entity.ToTable("urban_service_categories");

            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CategoryName)
                .HasMaxLength(150)
                .HasColumnName("category_name");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.Property(e => e.UserId)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("user_id");
            entity.Property(e => e.Address)
                .HasMaxLength(255)
                .HasColumnName("address");
            entity.Property(e => e.AvatarUrl)
                .HasMaxLength(500)
                .HasColumnName("avatar_url");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(150)
                .HasColumnName("full_name");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsVerified)
                .HasDefaultValue(false)
                .HasColumnName("is_verified");
            entity.Property(e => e.IsRefreshTokenRevoked)
                .HasDefaultValue(false)
                .HasColumnName("is_refresh_token_revoked");
            entity.Property(e => e.OperatorId).HasColumnName("operator_id");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20)
                .HasColumnName("phone_number");
            entity.Property(e => e.RefreshToken).HasColumnName("refresh_token");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Operator).WithMany(p => p.Users)
                .HasForeignKey(d => d.OperatorId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_user_operator");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_user_role");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
