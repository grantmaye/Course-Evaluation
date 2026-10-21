using Microsoft.EntityFrameworkCore;
namespace Evaluation.Api;

public static class DemoData
{
    public static async Task SeedAsync(EvaluationDb db)
    {
        // Small, fixed IDs make repeated startup safe without dropping a database or overwriting user data.
        var fixtures = new[]
        {
            Row(1, 1, 101, 5, "2025-01-01T00:00:00Z"),
            Row(2, 1, 101, 3, "2025-05-01T00:00:00Z"),
            Row(3, 1, 102, 1, "2025-12-31T23:59:59Z"),
            Row(4, 2, 201, 5, "2025-05-01T00:00:00Z"),
            Row(5, 1, 101, 2, "2026-01-01T00:00:00Z")
        };
        foreach (var row in fixtures)
            if (!await db.Responses.AnyAsync(x => x.Id == row.Id)) db.Responses.Add(row);
        await db.SaveChangesAsync();
    }
    private static EvaluationResponse Row(int id, int institution, int course, int rating, string timestamp) => new()
    {
        Id = Guid.Parse($"00000000-0000-0000-0000-{id:000000000000}"),
        InstitutionId = institution, CourseId = course, Rating = rating,
        SubmittedAtUtc = DateTime.Parse(timestamp, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal)
    };
}
