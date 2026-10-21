using Evaluation.Api;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
// Local synthetic-data demonstration, not an authenticated production service.
builder.WebHost.UseUrls("http://127.0.0.1:5080");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16_384);
builder.Services.AddProblemDetails();
// Keep binding failures as HTTP 400 in Development as well as Production.
builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.AddDbContext<EvaluationDb>(options => options.UseSqlite(
    builder.Configuration.GetConnectionString("Evaluations") ?? "Data Source=evaluations.db"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<SubmissionService>();
var app = builder.Build();
app.UseExceptionHandler();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EvaluationDb>();
    await db.Database.EnsureCreatedAsync();
    if (app.Configuration.GetValue("SeedDemoData", true)) await DemoData.SeedAsync(db);
}
app.MapGet("/", () => Results.Ok(new { name = "Course Evaluation API", mode = "Local synthetic-data demo", endpoints = new[] { "GET /health", "GET /api/courses?institutionId=1", "GET /api/reports?institutionId=1&year=2025", "POST /api/evaluations" } }));
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/courses", (int institutionId) => Catalog.IsInstitution(institutionId)
    ? Results.Ok(Catalog.Courses.Where(c => c.InstitutionId == institutionId))
    : Results.ValidationProblem(new Dictionary<string, string[]> { ["institutionId"] = ["Choose fictional institution 1 or 2."] }));
app.MapGet("/api/reports", async (int institutionId, int year, ReportService reports, CancellationToken ct) =>
{
    var errors = new Dictionary<string, string[]>();
    if (!Catalog.IsInstitution(institutionId)) errors["institutionId"] = ["Choose fictional institution 1 or 2."];
    if (year is < 2000 or > 2099) errors["year"] = ["Year must be between 2000 and 2099."];
    return errors.Count > 0 ? Results.ValidationProblem(errors) : Results.Ok(await reports.GetAsync(institutionId, year, ct));
});
app.MapPost("/api/evaluations", async (SubmitEvaluation request, SubmissionService submissions, CancellationToken ct) =>
{
    var errors = new Dictionary<string, string[]>();
    if (request.Id == Guid.Empty) errors["id"] = ["Provide a non-empty UUID as the idempotency key."];
    if (!Catalog.OwnsCourse(request.InstitutionId, request.CourseId)) errors["courseId"] = ["Course must belong to the selected fictional institution."];
    if (request.Rating is < 1 or > 5) errors["rating"] = ["Rating must be between 1 and 5."];
    if (errors.Count > 0) return Results.ValidationProblem(errors);
    var result = await submissions.SubmitAsync(request, ct);
    return result.Outcome switch
    {
        SubmissionOutcome.Created => Results.Created($"/api/evaluations/{result.Response.Id}", result.Response),
        SubmissionOutcome.Replayed => Results.Ok(result.Response),
        _ => Results.Problem(statusCode: 409, title: "Idempotency conflict", detail: "This ID was already used for a different evaluation.")
    };
});
app.MapGet("/api/evaluations/{id:guid}", async (Guid id, EvaluationDb db, CancellationToken ct) =>
    await db.Responses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) is { } response ? Results.Ok(response) : Results.NotFound());
await app.RunAsync();
public partial class Program { }
