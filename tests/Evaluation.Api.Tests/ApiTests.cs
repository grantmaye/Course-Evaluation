using System.Net;
using System.Net.Http.Json;
using Evaluation.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class DemoFactory(string? path = null, bool deleteOnDispose = true) : WebApplicationFactory<Program>
{
    public string DatabasePath { get; } = path ?? Path.Combine(Path.GetTempPath(), $"evaluation-test-{Guid.NewGuid():N}.db");
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureAppConfiguration((_, config) =>
        config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Evaluations"] = $"Data Source={DatabasePath}" }));
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (deleteOnDispose) foreach (var suffix in new[] { "", "-shm", "-wal" }) if (File.Exists(DatabasePath + suffix)) File.Delete(DatabasePath + suffix);
    }
}

public sealed class ApiTests
{
    [Fact]
    public async Task Report_UsesWeightedAverageAndHalfOpenYearBoundary()
    {
        using var app = new DemoFactory(); using var client = app.CreateClient();
        var report = await client.GetFromJsonAsync<EvaluationReport>("/api/reports?institutionId=1&year=2025");
        Assert.NotNull(report); Assert.Equal(3, report.Summary.ResponseCount);
        Assert.Equal(3d, report.Summary.AverageRating); // (5+3+1)/3, NOT average of course averages (4+1)/2.
        Assert.Equal(2, report.Summary.CourseCount);
        var nextYear = await client.GetFromJsonAsync<EvaluationReport>("/api/reports?institutionId=1&year=2026");
        Assert.Equal(1, nextYear!.Summary.ResponseCount); Assert.Equal(2d, nextYear.Summary.AverageRating);
    }
    [Fact]
    public async Task Report_FiltersInstitutionsAndReturnsNullForEmptyYear()
    {
        using var app = new DemoFactory(); using var client = app.CreateClient();
        var report = await client.GetFromJsonAsync<EvaluationReport>("/api/reports?institutionId=2&year=2025");
        Assert.Equal(1, report!.Summary.ResponseCount); Assert.Equal(201, Assert.Single(report.Courses).CourseId);
        var empty = await client.GetFromJsonAsync<EvaluationReport>("/api/reports?institutionId=2&year=2099");
        Assert.Empty(empty!.Courses); Assert.Null(empty.Summary.AverageRating);
    }
    [Theory]
    [InlineData("institutionId=3&year=2025")]
    [InlineData("institutionId=1&year=1999")]
    [InlineData("institutionId=1&year=2100")]
    [InlineData("institutionId=one&year=2025")]
    [InlineData("institutionId=1")]
    public async Task Report_RejectsInvalidFilters(string query)
    {
        using var app = new DemoFactory(); using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/reports?" + query)).StatusCode);
    }
    [Theory]
    [InlineData(1, 101, 0)] [InlineData(1, 101, 6)] [InlineData(1, 201, 5)] [InlineData(99, 101, 5)]
    public async Task Submit_RejectsInvalidRatingOrCrossInstitutionCourse(int institution, int course, int rating)
    {
        using var app = new DemoFactory(); using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/evaluations", new SubmitEvaluation(Guid.NewGuid(), institution, course, rating));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task Submit_ReplaysIdenticalRequestButRejectsChangedPayload()
    {
        using var app = new DemoFactory(); using var client = app.CreateClient();
        var payload = new SubmitEvaluation(Guid.NewGuid(), 1, 101, 4);
        var created = await client.PostAsJsonAsync("/api/evaluations", payload);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var original = await created.Content.ReadFromJsonAsync<EvaluationResponse>();
        Assert.NotNull(original); Assert.True(original.SubmittedAtUtc > DateTime.UtcNow.AddMinutes(-1));
        var replay = await client.PostAsJsonAsync("/api/evaluations", payload);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(original.SubmittedAtUtc, (await replay.Content.ReadFromJsonAsync<EvaluationResponse>())!.SubmittedAtUtc);
        var conflict = await client.PostAsJsonAsync("/api/evaluations", payload with { Rating = 2 });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(4, (await client.GetFromJsonAsync<EvaluationResponse>(created.Headers.Location))!.Rating);
    }
    [Fact]
    public async Task Submit_RejectsEmptyIdAndMalformedJson()
    {
        using var app = new DemoFactory(); using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/evaluations", new SubmitEvaluation(Guid.Empty, 1, 101, 4))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/evaluations", new StringContent("{broken", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
    }
    [Fact]
    public async Task ConcurrentRetry_StoresOneResponse()
    {
        using var app = new DemoFactory(); using var client = app.CreateClient();
        var payload = new SubmitEvaluation(Guid.NewGuid(), 1, 101, 4);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync("/api/evaluations", payload)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Equal(7, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
    }
    [Fact]
    public async Task MissingResponse_Returns404()
    {
        using var app = new DemoFactory(); using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/evaluations/{Guid.NewGuid()}")).StatusCode);
    }
}

public sealed class PersistenceTests
{
    [Fact]
    public async Task Restart_PreservesSubmittedRecordAndDoesNotDuplicateSeed()
    {
        var path = Path.Combine(Path.GetTempPath(), $"evaluation-restart-{Guid.NewGuid():N}.db");
        var payload = new SubmitEvaluation(Guid.NewGuid(), 1, 101, 4);
        using (var first = new DemoFactory(path, deleteOnDispose: false))
        using (var client = first.CreateClient())
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/evaluations", payload)).StatusCode);
        using var second = new DemoFactory(path);
        using var restarted = second.CreateClient();
        var persisted = await restarted.GetFromJsonAsync<EvaluationResponse>($"/api/evaluations/{payload.Id}");
        Assert.NotNull(persisted); Assert.Equal(4, persisted.Rating);
        Assert.Equal(DateTimeKind.Utc, persisted.SubmittedAtUtc.Kind);
        var report = await restarted.GetFromJsonAsync<EvaluationReport>("/api/reports?institutionId=1&year=2025");
        Assert.Equal(3, report!.Summary.ResponseCount);
    }
}

public sealed class IntegrityTests
{
    [Theory]
    [InlineData(1, 101, 0)]
    [InlineData(1, 201, 5)]
    [InlineData(99, 101, 5)]
    public async Task Database_RejectsInvalidRowsEvenWithoutApiValidation(int institution, int course, int rating)
    {
        using var app = new DemoFactory(); using var client = app.CreateClient();
        using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(app.Services);
        var db = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<EvaluationDb>(scope.ServiceProvider);
        db.Responses.Add(new EvaluationResponse { Id = Guid.NewGuid(), InstitutionId = institution,
            CourseId = course, Rating = rating, SubmittedAtUtc = DateTime.UtcNow });
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task ConcurrentChangedPayload_OneWinsAndOtherConflicts()
    {
        using var app = new DemoFactory(); using var client = app.CreateClient();
        var payload = new SubmitEvaluation(Guid.NewGuid(), 1, 101, 4);
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/evaluations", payload),
            client.PostAsJsonAsync("/api/evaluations", payload with { Rating = 2 }));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var winner = await responses.Single(x => x.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<EvaluationResponse>();
        var stored = await client.GetFromJsonAsync<EvaluationResponse>($"/api/evaluations/{payload.Id}");
        Assert.Equal(winner!.Rating, stored!.Rating);
    }
}
