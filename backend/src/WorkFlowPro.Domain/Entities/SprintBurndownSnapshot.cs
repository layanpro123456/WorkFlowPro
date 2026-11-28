using WorkFlowPro.Domain.Common;

namespace WorkFlowPro.Domain.Entities;

public class SprintBurndownSnapshot : BaseEntity
{
    public Guid SprintId { get; private set; }
    public DateOnly SnapshotDate { get; private set; }
    public int TotalPoints { get; private set; }
    public int RemainingPoints { get; private set; }
    public int CompletedPoints { get; private set; }
    public int RemainingTaskCount { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }

    public Sprint? Sprint { get; private set; }

    private SprintBurndownSnapshot() { } // For EF Core

    public SprintBurndownSnapshot(
        Guid sprintId,
        DateOnly snapshotDate,
        int totalPoints,
        int remainingPoints,
        int completedPoints,
        int remainingTaskCount,
        DateTime recordedAtUtc)
    {
        if (sprintId == Guid.Empty) throw new ArgumentException("SprintId cannot be empty.", nameof(sprintId));

        Id = Guid.NewGuid();
        SprintId = sprintId;
        SnapshotDate = snapshotDate;
        TotalPoints = totalPoints;
        RemainingPoints = remainingPoints;
        CompletedPoints = completedPoints;
        RemainingTaskCount = remainingTaskCount;
        RecordedAtUtc = recordedAtUtc;
        CreatedAtUtc = recordedAtUtc;
    }

    public void UpdateCounts(
        int totalPoints,
        int remainingPoints,
        int completedPoints,
        int remainingTaskCount,
        DateTime recordedAtUtc)
    {
        TotalPoints = totalPoints;
        RemainingPoints = remainingPoints;
        CompletedPoints = completedPoints;
        RemainingTaskCount = remainingTaskCount;
        RecordedAtUtc = recordedAtUtc;
        UpdatedAtUtc = recordedAtUtc;
    }
}
