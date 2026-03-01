using HnVue.Console.Models;

namespace HnVue.Console.Services;

/// <summary>
/// Mock worklist service for development.
/// TODO: Replace with gRPC adapter when ready.
/// </summary>
internal class MockWorklistService : IWorklistService
{
    public Task<IReadOnlyList<WorklistItem>> GetWorklistAsync(CancellationToken ct)
    {
        var items = GenerateMockWorklistItems();
        return Task.FromResult<IReadOnlyList<WorklistItem>>(items);
    }

    public Task<WorklistRefreshResult> RefreshWorklistAsync(WorklistRefreshRequest request, CancellationToken ct)
    {
        var items = GenerateMockWorklistItems();

        // Filter by date if specified
        if (request.Since.HasValue)
        {
            items = items.Where(i => i.ScheduledDateTime > request.Since.Value)
                .ToList();
        }

        return Task.FromResult(new WorklistRefreshResult
        {
            Items = items,
            RefreshedAt = DateTimeOffset.Now
        });
    }

    public Task SelectWorklistItemAsync(string procedureId, CancellationToken ct)
    {
        // Simulate successful selection
        return Task.CompletedTask;
    }

    private static List<WorklistItem> GenerateMockWorklistItems()
    {
        var now = DateTimeOffset.Now;
        return new List<WorklistItem>
        {
            new WorklistItem
            {
                ProcedureId = "PROC001",
                PatientId = "P001",
                PatientName = "Hong Gil-dong",
                AccessionNumber = "A001",
                ScheduledProcedureStepDescription = "Chest PA",
                ScheduledDateTime = now.AddHours(1),
                BodyPart = "Chest",
                Projection = "PA",
                Status = WorklistStatus.Scheduled
            },
            new WorklistItem
            {
                ProcedureId = "PROC002",
                PatientId = "P002",
                PatientName = "Kim Cheol-su",
                AccessionNumber = "A002",
                ScheduledProcedureStepDescription = "Chest LAT",
                ScheduledDateTime = now.AddHours(2),
                BodyPart = "Chest",
                Projection = "Lateral",
                Status = WorklistStatus.Scheduled
            },
            new WorklistItem
            {
                ProcedureId = "PROC003",
                PatientId = "P003",
                PatientName = "Lee Min-ji",
                AccessionNumber = "A003",
                ScheduledProcedureStepDescription = "Abdomen AP",
                ScheduledDateTime = now.AddHours(3),
                BodyPart = "Abdomen",
                Projection = "AP",
                Status = WorklistStatus.InProgress
            },
            new WorklistItem
            {
                ProcedureId = "PROC004",
                PatientId = "P004",
                PatientName = "Park So-dam",
                AccessionNumber = "A004",
                ScheduledProcedureStepDescription = "Knee AP Lateral",
                ScheduledDateTime = now.AddDays(-1),
                BodyPart = "Knee",
                Projection = "AP & Lateral",
                Status = WorklistStatus.Completed
            }
        };
    }
}
