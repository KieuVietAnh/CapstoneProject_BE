using UrbanService.BLL.Dtos;

namespace UrbanService.BLL.Interfaces;

public interface IEmailSender
{
    Task SendAsync(EmailMessageDto message, CancellationToken cancellationToken = default);
}
