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

    public virtual DbSet<AreaAlert> AreaAlerts { get; set; }

    public virtual DbSet<AreaHotspot> AreaHotspots { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Channel> Channels { get; set; }

    public virtual DbSet<CompletionDocument> CompletionDocuments { get; set; }

    public virtual DbSet<CoordinatorCoverage> CoordinatorCoverages { get; set; }

    public virtual DbSet<Feedback> Feedbacks { get; set; }

    public virtual DbSet<FeedbackAttachment> FeedbackAttachments { get; set; }

    public virtual DbSet<FeedbackComment> FeedbackComments { get; set; }

    public virtual DbSet<FeedbackDuplicateCandidate> FeedbackDuplicateCandidates { get; set; }

    public virtual DbSet<FeedbackProviderReport> FeedbackProviderReports { get; set; }

    public virtual DbSet<FeedbackResolution> FeedbackResolutions { get; set; }

    public virtual DbSet<FeedbackResolutionReview> FeedbackResolutionReviews { get; set; }

    public virtual DbSet<FeedbackStatusHistory> FeedbackStatusHistories { get; set; }

    public virtual DbSet<FeedbackSupport> FeedbackSupports { get; set; }

    public virtual DbSet<InteractionMessage> InteractionMessages { get; set; }

    public virtual DbSet<MessageAttachment> MessageAttachments { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<OperatingArea> OperatingAreas { get; set; }

    public virtual DbSet<ProviderContactLog> ProviderContactLogs { get; set; }

    public virtual DbSet<ProviderContract> ProviderContracts { get; set; }

    public virtual DbSet<ProviderContractAttachment> ProviderContractAttachments { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<ServiceProviderCoordinator> ServiceProviderCoordinators { get; set; }

    public virtual DbSet<StaffAreaAssignment> StaffAreaAssignments { get; set; }

    public virtual DbSet<UrbanServiceCategory> UrbanServiceCategories { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserAreaSubscription> UserAreaSubscriptions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("roles_pkey");
            entity.ToTable("roles");
            entity.HasIndex(e => e.RoleName, "roles_role_name_key").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.RoleName).HasMaxLength(100).HasColumnName("role_name");
            entity.Property(e => e.Description).HasMaxLength(255).HasColumnName("description");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("users_pkey");
            entity.ToTable("users");
            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.Property(e => e.UserId).HasDefaultValueSql("gen_random_uuid()").HasColumnName("user_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.FullName).HasMaxLength(150).HasColumnName("full_name");
            entity.Property(e => e.Email).HasMaxLength(150).HasColumnName("email");
            entity.Property(e => e.PasswordHash).HasMaxLength(255).HasColumnName("password_hash");
            entity.Property(e => e.PhoneNumber).HasMaxLength(20).HasColumnName("phone_number");
            entity.Property(e => e.Address).HasMaxLength(255).HasColumnName("address");
            entity.Property(e => e.AvatarUrl).HasMaxLength(500).HasColumnName("avatar_url");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.IsVerified).HasDefaultValue(false).HasColumnName("is_verified");
            entity.Property(e => e.RefreshToken).HasColumnName("refresh_token");
            entity.Property(e => e.IsRefreshTokenRevoked).HasDefaultValue(false).HasColumnName("is_refresh_token_revoked");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_user_role");
        });

        modelBuilder.Entity<OperatingArea>(entity =>
        {
            entity.HasKey(e => e.AreaId).HasName("operating_areas_pkey");
            entity.ToTable("operating_areas");
            entity.HasIndex(e => e.WardCode, "operating_areas_ward_code_key").IsUnique();

            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.AreaName).HasMaxLength(200).HasColumnName("area_name");
            entity.Property(e => e.AreaType).HasMaxLength(50).HasColumnName("area_type");
            entity.Property(e => e.WardCode).HasMaxLength(50).HasColumnName("ward_code");
            entity.Property(e => e.DistrictName).HasMaxLength(150).HasColumnName("district_name");
            entity.Property(e => e.ProvinceName).HasMaxLength(150).HasColumnName("province_name");
            entity.Property(e => e.CenterLatitude).HasPrecision(10, 7).HasColumnName("center_latitude");
            entity.Property(e => e.CenterLongitude).HasPrecision(10, 7).HasColumnName("center_longitude");
            entity.Property(e => e.BoundaryGeoJson).HasColumnName("boundary_geo_json");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.EndedAt).HasColumnName("ended_at");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<UserAreaSubscription>(entity =>
        {
            entity.HasKey(e => e.SubscriptionId).HasName("user_area_subscriptions_pkey");
            entity.ToTable("user_area_subscriptions");
            entity.HasIndex(e => new { e.UserId, e.AreaId }, "uq_user_area_subscription").IsUnique();

            entity.Property(e => e.SubscriptionId).HasColumnName("subscription_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.IsPrimaryArea).HasDefaultValue(false).HasColumnName("is_primary_area");
            entity.Property(e => e.ReceiveAlerts).HasDefaultValue(true).HasColumnName("receive_alerts");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

            entity.HasOne(d => d.User).WithMany(p => p.UserAreaSubscriptions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_user_area_subscription_user");

            entity.HasOne(d => d.Area).WithMany(p => p.UserAreaSubscriptions)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_user_area_subscription_area");
        });

        modelBuilder.Entity<StaffAreaAssignment>(entity =>
        {
            entity.HasKey(e => e.StaffAreaAssignmentId).HasName("staff_area_assignments_pkey");
            entity.ToTable("staff_area_assignments");

            entity.Property(e => e.StaffAreaAssignmentId).HasColumnName("staff_area_assignment_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.AssignedByUserId).HasColumnName("assigned_by_user_id");
            entity.Property(e => e.IsPrimary).HasDefaultValue(false).HasColumnName("is_primary");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

            entity.HasOne(d => d.User).WithMany(p => p.StaffAreaAssignmentUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_staff_area_assignment_user");

            entity.HasOne(d => d.AssignedByUser).WithMany(p => p.StaffAreaAssignmentAssignedByUsers)
                .HasForeignKey(d => d.AssignedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_staff_area_assignment_assigned_by");

            entity.HasOne(d => d.Area).WithMany(p => p.StaffAreaAssignments)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_staff_area_assignment_area");
        });

        modelBuilder.Entity<UrbanServiceCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("urban_service_categories_pkey");
            entity.ToTable("urban_service_categories");

            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CategoryName).HasMaxLength(150).HasColumnName("category_name");
            entity.Property(e => e.Description).HasMaxLength(500).HasColumnName("description");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
        });

        modelBuilder.Entity<ServiceProviderCoordinator>(entity =>
        {
            entity.HasKey(e => e.CoordinatorId).HasName("service_provider_coordinators_pkey");
            entity.ToTable("service_provider_coordinators");

            entity.Property(e => e.CoordinatorId).HasColumnName("coordinator_id");
            entity.Property(e => e.ProviderName).HasMaxLength(200).HasColumnName("provider_name");
            entity.Property(e => e.CoordinatorName).HasMaxLength(150).HasColumnName("coordinator_name");
            entity.Property(e => e.PhoneNumber).HasMaxLength(20).HasColumnName("phone_number");
            entity.Property(e => e.Email).HasMaxLength(150).HasColumnName("email");
            entity.Property(e => e.Address).HasMaxLength(255).HasColumnName("address");
            entity.Property(e => e.Note).HasMaxLength(500).HasColumnName("note");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<CoordinatorCoverage>(entity =>
        {
            entity.HasKey(e => e.CoverageId).HasName("coordinator_coverages_pkey");
            entity.ToTable("coordinator_coverages");

            entity.Property(e => e.CoverageId).HasColumnName("coverage_id");
            entity.Property(e => e.CoordinatorId).HasColumnName("coordinator_id");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.IsPrimary).HasDefaultValue(false).HasColumnName("is_primary");
            entity.Property(e => e.PriorityOrder).HasDefaultValue(1).HasColumnName("priority_order");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

            entity.HasOne(d => d.Coordinator).WithMany(p => p.CoordinatorCoverages)
                .HasForeignKey(d => d.CoordinatorId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_coordinator_coverage_coordinator");

            entity.HasOne(d => d.Area).WithMany(p => p.CoordinatorCoverages)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_coordinator_coverage_area");

            entity.HasOne(d => d.Category).WithMany(p => p.CoordinatorCoverages)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_coordinator_coverage_category");
        });

        modelBuilder.Entity<ProviderContract>(entity =>
        {
            entity.HasKey(e => e.ContractId).HasName("provider_contracts_pkey");
            entity.ToTable("provider_contracts");
            entity.HasIndex(e => e.ContractCode, "provider_contracts_contract_code_key").IsUnique();

            entity.Property(e => e.ContractId).HasColumnName("contract_id");
            entity.Property(e => e.CoordinatorId).HasColumnName("coordinator_id");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.ContractCode).HasMaxLength(100).HasColumnName("contract_code");
            entity.Property(e => e.ContractName).HasMaxLength(200).HasColumnName("contract_name");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'Draft'::character varying").HasColumnName("status");
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Coordinator).WithMany(p => p.ProviderContracts)
                .HasForeignKey(d => d.CoordinatorId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_provider_contract_coordinator");

            entity.HasOne(d => d.Area).WithMany(p => p.ProviderContracts)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_provider_contract_area");

            entity.HasOne(d => d.Category).WithMany(p => p.ProviderContracts)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_provider_contract_category");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.ProviderContracts)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_provider_contract_created_by");
        });

        modelBuilder.Entity<ProviderContractAttachment>(entity =>
        {
            entity.HasKey(e => e.ContractAttachmentId).HasName("provider_contract_attachments_pkey");
            entity.ToTable("provider_contract_attachments");

            entity.Property(e => e.ContractAttachmentId).HasColumnName("contract_attachment_id");
            entity.Property(e => e.ContractId).HasColumnName("contract_id");
            entity.Property(e => e.FileUrl).HasMaxLength(500).HasColumnName("file_url");
            entity.Property(e => e.FileType).HasMaxLength(50).HasColumnName("file_type");
            entity.Property(e => e.Description).HasMaxLength(500).HasColumnName("description");
            entity.Property(e => e.UploadedByUserId).HasColumnName("uploaded_by_user_id");
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("now()").HasColumnName("uploaded_at");

            entity.HasOne(d => d.Contract).WithMany(p => p.ProviderContractAttachments)
                .HasForeignKey(d => d.ContractId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_provider_contract_attachment_contract");

            entity.HasOne(d => d.UploadedByUser).WithMany(p => p.ProviderContractAttachments)
                .HasForeignKey(d => d.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_provider_contract_attachment_uploaded_by");
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.HasKey(e => e.FeedbackId).HasName("feedbacks_pkey");
            entity.ToTable("feedbacks");

            entity.Property(e => e.FeedbackId).HasDefaultValueSql("gen_random_uuid()").HasColumnName("feedback_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.Title).HasMaxLength(200).HasColumnName("title");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.LocationText).HasMaxLength(255).HasColumnName("location_text");
            entity.Property(e => e.Latitude).HasPrecision(10, 7).HasColumnName("latitude");
            entity.Property(e => e.Longitude).HasPrecision(10, 7).HasColumnName("longitude");
            entity.Property(e => e.LocationAccuracyMeters).HasColumnName("location_accuracy_meters");
            entity.Property(e => e.GeoSource).HasMaxLength(50).HasColumnName("geo_source");
            entity.Property(e => e.IsLocationVerified).HasDefaultValue(false).HasColumnName("is_location_verified");
            entity.Property(e => e.Priority).HasMaxLength(50).HasDefaultValueSql("'Medium'::character varying").HasColumnName("priority");
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'Submitted'::character varying").HasColumnName("status");
            entity.Property(e => e.DueDate).HasColumnName("due_date");
            entity.Property(e => e.ApprovedByManagerId).HasColumnName("approved_by_manager_id");
            entity.Property(e => e.ApprovedAt).HasColumnName("approved_at");
            entity.Property(e => e.IsMasterTicket).HasDefaultValue(false).HasColumnName("is_master_ticket");
            entity.Property(e => e.ParentTicketId).HasColumnName("parent_ticket_id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.User).WithMany(p => p.FeedbackUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_user");

            entity.HasOne(d => d.Area).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_area");

            entity.HasOne(d => d.Category).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_category");

            entity.HasOne(d => d.ApprovedByManager).WithMany(p => p.FeedbackApprovedByManagers)
                .HasForeignKey(d => d.ApprovedByManagerId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_feedback_manager");

            entity.HasOne(d => d.ParentTicket).WithMany(p => p.InverseParentTicket)
                .HasForeignKey(d => d.ParentTicketId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_feedback_parent");
        });

        modelBuilder.Entity<FeedbackDuplicateCandidate>(entity =>
        {
            entity.HasKey(e => e.DuplicateCandidateId).HasName("feedback_duplicate_candidates_pkey");
            entity.ToTable("feedback_duplicate_candidates");

            entity.HasIndex(e => new { e.FeedbackId, e.PotentialParentFeedbackId }, "uq_feedback_duplicate_candidate_pair").IsUnique();

            entity.HasIndex(e => e.Status, "ix_feedback_duplicate_candidates_status");

            entity.Property(e => e.DuplicateCandidateId).HasDefaultValueSql("gen_random_uuid()").HasColumnName("duplicate_candidate_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.PotentialParentFeedbackId).HasColumnName("potential_parent_feedback_id");
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'Pending'::character varying").HasColumnName("status");
            entity.Property(e => e.ConfidenceScore).HasPrecision(5, 4).HasColumnName("confidence_score");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.ReviewedByUserId).HasColumnName("reviewed_by_user_id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Feedback).WithMany(p => p.FeedbackDuplicateCandidates)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_feedback_duplicate_candidate_feedback");

            entity.HasOne(d => d.PotentialParentFeedback).WithMany(p => p.PotentialParentDuplicateCandidates)
                .HasForeignKey(d => d.PotentialParentFeedbackId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_duplicate_candidate_parent_feedback");

            entity.HasOne(d => d.ReviewedByUser).WithMany(p => p.FeedbackDuplicateCandidateReviews)
                .HasForeignKey(d => d.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_feedback_duplicate_candidate_reviewed_by");
        });

        modelBuilder.Entity<FeedbackAttachment>(entity =>
        {
            entity.HasKey(e => e.AttachmentId).HasName("feedback_attachments_pkey");
            entity.ToTable("feedback_attachments");

            entity.Property(e => e.AttachmentId).HasColumnName("attachment_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.FileUrl).HasMaxLength(500).HasColumnName("file_url");
            entity.Property(e => e.FileType).HasMaxLength(50).HasColumnName("file_type");
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("now()").HasColumnName("uploaded_at");

            entity.HasOne(d => d.Feedback).WithMany(p => p.FeedbackAttachments)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_feedback_attachment_feedback");
        });

        modelBuilder.Entity<FeedbackComment>(entity =>
        {
            entity.HasKey(e => e.CommentId).HasName("feedback_comments_pkey");
            entity.ToTable("feedback_comments");

            entity.Property(e => e.CommentId).HasColumnName("comment_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

            entity.HasOne(d => d.Feedback).WithMany(p => p.FeedbackComments)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_feedback_comment_feedback");

            entity.HasOne(d => d.User).WithMany(p => p.FeedbackComments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_comment_user");
        });

        modelBuilder.Entity<FeedbackSupport>(entity =>
        {
            entity.HasKey(e => e.SupportId).HasName("feedback_supports_pkey");
            entity.ToTable("feedback_supports");
            entity.HasIndex(e => new { e.FeedbackId, e.UserId }, "uq_feedback_support_user").IsUnique();

            entity.Property(e => e.SupportId).HasColumnName("support_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

            entity.HasOne(d => d.Feedback).WithMany(p => p.FeedbackSupports)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_feedback_support_feedback");

            entity.HasOne(d => d.User).WithMany(p => p.FeedbackSupports)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_support_user");
        });

        modelBuilder.Entity<FeedbackProviderReport>(entity =>
        {
            entity.HasKey(e => e.ProviderReportId).HasName("feedback_provider_reports_pkey");
            entity.ToTable("feedback_provider_reports");

            entity.Property(e => e.ProviderReportId).HasColumnName("provider_report_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.CoordinatorId).HasColumnName("coordinator_id");
            entity.Property(e => e.ReportedByUserId).HasColumnName("reported_by_user_id");
            entity.Property(e => e.ReportStatus).HasMaxLength(50).HasDefaultValueSql("'Reported'::character varying").HasColumnName("report_status");
            entity.Property(e => e.DueDate).HasColumnName("due_date");
            entity.Property(e => e.ReportNote).HasColumnName("report_note");
            entity.Property(e => e.ReportedAt).HasDefaultValueSql("now()").HasColumnName("reported_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Feedback).WithMany(p => p.FeedbackProviderReports)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_feedback_provider_report_feedback");

            entity.HasOne(d => d.Coordinator).WithMany(p => p.FeedbackProviderReports)
                .HasForeignKey(d => d.CoordinatorId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_provider_report_coordinator");

            entity.HasOne(d => d.ReportedByUser).WithMany(p => p.FeedbackProviderReports)
                .HasForeignKey(d => d.ReportedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_provider_report_user");
        });

        modelBuilder.Entity<ProviderContactLog>(entity =>
        {
            entity.HasKey(e => e.ContactLogId).HasName("provider_contact_logs_pkey");
            entity.ToTable("provider_contact_logs");

            entity.Property(e => e.ContactLogId).HasColumnName("contact_log_id");
            entity.Property(e => e.ProviderReportId).HasColumnName("provider_report_id");
            entity.Property(e => e.CoordinatorId).HasColumnName("coordinator_id");
            entity.Property(e => e.ContactedByUserId).HasColumnName("contacted_by_user_id");
            entity.Property(e => e.ContactMethod).HasMaxLength(50).HasColumnName("contact_method");
            entity.Property(e => e.ContactResult).HasMaxLength(50).HasColumnName("contact_result");
            entity.Property(e => e.ContactNote).HasColumnName("contact_note");
            entity.Property(e => e.ContactedAt).HasDefaultValueSql("now()").HasColumnName("contacted_at");

            entity.HasOne(d => d.ProviderReport).WithMany(p => p.ProviderContactLogs)
                .HasForeignKey(d => d.ProviderReportId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_provider_contact_log_report");

            entity.HasOne(d => d.Coordinator).WithMany(p => p.ProviderContactLogs)
                .HasForeignKey(d => d.CoordinatorId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_provider_contact_log_coordinator");

            entity.HasOne(d => d.ContactedByUser).WithMany(p => p.ProviderContactLogs)
                .HasForeignKey(d => d.ContactedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_provider_contact_log_user");
        });

        modelBuilder.Entity<CompletionDocument>(entity =>
        {
            entity.HasKey(e => e.CompletionDocumentId).HasName("completion_documents_pkey");
            entity.ToTable("completion_documents");

            entity.Property(e => e.CompletionDocumentId).HasColumnName("completion_document_id");
            entity.Property(e => e.ProviderReportId).HasColumnName("provider_report_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.CoordinatorId).HasColumnName("coordinator_id");
            entity.Property(e => e.UploadedByUserId).HasColumnName("uploaded_by_user_id");
            entity.Property(e => e.FileUrl).HasMaxLength(500).HasColumnName("file_url");
            entity.Property(e => e.FileType).HasMaxLength(50).HasColumnName("file_type");
            entity.Property(e => e.Description).HasMaxLength(500).HasColumnName("description");
            entity.Property(e => e.ReceivedAt).HasDefaultValueSql("now()").HasColumnName("received_at");

            entity.HasOne(d => d.ProviderReport).WithMany(p => p.CompletionDocuments)
                .HasForeignKey(d => d.ProviderReportId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_completion_document_report");

            entity.HasOne(d => d.Feedback).WithMany(p => p.CompletionDocuments)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_completion_document_feedback");

            entity.HasOne(d => d.Coordinator).WithMany(p => p.CompletionDocuments)
                .HasForeignKey(d => d.CoordinatorId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_completion_document_coordinator");

            entity.HasOne(d => d.UploadedByUser).WithMany(p => p.CompletionDocuments)
                .HasForeignKey(d => d.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_completion_document_uploaded_by");
        });

        modelBuilder.Entity<FeedbackResolution>(entity =>
        {
            entity.HasKey(e => e.ResolutionId).HasName("feedback_resolutions_pkey");
            entity.ToTable("feedback_resolutions");

            entity.Property(e => e.ResolutionId).HasColumnName("resolution_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.ProviderReportId).HasColumnName("provider_report_id");
            entity.Property(e => e.CreatedByStaffUserId).HasColumnName("created_by_staff_user_id");
            entity.Property(e => e.ResolutionSummary).HasMaxLength(500).HasColumnName("resolution_summary");
            entity.Property(e => e.ActionTaken).HasColumnName("action_taken");
            entity.Property(e => e.ResultNote).HasColumnName("result_note");
            entity.Property(e => e.ResolvedAt).HasDefaultValueSql("now()").HasColumnName("resolved_at");
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'SubmittedForApproval'::character varying").HasColumnName("status");

            entity.HasOne(d => d.Feedback).WithMany(p => p.FeedbackResolutions)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_feedback_resolution_feedback");

            entity.HasOne(d => d.ProviderReport).WithMany(p => p.FeedbackResolutions)
                .HasForeignKey(d => d.ProviderReportId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_feedback_resolution_report");

            entity.HasOne(d => d.CreatedByStaffUser).WithMany(p => p.FeedbackResolutions)
                .HasForeignKey(d => d.CreatedByStaffUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_resolution_user");
        });

        modelBuilder.Entity<FeedbackStatusHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("feedback_status_histories_pkey");
            entity.ToTable("feedback_status_histories");

            entity.Property(e => e.HistoryId).HasColumnName("history_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.ChangedByUserId).HasColumnName("changed_by_user_id");
            entity.Property(e => e.OldStatus).HasMaxLength(50).HasColumnName("old_status");
            entity.Property(e => e.NewStatus).HasMaxLength(50).HasColumnName("new_status");
            entity.Property(e => e.Note).HasMaxLength(500).HasColumnName("note");
            entity.Property(e => e.ChangedAt).HasDefaultValueSql("now()").HasColumnName("changed_at");

            entity.HasOne(d => d.Feedback).WithMany(p => p.FeedbackStatusHistories)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_feedback_status_history_feedback");

            entity.HasOne(d => d.ChangedByUser).WithMany(p => p.FeedbackStatusHistories)
                .HasForeignKey(d => d.ChangedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_status_history_user");
        });

        modelBuilder.Entity<FeedbackResolutionReview>(entity =>
        {
            entity.HasKey(e => e.ReviewId).HasName("feedback_resolution_reviews_pkey");
            entity.ToTable("feedback_resolution_reviews");

            entity.Property(e => e.ReviewId).HasColumnName("review_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.IsSatisfied).HasColumnName("is_satisfied");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

            entity.HasOne(d => d.Feedback).WithMany(p => p.FeedbackResolutionReviews)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_resolution_review_feedback");

            entity.HasOne(d => d.User).WithMany(p => p.FeedbackResolutionReviews)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_resolution_review_user");
        });

        modelBuilder.Entity<AreaHotspot>(entity =>
        {
            entity.HasKey(e => e.HotspotId).HasName("area_hotspots_pkey");
            entity.ToTable("area_hotspots");

            entity.Property(e => e.HotspotId).HasColumnName("hotspot_id");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CenterLatitude).HasPrecision(10, 7).HasColumnName("center_latitude");
            entity.Property(e => e.CenterLongitude).HasPrecision(10, 7).HasColumnName("center_longitude");
            entity.Property(e => e.RadiusMeters).HasColumnName("radius_meters");
            entity.Property(e => e.TimeWindowStart).HasColumnName("time_window_start");
            entity.Property(e => e.TimeWindowEnd).HasColumnName("time_window_end");
            entity.Property(e => e.FeedbackCount).HasDefaultValue(0).HasColumnName("feedback_count");
            entity.Property(e => e.MasterTicketCount).HasDefaultValue(0).HasColumnName("master_ticket_count");
            entity.Property(e => e.AveragePriorityScore).HasPrecision(5, 2).HasColumnName("average_priority_score");
            entity.Property(e => e.RiskLevel).HasMaxLength(50).HasColumnName("risk_level");
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'Active'::character varying").HasColumnName("status");
            entity.Property(e => e.DetectedBy).HasMaxLength(50).HasColumnName("detected_by");
            entity.Property(e => e.SourceQueryJson).HasColumnName("source_query_json");
            entity.Property(e => e.FirstDetectedAt).HasDefaultValueSql("now()").HasColumnName("first_detected_at");
            entity.Property(e => e.LastCalculatedAt).HasDefaultValueSql("now()").HasColumnName("last_calculated_at");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");

            entity.HasOne(d => d.Area).WithMany(p => p.AreaHotspots)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_area_hotspot_area");

            entity.HasOne(d => d.Category).WithMany(p => p.AreaHotspots)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_area_hotspot_category");
        });

        modelBuilder.Entity<AreaAlert>(entity =>
        {
            entity.HasKey(e => e.AlertId).HasName("area_alerts_pkey");
            entity.ToTable("area_alerts");

            entity.Property(e => e.AlertId).HasColumnName("alert_id");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.HotspotId).HasColumnName("hotspot_id");
            entity.Property(e => e.Title).HasMaxLength(200).HasColumnName("title");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.AlertType).HasMaxLength(50).HasColumnName("alert_type");
            entity.Property(e => e.Severity).HasMaxLength(50).HasColumnName("severity");
            entity.Property(e => e.Latitude).HasPrecision(10, 7).HasColumnName("latitude");
            entity.Property(e => e.Longitude).HasPrecision(10, 7).HasColumnName("longitude");
            entity.Property(e => e.RadiusMeters).HasColumnName("radius_meters");
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'Active'::character varying").HasColumnName("status");
            entity.Property(e => e.StartAt).HasColumnName("start_at");
            entity.Property(e => e.EndAt).HasColumnName("end_at");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Area).WithMany(p => p.AreaAlerts)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_area_alert_area");

            entity.HasOne(d => d.Category).WithMany(p => p.AreaAlerts)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_area_alert_category");

            entity.HasOne(d => d.Hotspot).WithMany(p => p.AreaAlerts)
                .HasForeignKey(d => d.HotspotId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_area_alert_hotspot");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.AreaAlerts)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_area_alert_user");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("notifications_pkey");
            entity.ToTable("notifications");

            entity.Property(e => e.NotificationId).HasColumnName("notification_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.AlertId).HasColumnName("alert_id");
            entity.Property(e => e.Title).HasMaxLength(200).HasColumnName("title");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.Type).HasMaxLength(50).HasColumnName("type");
            entity.Property(e => e.IsRead).HasDefaultValue(false).HasColumnName("is_read");
            entity.Property(e => e.TargetUrl).HasMaxLength(500).HasColumnName("target_url");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_notification_user");

            entity.HasOne(d => d.Alert).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.AlertId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_notification_area_alert");
        });

        modelBuilder.Entity<InteractionMessage>(entity =>
        {
            entity.HasKey(e => e.InteractionMessageId).HasName("interaction_messages_pkey");
            entity.ToTable("interaction_messages");

            entity.Property(e => e.InteractionMessageId).HasColumnName("interaction_message_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.SenderType).HasMaxLength(50).HasColumnName("sender_type");
            entity.Property(e => e.MessageText).HasColumnName("message_text");
            entity.Property(e => e.IsInternal).HasDefaultValue(false).HasColumnName("is_internal");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

            entity.HasOne(d => d.Feedback).WithMany(p => p.InteractionMessages)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade)
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
            entity.Property(e => e.InteractionMessageId).HasColumnName("interaction_message_id");
            entity.Property(e => e.FileUrl).HasMaxLength(500).HasColumnName("file_url");
            entity.Property(e => e.FileType).HasMaxLength(50).HasColumnName("file_type");
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("now()").HasColumnName("uploaded_at");

            entity.HasOne(d => d.InteractionMessage).WithMany(p => p.MessageAttachments)
                .HasForeignKey(d => d.InteractionMessageId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_message_attachment_message");
        });

        modelBuilder.Entity<Channel>(entity =>
        {
            entity.HasKey(e => e.ChannelId).HasName("channels_pkey");
            entity.ToTable("channels");

            entity.Property(e => e.ChannelId).HasColumnName("channel_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.ChannelName).HasMaxLength(100).HasColumnName("channel_name");
            entity.Property(e => e.ExternalConversationId).HasMaxLength(200).HasColumnName("external_conversation_id");
            entity.Property(e => e.ExternalMessageId).HasMaxLength(200).HasColumnName("external_message_id");
            entity.Property(e => e.SourceUserExternalId).HasMaxLength(200).HasColumnName("source_user_external_id");
            entity.Property(e => e.ReceivedAt).HasDefaultValueSql("now()").HasColumnName("received_at");

            entity.HasOne(d => d.Feedback).WithMany(p => p.Channels)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_channel_feedback");
        });

        modelBuilder.Entity<AnalysisResult>(entity =>
        {
            entity.HasKey(e => e.AnalysisResultId).HasName("analysis_results_pkey");
            entity.ToTable("analysis_results");

            entity.Property(e => e.AnalysisResultId).HasColumnName("analysis_result_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.ModelName).HasMaxLength(100).HasColumnName("model_name");
            entity.Property(e => e.DetectedCategoryId).HasColumnName("detected_category_id");
            entity.Property(e => e.DetectedAreaId).HasColumnName("detected_area_id");
            entity.Property(e => e.Sentiment).HasMaxLength(50).HasColumnName("sentiment");
            entity.Property(e => e.UrgencyLevel).HasMaxLength(50).HasColumnName("urgency_level");
            entity.Property(e => e.Summary).HasMaxLength(500).HasColumnName("summary");
            entity.Property(e => e.Keywords).HasMaxLength(500).HasColumnName("keywords");
            entity.Property(e => e.ConfidenceScore).HasPrecision(5, 4).HasColumnName("confidence_score");
            entity.Property(e => e.RawResponse).HasColumnName("raw_response");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

            entity.HasOne(d => d.Feedback).WithMany(p => p.AnalysisResults)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_analysis_result_feedback");

            entity.HasOne(d => d.DetectedCategory).WithMany(p => p.AnalysisResults)
                .HasForeignKey(d => d.DetectedCategoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_analysis_result_category");

            entity.HasOne(d => d.DetectedArea).WithMany(p => p.AnalysisResults)
                .HasForeignKey(d => d.DetectedAreaId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_analysis_result_area");
        });

        modelBuilder.Entity<AiConversation>(entity =>
        {
            entity.HasKey(e => e.AiConversationId).HasName("ai_conversations_pkey");
            entity.ToTable("ai_conversations");

            entity.Property(e => e.AiConversationId).HasColumnName("ai_conversation_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.Title).HasMaxLength(200).HasColumnName("title");
            entity.Property(e => e.StartedAt).HasDefaultValueSql("now()").HasColumnName("started_at");
            entity.Property(e => e.EndedAt).HasColumnName("ended_at");
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'Active'::character varying").HasColumnName("status");

            entity.HasOne(d => d.User).WithMany(p => p.AiConversations)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_ai_conversation_user");

            entity.HasOne(d => d.Feedback).WithMany(p => p.AiConversations)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_ai_conversation_feedback");
        });

        modelBuilder.Entity<AiMessage>(entity =>
        {
            entity.HasKey(e => e.AiMessageId).HasName("ai_messages_pkey");
            entity.ToTable("ai_messages");

            entity.Property(e => e.AiMessageId).HasColumnName("ai_message_id");
            entity.Property(e => e.AiConversationId).HasColumnName("ai_conversation_id");
            entity.Property(e => e.SenderType).HasMaxLength(50).HasColumnName("sender_type");
            entity.Property(e => e.MessageText).HasColumnName("message_text");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

            entity.HasOne(d => d.AiConversation).WithMany(p => p.AiMessages)
                .HasForeignKey(d => d.AiConversationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_ai_message_conversation");
        });

        modelBuilder.Entity<AiKnowledgeSource>(entity =>
        {
            entity.HasKey(e => e.KnowledgeSourceId).HasName("ai_knowledge_sources_pkey");
            entity.ToTable("ai_knowledge_sources");

            entity.Property(e => e.KnowledgeSourceId).HasColumnName("knowledge_source_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.Title).HasMaxLength(200).HasColumnName("title");
            entity.Property(e => e.SourceType).HasMaxLength(50).HasColumnName("source_type");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.FileUrl).HasMaxLength(500).HasColumnName("file_url");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Category).WithMany(p => p.AiKnowledgeSources)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_knowledge_source_category");

            entity.HasOne(d => d.Area).WithMany(p => p.AiKnowledgeSources)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_knowledge_source_area");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("audit_logs_pkey");
            entity.ToTable("audit_logs");

            entity.Property(e => e.AuditLogId).HasColumnName("audit_log_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Action).HasMaxLength(100).HasColumnName("action");
            entity.Property(e => e.EntityName).HasMaxLength(100).HasColumnName("entity_name");
            entity.Property(e => e.EntityId).HasMaxLength(100).HasColumnName("entity_id");
            entity.Property(e => e.OldValue).HasColumnName("old_value");
            entity.Property(e => e.NewValue).HasColumnName("new_value");
            entity.Property(e => e.IpAddress).HasMaxLength(50).HasColumnName("ip_address");
            entity.Property(e => e.UserAgent).HasMaxLength(500).HasColumnName("user_agent");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_audit_log_user");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
