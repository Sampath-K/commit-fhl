namespace CommitApi.Models.Capacity;

/// <summary>A calendar free slot available for focus or rescheduling work.</summary>
/// <param name="Start">Slot start (UTC).</param>
/// <param name="End">Slot end (UTC).</param>
public record TimeSlot(DateTimeOffset Start, DateTimeOffset End);

/// <summary>
/// Wellbeing and capacity snapshot for a user, derived from Viva Insights
/// activity statistics and calendar data.
/// </summary>
/// <param name="UserId">Hashed (pseudonymized) user identifier.</param>
/// <param name="LoadIndex">Ratio of actual work hours to expected (1.0 = 100% load).</param>
/// <param name="BurnoutTrend">Signed delta vs last week (+ve = increasing load).</param>
/// <param name="FreeSlots">Available 2-hour+ slots in next 3 days for focus/rescheduling.</param>
public record CapacitySnapshot(
    string UserId,
    double LoadIndex,
    double BurnoutTrend,
    IReadOnlyList<TimeSlot> FreeSlots);
