using Microsoft.EntityFrameworkCore;
namespace Evaluation.Api;

public sealed class ReportService(EvaluationDb db)
{
    public async Task<EvaluationReport> GetAsync(int institutionId, int year, CancellationToken cancellationToken)
    {
        var start = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddYears(1);
        // Half-open UTC range preserves year boundaries and avoids applying YEAR() to the indexed column.
        // This is a query design choice, not an unmeasured performance claim.
        var aggregates = await db.Responses.AsNoTracking()
            .Where(x => x.InstitutionId == institutionId && x.SubmittedAtUtc >= start && x.SubmittedAtUtc < end)
            .GroupBy(x => x.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count(), Total = g.Sum(x => x.Rating) })
            .OrderBy(x => x.CourseId)
            .ToListAsync(cancellationToken);
        var courses = aggregates.Select(x =>
        {
            var course = Catalog.Courses.Single(c => c.Id == x.CourseId && c.InstitutionId == institutionId);
            return new CourseSummary(x.CourseId, course.Code, course.Title, x.Count, x.Total, (double)x.Total / x.Count);
        }).ToArray();
        var count = courses.Sum(x => x.ResponseCount);
        var total = courses.Sum(x => x.RatingTotal);
        return new(institutionId, year, "SQLite via EF Core", new(courses.Length, count, count == 0 ? null : (double)total / count), courses);
    }
}
