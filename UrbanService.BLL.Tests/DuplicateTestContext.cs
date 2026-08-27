using NSubstitute;
using UrbanService.BLL.Common.Constraint;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Tests;

internal sealed class DuplicateTestContext
{
    public DuplicateTestContext()
    {
        ManagerUserId = Guid.NewGuid();
        var managerRole = new Role
        {
            RoleId = 3,
            RoleName = UserRole.INTERACTIONMANAGER
        };
        Users.Add(new User
        {
            UserId = ManagerUserId,
            RoleId = managerRole.RoleId,
            Role = managerRole,
            FullName = "Test Manager",
            Email = "manager@example.test",
            PasswordHash = "test",
            IsActive = true,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        });

        foreach (var areaId in new[] { 1, 2 })
        {
            var area = new OperatingArea
            {
                AreaId = areaId,
                AreaName = $"Area {areaId}",
                AreaType = "Ward",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            ManagerAreaAssignments.Add(new ManagerAreaAssignment
            {
                ManagerAreaAssignmentId = areaId,
                ManagerUserId = ManagerUserId,
                AreaId = areaId,
                Area = area,
                CreatedByUserId = ManagerUserId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        FeedbackRepository.Entities.Returns(_ => Feedbacks.AsAsyncQueryable());
        CandidateRepository.Entities.Returns(_ => Candidates.AsAsyncQueryable());
        UserRepository.Entities.Returns(_ => Users.AsAsyncQueryable());
        ManagerAreaAssignmentRepository.Entities.Returns(
            _ => ManagerAreaAssignments.AsAsyncQueryable());
        IncidentReportLinkRepository.Entities.Returns(
            _ => IncidentReportLinks.AsAsyncQueryable());

        CandidateRepository.AddAsync(Arg.Any<FeedbackDuplicateCandidate>())
            .Returns(call =>
            {
                var candidate = call.Arg<FeedbackDuplicateCandidate>();
                if (candidate.DuplicateCandidateId == Guid.Empty)
                {
                    candidate.DuplicateCandidateId = Guid.NewGuid();
                }

                candidate.Feedback = Feedbacks.Single(f => f.FeedbackId == candidate.FeedbackId);
                candidate.PotentialParentFeedback = Feedbacks.Single(
                    f => f.FeedbackId == candidate.PotentialParentFeedbackId);
                Candidates.Add(candidate);
                return Task.CompletedTask;
            });

        UnitOfWork.GetRepository<Feedback>().Returns(FeedbackRepository);
        UnitOfWork.GetRepository<FeedbackDuplicateCandidate>().Returns(CandidateRepository);
        UnitOfWork.GetRepository<User>().Returns(UserRepository);
        UnitOfWork.GetRepository<ManagerAreaAssignment>()
            .Returns(ManagerAreaAssignmentRepository);
        UnitOfWork.GetRepository<IncidentReportLink>()
            .Returns(IncidentReportLinkRepository);
        UnitOfWork.SaveAsync().Returns(Task.CompletedTask);
        UnitOfWork.AcquireTransactionAdvisoryLockAsync(Arg.Any<long>())
            .Returns(Task.CompletedTask);
    }

    public List<Feedback> Feedbacks { get; } = [];

    public List<FeedbackDuplicateCandidate> Candidates { get; } = [];

    public List<User> Users { get; } = [];

    public List<ManagerAreaAssignment> ManagerAreaAssignments { get; } = [];

    public List<IncidentReportLink> IncidentReportLinks { get; } = [];

    public Guid ManagerUserId { get; }

    public User AddActor(string roleName, string fullName = "Test Actor")
    {
        var role = new Role
        {
            RoleId = Users.Count + 10,
            RoleName = roleName
        };
        var user = new User
        {
            UserId = Guid.NewGuid(),
            RoleId = role.RoleId,
            Role = role,
            FullName = fullName,
            Email = $"{Guid.NewGuid():N}@example.test",
            PasswordHash = "test",
            IsActive = true,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        };
        Users.Add(user);
        return user;
    }

    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public IGenericRepository<Feedback> FeedbackRepository { get; } =
        Substitute.For<IGenericRepository<Feedback>>();

    public IGenericRepository<FeedbackDuplicateCandidate> CandidateRepository { get; } =
        Substitute.For<IGenericRepository<FeedbackDuplicateCandidate>>();

    public IGenericRepository<User> UserRepository { get; } =
        Substitute.For<IGenericRepository<User>>();

    public IGenericRepository<ManagerAreaAssignment> ManagerAreaAssignmentRepository { get; } =
        Substitute.For<IGenericRepository<ManagerAreaAssignment>>();

    public IGenericRepository<IncidentReportLink> IncidentReportLinkRepository { get; } =
        Substitute.For<IGenericRepository<IncidentReportLink>>();

    public static Feedback Feedback(
        Guid id,
        DateTime createdAt,
        bool isMaster,
        string status = "Verified",
        int areaId = 1,
        decimal? latitude = 10.762622m,
        decimal? longitude = 106.660172m,
        Guid? parentTicketId = null)
    {
        return new Feedback
        {
            FeedbackId = id,
            UserId = Guid.NewGuid(),
            AreaId = areaId,
            Title = $"Feedback {id}",
            Description = "Cung mot su co tai cung vi tri",
            LocationText = "Quan 1",
            Latitude = latitude,
            Longitude = longitude,
            Status = status,
            IsMasterTicket = isMaster,
            ParentTicketId = parentTicketId,
            CreatedAt = createdAt
        };
    }

    public FeedbackDuplicateCandidate Candidate(
        Feedback child,
        Feedback parent,
        string status = "Pending")
    {
        TrackActiveIncident(child);
        TrackActiveIncident(parent);

        var candidate = new FeedbackDuplicateCandidate
        {
            DuplicateCandidateId = Guid.NewGuid(),
            FeedbackId = child.FeedbackId,
            PotentialParentFeedbackId = parent.FeedbackId,
            Feedback = child,
            PotentialParentFeedback = parent,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        Candidates.Add(candidate);
        return candidate;
    }

    public IncidentReportLink TrackActiveIncident(
        Feedback feedback,
        Guid? assignedStaffUserId = null,
        string? incidentStatus = null)
    {
        var existingLink = feedback.IncidentReportLinks.FirstOrDefault(link =>
            link.LinkStatus == IncidentLinkStatus.Active);
        if (existingLink is not null)
        {
            if (!IncidentReportLinks.Contains(existingLink))
            {
                IncidentReportLinks.Add(existingLink);
            }

            existingLink.Incident.AssignedStaffUserId = assignedStaffUserId;
            if (incidentStatus is not null)
            {
                existingLink.Incident.Status = incidentStatus;
            }

            return existingLink;
        }

        var incident = new Incident
        {
            IncidentId = Guid.NewGuid(),
            AreaId = feedback.AreaId,
            Title = feedback.Title,
            LocationText = feedback.LocationText,
            Status = feedback.Status,
            AssignedStaffUserId = assignedStaffUserId,
            CreatedAt = feedback.CreatedAt
        };
        if (incidentStatus is not null)
        {
            incident.Status = incidentStatus;
        }
        var link = new IncidentReportLink
        {
            IncidentReportLinkId = Guid.NewGuid(),
            IncidentId = incident.IncidentId,
            Incident = incident,
            FeedbackId = feedback.FeedbackId,
            Feedback = feedback,
            LinkStatus = IncidentLinkStatus.Active,
            LinkMethod = IncidentLinkMethod.Created,
            LinkRole = IncidentLinkRole.Primary,
            LinkedAt = feedback.CreatedAt
        };
        feedback.IncidentReportLinks.Add(link);
        incident.IncidentReportLinks.Add(link);
        IncidentReportLinks.Add(link);
        return link;
    }
}
