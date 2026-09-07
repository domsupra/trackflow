using TrackFlow.Api.Data;

namespace TrackFlow.Api.Reports;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/reports/campaigns", GetCampaignReport);
        return app;
    }

    private static async Task<IResult> GetCampaignReport(
        DateTime? from, DateTime? to, TrackingDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (from is null) errors["from"] = new[] { "Required (ISO-8601)." };
        if (to is null) errors["to"] = new[] { "Required (ISO-8601)." };
        if (from is not null && to is not null && to <= from) errors["to"] = new[] { "Must be after 'from'." };
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var rows = await CampaignReport.QueryAsync(db, from!.Value.ToUniversalTime(), to!.Value.ToUniversalTime(), ct);
        return Results.Ok(rows);
    }
}
