namespace Evaluation.Api;

// Synthetic institutional data only. There is deliberately no student identity field.
public sealed class EvaluationResponse
{
    public Guid Id { get; set; }
    public int InstitutionId { get; set; }
    public int CourseId { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public int Rating { get; set; }
}

public sealed record SubmitEvaluation(Guid Id, int InstitutionId, int CourseId, int Rating);
public sealed record Course(int Id, int InstitutionId, string Code, string Title);
public sealed record CourseSummary(int CourseId, string Code, string Title, int ResponseCount, int RatingTotal, double AverageRating);
public sealed record ReportSummary(int CourseCount, int ResponseCount, double? AverageRating);
public sealed record EvaluationReport(int InstitutionId, int Year, string Engine, ReportSummary Summary, IReadOnlyList<CourseSummary> Courses);

public static class Catalog
{
    public static readonly Course[] Courses =
    [
        new(101, 1, "CSC-151", "Programming foundations"),
        new(102, 1, "MAT-171", "Precalculus algebra"),
        new(201, 2, "CSC-151", "Programming foundations")
    ];
    public static bool IsInstitution(int id) => id is 1 or 2;
    public static bool OwnsCourse(int institutionId, int courseId) => Courses.Any(c => c.Id == courseId && c.InstitutionId == institutionId);
}
