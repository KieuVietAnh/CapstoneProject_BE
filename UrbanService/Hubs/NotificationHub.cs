using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace UrbanService.Hubs;

[Authorize]
public class NotificationHub : Hub
{
}
