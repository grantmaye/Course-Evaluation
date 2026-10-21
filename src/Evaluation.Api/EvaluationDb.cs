using Microsoft.EntityFrameworkCore;
namespace Evaluation.Api;

public sealed class EvaluationDb(DbContextOptions<EvaluationDb> options) : DbContext(options)
{
    public DbSet<EvaluationResponse> Responses => Set<EvaluationResponse>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        var response = model.Entity<EvaluationResponse>();
        response.HasKey(x => x.Id);
        // SQLite stores no DateTimeKind; restore UTC on materialization for stable JSON timestamps.
        response.Property(x => x.SubmittedAtUtc).HasConversion(
            value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        response.ToTable("EvaluationResponses", table =>
        {
            table.HasCheckConstraint("CK_Rating", "Rating BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_Institution", "InstitutionId IN (1,2)");
            table.HasCheckConstraint("CK_Course", "(InstitutionId = 1 AND CourseId IN (101,102)) OR (InstitutionId = 2 AND CourseId = 201)");
        });
        response.HasIndex(x => new { x.InstitutionId, x.SubmittedAtUtc, x.CourseId });
    }
}
