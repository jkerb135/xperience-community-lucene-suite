using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Search;

namespace XperienceCommunity.Lucene.Suite.Widgets.Events;

/// <summary>Maps the browser endpoints for search analytics.</summary>
public static class SuiteEventsEndpoints
{
    /// <summary>Largest accepted event body in bytes.</summary>
    public const int MaxBodyBytes = 1024;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Maps <c>POST {pattern}</c> (a click or conversion, rate-limited per IP; 202, 400, 413 or 429) and
    /// <c>GET {pattern}/settings</c> (the <see cref="SuiteClientSettings"/>). Requires AddLuceneSuiteWidgets.
    /// </summary>
    /// <returns>The route group, for host-specific conventions.</returns>
    public static RouteGroupBuilder MapLuceneSuiteEvents(this IEndpointRouteBuilder endpoints, string pattern = "/lucenesuite/events")
    {
        string path = "/" + pattern.Trim('/');
        endpoints.ServiceProvider.GetRequiredService<SuiteEventsPath>().Value = path;
        var group = endpoints.MapGroup(path);
        group.MapPost(string.Empty, Post).DisableAntiforgery();
        group.MapGet("settings", (HttpContext context, IOptions<SuiteSearchOptions> options) =>
        {
            context.Response.Headers.CacheControl = "public, max-age=300";
            return Results.Json(SuiteClientSettings.From(options.Value, context.Request.PathBase + path));
        });
        return group;
    }

    private static async Task<IResult> Post(HttpContext context, SuiteEventsRateLimiter limiter, ISearchInteractionLogger logger)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!limiter.TryAcquire(context))
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        var body = context.Request.ContentLength > MaxBodyBytes ? null : await ReadBody(context.Request.Body, context.RequestAborted);
        if (body is null)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        try
        {
            var e = JsonSerializer.Deserialize<SuiteEvent>(body.Value.Span, Json);
            switch (e?.Type)
            {
                case "click" when e.Position is { } position:
                    logger.LogClick(e.SearchId, e.Sequence, e.ItemGuid, position, e.EventId);
                    break;
                case "conversion":
                    logger.LogConversion(e.SearchId, e.Sequence, e.ItemGuid, e.Position, e.EventId);
                    break;
                default:
                    return Invalid();
            }
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return Invalid();
        }

        return Results.StatusCode(StatusCodes.Status202Accepted);
    }

    /// <summary>Reads at most <see cref="MaxBodyBytes"/>; null when the body is longer, whatever Content-Length said.</summary>
    private static async Task<ReadOnlyMemory<byte>?> ReadBody(Stream body, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxBodyBytes + 1];
        int length = 0;
        int read;
        while (length < buffer.Length && (read = await body.ReadAsync(buffer.AsMemory(length), cancellationToken)) > 0)
        {
            length += read;
        }

        if (length > MaxBodyBytes)
        {
            return null;
        }

        return buffer.AsMemory(0, length);
    }

    private static IResult Invalid() => Results.Text("Invalid event.", statusCode: StatusCodes.Status400BadRequest);

    private sealed record SuiteEvent(string? Type, Guid SearchId, int Sequence, Guid ItemGuid, int? Position, Guid? EventId);
}

/// <summary>The path <see cref="SuiteEventsEndpoints.MapLuceneSuiteEvents"/> mapped, without PathBase; null until it is called.</summary>
internal sealed class SuiteEventsPath
{
    public string? Value { get; set; }
}
