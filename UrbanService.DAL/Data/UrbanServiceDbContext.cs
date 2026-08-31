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

    public virtual DbSet<ManagerAreaAssignment> ManagerAreaAssignments { get; set; }

    public virtual DbSet<Incident> Incidents { get; set; }

    public virtual DbSet<IncidentEvent> IncidentEvents { get; set; }

    public virtual DbSet<IncidentReportLink> IncidentReportLinks { get; set; }

    public virtual DbSet<IncidentSubscription> IncidentSubscriptions { get; set; }

    public virtual DbSet<MessageAttachment> MessageAttachments { get; set; }

    public virtual DbSet<MessengerFeedbackConversation> MessengerFeedbackConversations { get; set; }

    public virtual DbSet<MessengerFeedbackDraftAttachment> MessengerFeedbackDraftAttachments { get; set; }

    public virtual DbSet<MessengerFeedbackSubmission> MessengerFeedbackSubmissions { get; set; }

    public virtual DbSet<ZaloFeedbackConversation> ZaloFeedbackConversations { get; set; }

    public virtual DbSet<ZaloFeedbackDraftAttachment> ZaloFeedbackDraftAttachments { get; set; }

    public virtual DbSet<ZaloFeedbackSubmission> ZaloFeedbackSubmissions { get; set; }

    public virtual DbSet<ZaloOauthCredential> ZaloOauthCredentials { get; set; }

    public virtual DbSet<ZaloWebhookEvent> ZaloWebhookEvents { get; set; }

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

    public virtual DbSet<SlaPolicy> SlaPolicies { get; set; }

    public virtual DbSet<FeedbackSla> FeedbackSlas { get; set; }

    public virtual DbSet<SlaEvent> SlaEvents { get; set; }

    public virtual DbSet<SlaPauseHistory> SlaPauseHistories { get; set; }

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
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.AssignedByUserId).HasColumnName("assigned_by_user_id");
            entity.Property(e => e.IsPrimary).HasDefaultValue(false).HasColumnName("is_primary");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

            entity.HasIndex(e => new { e.UserId, e.AreaId, e.CategoryId }, "uq_staff_responsibility_scope")
                .IsUnique();

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

            entity.HasOne(d => d.Category).WithMany(p => p.StaffAreaAssignments)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_staff_area_assignment_category");
        });

        modelBuilder.Entity<ManagerAreaAssignment>(entity =>
        {
            entity.HasKey(e => e.ManagerAreaAssignmentId)
                .HasName("manager_area_assignments_pkey");
            entity.ToTable("manager_area_assignments");

            entity.Property(e => e.ManagerAreaAssignmentId)
                .HasColumnName("manager_area_assignment_id");
            entity.Property(e => e.ManagerUserId).HasColumnName("manager_user_id");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id");
            entity.Property(e => e.UpdatedByUserId).HasColumnName("updated_by_user_id");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(
                    e => new { e.ManagerUserId, e.AreaId },
                    "uq_manager_area_assignment_scope")
                .IsUnique();

            entity.HasOne(d => d.ManagerUser)
                .WithMany(p => p.ManagerAreaAssignmentManagers)
                .HasForeignKey(d => d.ManagerUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_manager_area_assignment_manager");

            entity.HasOne(d => d.Area)
                .WithMany(p => p.ManagerAreaAssignments)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_manager_area_assignment_area");

            entity.HasOne(d => d.CreatedByUser)
                .WithMany(p => p.ManagerAreaAssignmentCreatedByUsers)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_manager_area_assignment_created_by");

            entity.HasOne(d => d.UpdatedByUser)
                .WithMany(p => p.ManagerAreaAssignmentUpdatedByUsers)
                .HasForeignKey(d => d.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_manager_area_assignment_updated_by");
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

        modelBuilder.Entity<Incident>(entity =>
        {
            entity.HasKey(e => e.IncidentId).HasName("incidents_pkey");
            entity.ToTable("incidents", table =>
            {
                table.HasCheckConstraint(
                    "ck_incident_status",
                    "status IN ('New', 'Submitted', 'AiReviewed', 'Verified', 'Assigned', 'InProgress', 'Resolved', 'SubmittedForApproval', 'Approved', 'Rejected', 'NeedRework', 'Closed', 'Cancelled', 'Merged')");
                table.HasCheckConstraint(
                    "ck_incident_merge_not_self",
                    "merged_into_incident_id IS NULL OR merged_into_incident_id <> incident_id");
                table.HasCheckConstraint(
                    "ck_incident_severity",
                    "severity IN ('Low', 'Medium', 'High', 'Critical')");
            });

            entity.HasIndex(e => new { e.AreaId, e.Status, e.CreatedAt }, "ix_incidents_area_status_created_at");
            entity.HasIndex(e => new { e.CategoryId, e.Status }, "ix_incidents_category_status");
            entity.HasIndex(e => e.MergedIntoIncidentId, "ix_incidents_merged_into_incident_id");
            entity.HasIndex(e => e.AssignedStaffUserId, "ix_incidents_assigned_staff_user_id");

            entity.Property(e => e.IncidentId).HasDefaultValueSql("gen_random_uuid()").HasColumnName("incident_id");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.Title).HasMaxLength(200).HasColumnName("title");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.LocationText).HasMaxLength(255).HasColumnName("location_text");
            entity.Property(e => e.Latitude).HasPrecision(10, 7).HasColumnName("latitude");
            entity.Property(e => e.Longitude).HasPrecision(10, 7).HasColumnName("longitude");
            entity.Property(e => e.Priority).HasMaxLength(50).HasDefaultValueSql("'Medium'::character varying").HasColumnName("priority");
            entity.Property(e => e.Severity).HasMaxLength(20).HasDefaultValue("Medium").HasColumnName("severity");
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'New'::character varying").HasColumnName("status");
            entity.Property(e => e.DueDate).HasColumnName("due_date");
            entity.Property(e => e.AssignedAt).HasColumnName("assigned_at");
            entity.Property(e => e.ProcessingStartedAt).HasColumnName("processing_started_at");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.ClosedAt).HasColumnName("closed_at");
            entity.Property(e => e.MergedIntoIncidentId).HasColumnName("merged_into_incident_id");
            entity.Property(e => e.AssignedStaffUserId).HasColumnName("assigned_staff_user_id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Area).WithMany(p => p.Incidents)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_incident_area");

            entity.HasOne(d => d.Category).WithMany(p => p.Incidents)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_incident_category");

            entity.HasOne(d => d.MergedIntoIncident).WithMany(p => p.MergedIncidents)
                .HasForeignKey(d => d.MergedIntoIncidentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_incident_merged_into");

            entity.HasOne(d => d.AssignedStaffUser).WithMany()
                .HasForeignKey(d => d.AssignedStaffUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_incident_assigned_staff");
        });

        modelBuilder.Entity<IncidentReportLink>(entity =>
        {
            entity.HasKey(e => e.IncidentReportLinkId).HasName("incident_report_links_pkey");
            entity.ToTable("incident_report_links", table =>
            {
                table.HasCheckConstraint(
                    "ck_incident_report_link_status",
                    "link_status IN ('Active', 'Unlinked')");
                table.HasCheckConstraint(
                    "ck_incident_report_link_method",
                    "link_method IN ('Created', 'Backfill', 'UserSelected', 'AiSuggested', 'StaffConfirmed')");
                table.HasCheckConstraint(
                    "ck_incident_report_link_role",
                    "link_role IN ('Primary', 'Corroborating')");
                table.HasCheckConstraint(
                    "ck_incident_report_link_unlinked",
                    "(link_status = 'Active' AND unlinked_at IS NULL) OR (link_status = 'Unlinked' AND unlinked_at IS NOT NULL)");
            });

            entity.HasIndex(e => e.IncidentId, "ix_incident_report_links_incident_id");
            entity.HasIndex(e => e.FeedbackId, "uq_incident_report_links_active_feedback")
                .IsUnique()
                .HasFilter("link_status = 'Active'");

            entity.Property(e => e.IncidentReportLinkId).HasDefaultValueSql("gen_random_uuid()").HasColumnName("incident_report_link_id");
            entity.Property(e => e.IncidentId).HasColumnName("incident_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.LinkStatus).HasMaxLength(20).HasDefaultValue("Active").HasColumnName("link_status");
            entity.Property(e => e.LinkMethod).HasMaxLength(30).HasColumnName("link_method");
            entity.Property(e => e.LinkRole).HasMaxLength(30).HasDefaultValue("Corroborating").HasColumnName("link_role");
            entity.Property(e => e.ConfidenceScore).HasPrecision(5, 4).HasColumnName("confidence_score");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.LinkedByUserId).HasColumnName("linked_by_user_id");
            entity.Property(e => e.LinkedAt).HasDefaultValueSql("now()").HasColumnName("linked_at");
            entity.Property(e => e.UnlinkedByUserId).HasColumnName("unlinked_by_user_id");
            entity.Property(e => e.UnlinkedAt).HasColumnName("unlinked_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Incident).WithMany(p => p.IncidentReportLinks)
                .HasForeignKey(d => d.IncidentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_incident_report_link_incident");

            entity.HasOne(d => d.Feedback).WithMany(p => p.IncidentReportLinks)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_incident_report_link_feedback");

            entity.HasOne(d => d.LinkedByUser).WithMany()
                .HasForeignKey(d => d.LinkedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_incident_report_link_linked_by");

            entity.HasOne(d => d.UnlinkedByUser).WithMany()
                .HasForeignKey(d => d.UnlinkedByUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_incident_report_link_unlinked_by");
        });

        modelBuilder.Entity<IncidentEvent>(entity =>
        {
            entity.HasKey(e => e.IncidentEventId).HasName("incident_events_pkey");
            entity.ToTable("incident_events");

            entity.HasIndex(e => new { e.IncidentId, e.CreatedAt }, "ix_incident_events_incident_created_at");

            entity.Property(e => e.IncidentEventId).HasColumnName("incident_event_id");
            entity.Property(e => e.IncidentId).HasColumnName("incident_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.EventType).HasMaxLength(50).HasColumnName("event_type");
            entity.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(e => e.PayloadJson).HasColumnType("jsonb").HasColumnName("payload_json");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");

            entity.HasOne(d => d.Incident).WithMany(p => p.IncidentEvents)
                .HasForeignKey(d => d.IncidentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_incident_event_incident");

            entity.HasOne(d => d.Feedback).WithMany(p => p.IncidentEvents)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_incident_event_feedback");

            entity.HasOne(d => d.ActorUser).WithMany()
                .HasForeignKey(d => d.ActorUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_incident_event_actor");
        });

        modelBuilder.Entity<IncidentSubscription>(entity =>
        {
            entity.HasKey(e => e.IncidentSubscriptionId).HasName("incident_subscriptions_pkey");
            entity.ToTable("incident_subscriptions", table =>
            {
                table.HasCheckConstraint(
                    "ck_incident_subscription_source_type",
                    "source_type IN ('Report', 'Follow', 'Support', 'Backfill', 'Manual')");
            });

            entity.HasIndex(e => new { e.IncidentId, e.UserId }, "uq_incident_subscriptions_incident_user").IsUnique();
            entity.HasIndex(e => new { e.UserId, e.IsActive }, "ix_incident_subscriptions_user_active");

            entity.Property(e => e.IncidentSubscriptionId).HasDefaultValueSql("gen_random_uuid()").HasColumnName("incident_subscription_id");
            entity.Property(e => e.IncidentId).HasColumnName("incident_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.SourceType).HasMaxLength(20).HasColumnName("source_type");
            entity.Property(e => e.SourceFeedbackId).HasColumnName("source_feedback_id");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne(d => d.Incident).WithMany(p => p.IncidentSubscriptions)
                .HasForeignKey(d => d.IncidentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_incident_subscription_incident");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_incident_subscription_user");

            entity.HasOne(d => d.SourceFeedback).WithMany(p => p.IncidentSubscriptions)
                .HasForeignKey(d => d.SourceFeedbackId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_incident_subscription_source_feedback");
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.HasKey(e => e.FeedbackId).HasName("feedbacks_pkey");
            entity.ToTable("feedbacks", table =>
            {
                table.HasCheckConstraint(
                    "ck_feedback_master_has_no_parent",
                    "NOT is_master_ticket OR parent_ticket_id IS NULL");
                table.HasCheckConstraint(
                    "ck_feedback_parent_not_self",
                    "parent_ticket_id IS NULL OR parent_ticket_id <> feedback_id");
            });

            entity.HasIndex(
                    e => new { e.AreaId, e.IsMasterTicket, e.CreatedAt },
                    "ix_feedbacks_duplicate_master_lookup")
                .HasFilter("is_master_ticket = TRUE AND parent_ticket_id IS NULL");

            entity.HasIndex(e => e.AreaId, "IX_feedbacks_area_id");

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
            entity.Property(e => e.SubmissionChannel)
                .HasMaxLength(20)
                .HasDefaultValue("Web")
                .HasColumnName("submission_channel");
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
            entity.ToTable("feedback_duplicate_candidates", table =>
            {
                table.HasCheckConstraint(
                    "ck_feedback_duplicate_candidate_not_self",
                    "feedback_id <> potential_parent_feedback_id");
                table.HasCheckConstraint(
                    "ck_feedback_duplicate_candidate_status",
                    "status IN ('Pending', 'Confirmed', 'Rejected')");
            });

            entity.HasIndex(e => new { e.FeedbackId, e.PotentialParentFeedbackId }, "uq_feedback_duplicate_candidate_pair").IsUnique();

            entity.HasIndex(e => e.Status, "ix_feedback_duplicate_candidates_status");

            entity.HasIndex(
                    e => e.FeedbackId,
                    "uq_feedback_duplicate_candidate_active_child")
                .IsUnique()
                .HasFilter("status IN ('Pending', 'Confirmed')");

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
            entity.Property(e => e.IncidentId).HasColumnName("incident_id");
            entity.Property(e => e.CoordinatorId).HasColumnName("coordinator_id");
            entity.Property(e => e.ReportedByUserId).HasColumnName("reported_by_user_id");
            entity.Property(e => e.ReportStatus).HasMaxLength(50).HasDefaultValueSql("'Reported'::character varying").HasColumnName("report_status");
            entity.Property(e => e.DueDate).HasColumnName("due_date");
            entity.Property(e => e.ReportNote).HasColumnName("report_note");
            entity.Property(e => e.ReportedAt).HasDefaultValueSql("now()").HasColumnName("reported_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.IncidentId, "ux_feedback_provider_reports_incident_id")
                .IsUnique();

            entity.HasOne(d => d.Incident).WithMany(p => p.ProviderAssignments)
                .HasForeignKey(d => d.IncidentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_provider_report_incident");

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
            entity.Property(e => e.IncidentId).HasColumnName("incident_id");
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

            entity.HasOne(d => d.Incident).WithMany(p => p.CompletionDocuments)
                .HasForeignKey(d => d.IncidentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_completion_document_incident");

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
            entity.Property(e => e.IncidentId).HasColumnName("incident_id");
            entity.Property(e => e.ProviderReportId).HasColumnName("provider_report_id");
            entity.Property(e => e.CreatedByStaffUserId).HasColumnName("created_by_staff_user_id");
            entity.Property(e => e.ResolutionSummary).HasMaxLength(500).HasColumnName("resolution_summary");
            entity.Property(e => e.ActionTaken).HasColumnName("action_taken");
            entity.Property(e => e.ResultNote).HasColumnName("result_note");
            entity.Property(e => e.ResolvedAt).HasDefaultValueSql("now()").HasColumnName("resolved_at");
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValueSql("'SubmittedForApproval'::character varying").HasColumnName("status");

            entity.HasIndex(e => e.IncidentId, "ux_feedback_resolutions_incident_id")
                .IsUnique();

            entity.HasOne(d => d.Incident).WithMany(p => p.Resolutions)
                .HasForeignKey(d => d.IncidentId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_resolution_incident");

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
            entity.Property(e => e.IncidentId).HasColumnName("incident_id");
            entity.Property(e => e.Title).HasMaxLength(200).HasColumnName("title");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.Type).HasMaxLength(50).HasColumnName("type");
            entity.Property(e => e.IsRead).HasDefaultValue(false).HasColumnName("is_read");
            entity.Property(e => e.TargetUrl).HasMaxLength(500).HasColumnName("target_url");
            entity.Property(e => e.TargetType).HasMaxLength(50).HasColumnName("target_type");
            entity.Property(e => e.TargetId).HasMaxLength(100).HasColumnName("target_id");
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

            entity.HasOne(d => d.Incident).WithMany()
                .HasForeignKey(d => d.IncidentId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_notification_incident");
        });

        modelBuilder.Entity<MessengerFeedbackConversation>(entity =>
        {
            entity.HasKey(e => e.ConversationId)
                .HasName("messenger_feedback_conversations_pkey");
            entity.ToTable("messenger_feedback_conversations");

            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.PageId).HasMaxLength(100).HasColumnName("page_id");
            entity.Property(e => e.SenderPsid).HasMaxLength(100).HasColumnName("sender_psid");
            entity.Property(e => e.State).HasMaxLength(50).HasColumnName("state");
            entity.Property(e => e.Title).HasMaxLength(200).HasColumnName("title");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.LocationText).HasMaxLength(500).HasColumnName("location_text");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.LastMessageId).HasMaxLength(200).HasColumnName("last_message_id");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasIndex(e => new { e.PageId, e.SenderPsid })
                .IsUnique()
                .HasDatabaseName("uq_messenger_feedback_conversations_page_sender");

            entity.HasIndex(e => e.FeedbackId)
                .HasDatabaseName("ix_messenger_feedback_conversations_feedback_id");

            entity.HasOne(d => d.Area)
                .WithMany(p => p.MessengerFeedbackConversations)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_messenger_feedback_conversation_area");

            entity.HasOne(d => d.Feedback)
                .WithMany(p => p.MessengerFeedbackConversations)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_messenger_feedback_conversation_feedback");
        });

        modelBuilder.Entity<MessengerFeedbackSubmission>(entity =>
        {
            entity.HasKey(e => e.SubmissionId)
                .HasName("messenger_feedback_submissions_pkey");
            entity.ToTable("messenger_feedback_submissions");

            entity.Property(e => e.SubmissionId).HasColumnName("submission_id");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.HasIndex(e => new { e.ConversationId, e.CreatedAt })
                .HasDatabaseName("ix_messenger_feedback_submissions_conversation_created_at");

            entity.HasIndex(e => e.FeedbackId)
                .IsUnique()
                .HasDatabaseName("uq_messenger_feedback_submissions_feedback_id");

            entity.HasOne(d => d.Conversation)
                .WithMany(p => p.Submissions)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_messenger_feedback_submission_conversation");

            entity.HasOne(d => d.Feedback)
                .WithMany(p => p.MessengerFeedbackSubmissions)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_messenger_feedback_submission_feedback");
        });

        modelBuilder.Entity<MessengerFeedbackDraftAttachment>(entity =>
        {
            entity.HasKey(e => e.DraftAttachmentId)
                .HasName("messenger_feedback_draft_attachments_pkey");
            entity.ToTable("messenger_feedback_draft_attachments");

            entity.Property(e => e.DraftAttachmentId).HasColumnName("draft_attachment_id");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.SourceUrl).HasMaxLength(2000).HasColumnName("source_url");
            entity.Property(e => e.FileType).HasMaxLength(100).HasColumnName("file_type");
            entity.Property(e => e.SourceMessageId).HasMaxLength(200).HasColumnName("source_message_id");
            entity.Property(e => e.SourceOrdinal).HasColumnName("source_ordinal");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.HasIndex(e => new { e.ConversationId, e.CreatedAt })
                .HasDatabaseName("ix_messenger_feedback_draft_attachments_conversation_created_at");

            entity.HasIndex(e => new { e.ConversationId, e.SourceMessageId, e.SourceOrdinal })
                .IsUnique()
                .HasDatabaseName("uq_messenger_feedback_draft_attachments_message_ordinal");

            entity.HasOne(d => d.Conversation)
                .WithMany(p => p.DraftAttachments)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_messenger_feedback_draft_attachment_conversation");
        });

        modelBuilder.Entity<ZaloFeedbackConversation>(entity =>
        {
            entity.HasKey(e => e.ConversationId)
                .HasName("zalo_feedback_conversations_pkey");
            entity.ToTable("zalo_feedback_conversations");

            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.OaId).HasMaxLength(100).HasColumnName("oa_id");
            entity.Property(e => e.SenderUserId).HasMaxLength(100).HasColumnName("sender_user_id");
            entity.Property(e => e.State).HasMaxLength(50).HasColumnName("state");
            entity.Property(e => e.Title).HasMaxLength(200).HasColumnName("title");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.LocationText).HasMaxLength(500).HasColumnName("location_text");
            entity.Property(e => e.Latitude).HasPrecision(10, 7).HasColumnName("latitude");
            entity.Property(e => e.Longitude).HasPrecision(10, 7).HasColumnName("longitude");
            entity.Property(e => e.AreaId).HasColumnName("area_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.LastMessageId).HasMaxLength(200).HasColumnName("last_message_id");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasIndex(e => new { e.OaId, e.SenderUserId })
                .IsUnique()
                .HasDatabaseName("uq_zalo_feedback_conversations_oa_sender");

            entity.HasIndex(e => e.FeedbackId)
                .HasDatabaseName("ix_zalo_feedback_conversations_feedback_id");

            entity.HasOne(d => d.Area)
                .WithMany(p => p.ZaloFeedbackConversations)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_zalo_feedback_conversation_area");

            entity.HasOne(d => d.Feedback)
                .WithMany(p => p.ZaloFeedbackConversations)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_zalo_feedback_conversation_feedback");
        });

        modelBuilder.Entity<ZaloFeedbackSubmission>(entity =>
        {
            entity.HasKey(e => e.SubmissionId)
                .HasName("zalo_feedback_submissions_pkey");
            entity.ToTable("zalo_feedback_submissions");

            entity.Property(e => e.SubmissionId).HasColumnName("submission_id");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.FeedbackId).HasColumnName("feedback_id");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.HasIndex(e => new { e.ConversationId, e.CreatedAt })
                .HasDatabaseName("ix_zalo_feedback_submissions_conversation_created_at");

            entity.HasIndex(e => e.FeedbackId)
                .IsUnique()
                .HasDatabaseName("uq_zalo_feedback_submissions_feedback_id");

            entity.HasOne(d => d.Conversation)
                .WithMany(p => p.Submissions)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_zalo_feedback_submission_conversation");

            entity.HasOne(d => d.Feedback)
                .WithMany(p => p.ZaloFeedbackSubmissions)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_zalo_feedback_submission_feedback");
        });

        modelBuilder.Entity<ZaloFeedbackDraftAttachment>(entity =>
        {
            entity.HasKey(e => e.DraftAttachmentId)
                .HasName("zalo_feedback_draft_attachments_pkey");
            entity.ToTable("zalo_feedback_draft_attachments");

            entity.Property(e => e.DraftAttachmentId).HasColumnName("draft_attachment_id");
            entity.Property(e => e.ConversationId).HasColumnName("conversation_id");
            entity.Property(e => e.SourceUrl).HasMaxLength(2000).HasColumnName("source_url");
            entity.Property(e => e.FileType).HasMaxLength(100).HasColumnName("file_type");
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");

            entity.HasIndex(e => new { e.ConversationId, e.CreatedAt })
                .HasDatabaseName("ix_zalo_feedback_draft_attachments_conversation_created_at");

            entity.HasOne(d => d.Conversation)
                .WithMany(p => p.DraftAttachments)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_zalo_feedback_draft_attachment_conversation");
        });

        modelBuilder.Entity<ZaloWebhookEvent>(entity =>
        {
            entity.HasKey(e => e.WebhookEventId)
                .HasName("zalo_webhook_events_pkey");
            entity.ToTable("zalo_webhook_events");

            entity.Property(e => e.WebhookEventId).HasColumnName("webhook_event_id");
            entity.Property(e => e.EventKey).HasMaxLength(64).HasColumnName("event_key");
            entity.Property(e => e.Payload).HasColumnName("payload");
            entity.Property(e => e.Status).HasMaxLength(20).HasColumnName("status");
            entity.Property(e => e.AttemptCount).HasColumnName("attempt_count");
            entity.Property(e => e.LastError).HasMaxLength(2000).HasColumnName("last_error");
            entity.Property(e => e.ReceivedAt)
                .HasColumnType("timestamp without time zone")
                .HasDefaultValueSql("now()")
                .HasColumnName("received_at");
            entity.Property(e => e.ProcessedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("processed_at");

            entity.HasIndex(e => e.EventKey)
                .IsUnique()
                .HasDatabaseName("uq_zalo_webhook_events_event_key");
            entity.HasIndex(e => new { e.Status, e.ReceivedAt })
                .HasDatabaseName("ix_zalo_webhook_events_status_received_at");
        });

        modelBuilder.Entity<ZaloOauthCredential>(entity =>
        {
            entity.HasKey(e => e.OaId)
                .HasName("zalo_oauth_credentials_pkey");
            entity.ToTable("zalo_oauth_credentials");

            entity.Property(e => e.OaId).HasMaxLength(100).HasColumnName("oa_id");
            entity.Property(e => e.AccessTokenCiphertext).HasColumnName("access_token_ciphertext");
            entity.Property(e => e.RefreshTokenCiphertext).HasColumnName("refresh_token_ciphertext");
            entity.Property(e => e.AccessTokenExpiresAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("access_token_expires_at");
            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");
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

        modelBuilder.Entity<SlaPolicy>(entity =>
        {
            entity.HasKey(e => e.SlaPolicyId)
                .HasName("sla_policies_pkey");

            entity.ToTable("sla_policies");

            entity.Property(e => e.SlaPolicyId)
                .HasColumnName("sla_policy_id");

            entity.Property(e => e.PolicyName)
                .HasMaxLength(200)
                .HasColumnName("policy_name");

            entity.Property(e => e.AreaId)
                .HasColumnName("area_id");

            entity.Property(e => e.CategoryId)
                .HasColumnName("category_id");

            entity.Property(e => e.Priority)
                .HasMaxLength(20)
                .HasColumnName("priority");

            entity.Property(e => e.ResponseTimeMinutes)
                .HasColumnName("response_time_minutes");

            entity.Property(e => e.ResolutionTimeMinutes)
                .HasColumnName("resolution_time_minutes");

            entity.Property(e => e.EffectiveFrom)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("effective_from");

            entity.Property(e => e.EffectiveTo)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("effective_to");

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");

            entity.Property(e => e.CreatedByUserId)
                .HasColumnName("created_by_user_id");

            entity.Property(e => e.UpdatedByUserId)
                .HasColumnName("updated_by_user_id");

            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasIndex(e => new
            {
                e.AreaId,
                e.CategoryId,
                e.Priority,
                e.IsActive
            })
            .HasDatabaseName("ix_sla_policies_lookup");

            entity.HasOne(d => d.Area)
                .WithMany(p => p.SlaPolicies)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_sla_policies_operating_areas");

            entity.HasOne(d => d.Category)
                .WithMany(p => p.SlaPolicies)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_sla_policies_categories");

            entity.HasOne(d => d.CreatedByUser)
                .WithMany(p => p.CreatedSlaPolicies)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_sla_policies_created_by_user");

            entity.HasOne(d => d.UpdatedByUser)
                .WithMany(p => p.UpdatedSlaPolicies)
                .HasForeignKey(d => d.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_sla_policies_updated_by_user");
        });

        modelBuilder.Entity<FeedbackSla>(entity =>
        {
            entity.HasKey(e => e.FeedbackSlaId)
                .HasName("feedback_slas_pkey");

            entity.ToTable("feedback_slas");

            entity.Property(e => e.FeedbackSlaId)
                .HasColumnName("feedback_sla_id");

            entity.Property(e => e.FeedbackId)
                .HasColumnName("feedback_id");

            entity.Property(e => e.SlaPolicyId)
                .HasColumnName("sla_policy_id");

            entity.Property(e => e.AreaId)
                .HasColumnName("area_id");

            entity.Property(e => e.CategoryId)
                .HasColumnName("category_id");

            entity.Property(e => e.Priority)
                .HasMaxLength(20)
                .HasColumnName("priority");

            entity.Property(e => e.StartedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("started_at");

            entity.Property(e => e.ResponseDueAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("response_due_at");

            entity.Property(e => e.ResolutionDueAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("resolution_due_at");

            entity.Property(e => e.RespondedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("responded_at");

            entity.Property(e => e.ResolvedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("resolved_at");

            entity.Property(e => e.TotalPausedMinutes)
                .HasDefaultValue(0)
                .HasColumnName("total_paused_minutes");

            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasColumnName("status");

            entity.Property(e => e.ResponseStatus)
                .HasMaxLength(30)
                .HasColumnName("response_status");

            entity.Property(e => e.ResolutionStatus)
                .HasMaxLength(30)
                .HasColumnName("resolution_status");

            entity.Property(e => e.IsResponseBreached)
                .HasDefaultValue(false)
                .HasColumnName("is_response_breached");

            entity.Property(e => e.IsResolutionBreached)
                .HasDefaultValue(false)
                .HasColumnName("is_resolution_breached");

            entity.Property(e => e.IsCurrent)
                .HasDefaultValue(true)
                .HasColumnName("is_current");

            entity.Property(e => e.StartedByUserId)
                .HasColumnName("started_by_user_id");

            entity.Property(e => e.CompletedByUserId)
                .HasColumnName("completed_by_user_id");

            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasIndex(e => e.FeedbackId)
                .HasDatabaseName("ix_feedback_slas_feedback_id");

            entity.HasIndex(e => new
            {
                e.Status,
                e.ResponseDueAt,
                e.ResolutionDueAt
            })
            .HasDatabaseName("ix_feedback_slas_monitoring");

            entity.HasIndex(e => e.FeedbackId)
                .IsUnique()
                .HasFilter("is_current = true")
                .HasDatabaseName("ux_feedback_slas_current_feedback");

            entity.HasOne(d => d.Feedback)
                .WithMany(p => p.FeedbackSlas)
                .HasForeignKey(d => d.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_feedback_slas_feedbacks");

            entity.HasOne(d => d.SlaPolicy)
                .WithMany(p => p.FeedbackSlas)
                .HasForeignKey(d => d.SlaPolicyId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_slas_sla_policies");

            entity.HasOne(d => d.Area)
                .WithMany(p => p.FeedbackSlas)
                .HasForeignKey(d => d.AreaId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_slas_operating_areas");

            entity.HasOne(d => d.Category)
                .WithMany(p => p.FeedbackSlas)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_slas_categories");

            entity.HasOne(d => d.StartedByUser)
                .WithMany(p => p.StartedFeedbackSlas)
                .HasForeignKey(d => d.StartedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_slas_started_by_user");

            entity.HasOne(d => d.CompletedByUser)
                .WithMany(p => p.CompletedFeedbackSlas)
                .HasForeignKey(d => d.CompletedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_feedback_slas_completed_by_user");
        });

        modelBuilder.Entity<SlaEvent>(entity =>
        {
            entity.HasKey(e => e.SlaEventId)
                .HasName("sla_events_pkey");

            entity.ToTable("sla_events");

            entity.Property(e => e.SlaEventId)
                .HasColumnName("sla_event_id");

            entity.Property(e => e.FeedbackSlaId)
                .HasColumnName("feedback_sla_id");

            entity.Property(e => e.EventType)
                .HasMaxLength(50)
                .HasColumnName("event_type");

            entity.Property(e => e.OldStatus)
                .HasMaxLength(30)
                .HasColumnName("old_status");

            entity.Property(e => e.NewStatus)
                .HasMaxLength(30)
                .HasColumnName("new_status");

            entity.Property(e => e.Note)
                .HasMaxLength(1000)
                .HasColumnName("note");

            entity.Property(e => e.TriggeredByUserId)
                .HasColumnName("triggered_by_user_id");

            entity.Property(e => e.TriggerSource)
                .HasMaxLength(20)
                .HasColumnName("trigger_source");

            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.HasIndex(e => new
            {
                e.FeedbackSlaId,
                e.CreatedAt
            })
            .HasDatabaseName("ix_sla_events_feedback_sla_created_at");

            entity.HasOne(d => d.FeedbackSla)
                .WithMany(p => p.SlaEvents)
                .HasForeignKey(d => d.FeedbackSlaId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_sla_events_feedback_slas");

            entity.HasOne(d => d.TriggeredByUser)
                .WithMany(p => p.TriggeredSlaEvents)
                .HasForeignKey(d => d.TriggeredByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_sla_events_triggered_by_user");
        });

        modelBuilder.Entity<SlaPauseHistory>(entity =>
        {
            entity.HasKey(e => e.SlaPauseHistoryId)
                .HasName("sla_pause_histories_pkey");

            entity.ToTable("sla_pause_histories");

            entity.Property(e => e.SlaPauseHistoryId)
                .HasColumnName("sla_pause_history_id");

            entity.Property(e => e.FeedbackSlaId)
                .HasColumnName("feedback_sla_id");

            entity.Property(e => e.ReasonCode)
                .HasMaxLength(50)
                .HasColumnName("reason_code");

            entity.Property(e => e.ReasonNote)
                .HasMaxLength(1000)
                .HasColumnName("reason_note");

            entity.Property(e => e.PausedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("paused_at");

            entity.Property(e => e.ResumedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("resumed_at");

            entity.Property(e => e.PausedMinutes)
                .HasColumnName("paused_minutes");

            entity.Property(e => e.PausedByUserId)
                .HasColumnName("paused_by_user_id");

            entity.Property(e => e.ResumedByUserId)
                .HasColumnName("resumed_by_user_id");

            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("created_at");

            entity.Property(e => e.UpdatedAt)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("updated_at");

            entity.HasIndex(e => e.FeedbackSlaId)
                .HasDatabaseName("ix_sla_pause_histories_feedback_sla_id");

            entity.HasOne(d => d.FeedbackSla)
                .WithMany(p => p.SlaPauseHistories)
                .HasForeignKey(d => d.FeedbackSlaId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_sla_pause_histories_feedback_slas");

            entity.HasOne(d => d.PausedByUser)
                .WithMany(p => p.PausedSlaHistories)
                .HasForeignKey(d => d.PausedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_sla_pause_histories_paused_by_user");

            entity.HasOne(d => d.ResumedByUser)
                .WithMany(p => p.ResumedSlaHistories)
                .HasForeignKey(d => d.ResumedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_sla_pause_histories_resumed_by_user");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
