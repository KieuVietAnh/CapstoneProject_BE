using Microsoft.EntityFrameworkCore;
using UrbanService.DAL.Data;
using UrbanService.DAL.Entities;
using Xunit;

namespace UrbanService.BLL.Tests;

public sealed class IncidentProviderWorkflowModelTests
{
    [Fact]
    public void ProviderWorkflow_IsIncidentBasedAndHasOneAssignmentPerIncident()
    {
        var options = new DbContextOptionsBuilder<UrbanServiceDbContext>()
            .UseNpgsql("Host=localhost;Database=model-test;Username=test;Password=test")
            .Options;
        using var dbContext = new UrbanServiceDbContext(options);

        var assignment = dbContext.Model.FindEntityType(typeof(FeedbackProviderReport))!;
        var assignmentIncident = assignment.FindProperty(nameof(FeedbackProviderReport.IncidentId));
        Assert.NotNull(assignmentIncident);
        Assert.False(assignmentIncident!.IsNullable);
        Assert.Contains(
            assignment.GetIndexes(),
            index => index.IsUnique &&
                index.Properties.Single().Name == nameof(FeedbackProviderReport.IncidentId));

        var resolution = dbContext.Model.FindEntityType(typeof(FeedbackResolution))!;
        Assert.NotNull(resolution.FindProperty(nameof(FeedbackResolution.IncidentId)));

        var document = dbContext.Model.FindEntityType(typeof(CompletionDocument))!;
        Assert.NotNull(document.FindProperty(nameof(CompletionDocument.IncidentId)));
    }
}
