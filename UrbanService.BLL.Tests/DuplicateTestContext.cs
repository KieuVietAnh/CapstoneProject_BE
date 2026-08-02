using NSubstitute;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Tests;

internal sealed class DuplicateTestContext
{
    public DuplicateTestContext()
    {
        FeedbackRepository.Entities.Returns(_ => Feedbacks.AsAsyncQueryable());
        CandidateRepository.Entities.Returns(_ => Candidates.AsAsyncQueryable());

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
        UnitOfWork.SaveAsync().Returns(Task.CompletedTask);
        UnitOfWork.AcquireTransactionAdvisoryLockAsync(Arg.Any<long>())
            .Returns(Task.CompletedTask);
    }

    public List<Feedback> Feedbacks { get; } = [];

    public List<FeedbackDuplicateCandidate> Candidates { get; } = [];

    public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    public IGenericRepository<Feedback> FeedbackRepository { get; } =
        Substitute.For<IGenericRepository<Feedback>>();

    public IGenericRepository<FeedbackDuplicateCandidate> CandidateRepository { get; } =
        Substitute.For<IGenericRepository<FeedbackDuplicateCandidate>>();

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
}
