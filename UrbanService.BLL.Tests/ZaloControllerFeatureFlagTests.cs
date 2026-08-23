using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using UrbanService.BLL.Interfaces;
using UrbanService.Controllers;
using Xunit;

namespace UrbanService.BLL.Tests;

public sealed class ZaloControllerFeatureFlagTests
{
    [Fact]
    public async Task ReceiveWebhook_WhenZaloDisabled_ReturnsNotFoundWithoutProcessing()
    {
        var zaloService = Substitute.For<IZaloService>();
        var inbox = Substitute.For<IZaloWebhookInbox>();
        var queue = Substitute.For<IZaloWebhookQueue>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Zalo:Enabled"] = "false"
            })
            .Build();
        var controller = new ZaloController(zaloService, inbox, queue, configuration);

        var result = await controller.ReceiveWebhook(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        zaloService.DidNotReceiveWithAnyArgs().IsSignatureValid(default!, default);
        await inbox.DidNotReceiveWithAnyArgs().StoreAsync(default!, default);
        await queue.DidNotReceiveWithAnyArgs().EnqueueAsync(default, default);
    }
}
