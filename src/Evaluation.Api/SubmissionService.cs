using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
namespace Evaluation.Api;

public enum SubmissionOutcome { Created, Replayed, Conflict }
public sealed record SubmissionResult(SubmissionOutcome Outcome, EvaluationResponse Response);

public sealed class SubmissionService(EvaluationDb db, TimeProvider clock)
{
    public async Task<SubmissionResult> SubmitAsync(SubmitEvaluation request, CancellationToken cancellationToken)
    {
        var existing = await db.Responses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (existing is not null) return Resolve(existing, request);
        var response = new EvaluationResponse
        {
            Id = request.Id, InstitutionId = request.InstitutionId, CourseId = request.CourseId,
            Rating = request.Rating, SubmittedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        db.Responses.Add(response);
        try
        {
            // One SaveChanges operation is transactional. The primary key enforces uniqueness across requests.
            await db.SaveChangesAsync(cancellationToken);
            return new(SubmissionOutcome.Created, response);
        }
        catch (DbUpdateException error) when (error.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            // Another request can win between lookup and insert. Only treat an existing matching ID as a replay.
            db.Entry(response).State = EntityState.Detached;
            existing = await db.Responses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (existing is null) throw;
            return Resolve(existing, request);
        }
    }
    private static SubmissionResult Resolve(EvaluationResponse existing, SubmitEvaluation request) =>
        new(existing.InstitutionId == request.InstitutionId && existing.CourseId == request.CourseId && existing.Rating == request.Rating
            ? SubmissionOutcome.Replayed : SubmissionOutcome.Conflict, existing);
}
