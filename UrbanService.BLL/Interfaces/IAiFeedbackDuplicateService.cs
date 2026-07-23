using UrbanService.DAL.Entities;

namespace UrbanService.BLL.Interfaces;

public interface IAiFeedbackDuplicateService
{
    Task CheckAndLinkDuplicateAsync(Feedback feedback, Guid reviewedByUserId);
}