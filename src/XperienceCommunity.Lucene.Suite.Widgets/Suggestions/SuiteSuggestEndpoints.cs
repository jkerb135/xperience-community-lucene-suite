using System.Globalization;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Suggestions;
using XperienceCommunity.Lucene.Suite.Widgets.Suggestions;

namespace XperienceCommunity.Lucene.Suite.Widgets;

/// <summary>Maps the browser endpoint for query suggestions, a thin adapter over <see cref="ISuiteSuggestions"/>.</summary>
public static class SuiteSuggestEndpoints
{
    /// <summary>
    /// Maps <c>GET {pattern}?index=&amp;q=</c>, returning a JSON array of strings for an index listed in
    /// <see cref="SuiteWidgetsOptions.SuggestionIndexes"/>: 404 for any other index, 400 when q is longer than
    /// <see cref="ISuiteSuggestions.MaxInputLength"/>, 429 over <see cref="SuiteWidgetsOptions.SuggestionsPerMinute"/> per client IP.
    /// Records no analytics. Requires AddLuceneSuiteWidgets.
    /// </summary>
    /// <returns>The endpoint, for host-specific conventions.</returns>
    public static RouteHandlerBuilder MapLuceneSuiteSuggestions(this IEndpointRouteBuilder endpoints, string pattern = "/lucenesuite/suggest")
    {
        string path = "/" + pattern.Trim('/');
        endpoints.ServiceProvider.GetRequiredService<SuiteSuggestPath>().Value = path;
        return endpoints.MapGet(path, Get);
    }

    private static async Task<IResult> Get(HttpContext context, SuiteSuggestRateLimiter limiter, ISuiteSuggestions suggestions, IOptions<SuiteWidgetsOptions> options)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!limiter.TryAcquire(context))
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        var widgets = options.Value;
        string requested = context.Request.Query["index"].ToString();
        string? index = widgets.SuggestionIndexes.FirstOrDefault(name => string.Equals(name, requested, StringComparison.OrdinalIgnoreCase));
        if (index is null)
        {
            return Results.NotFound();
        }

        string q = context.Request.Query["q"].ToString();
        if (q.Length > ISuiteSuggestions.MaxInputLength)
        {
            return Results.BadRequest();
        }

        IReadOnlyList<string> result = q.Length == 0 ? [] : await suggestions.SuggestAsync(index, q, cancellationToken: context.RequestAborted);
        context.Response.Headers.CacheControl = widgets.SuggestionsMaxAgeSeconds == 0
            ? "no-store"
            : string.Create(CultureInfo.InvariantCulture, $"public, max-age={widgets.SuggestionsMaxAgeSeconds}");
        return Results.Json(result);
    }
}

/// <summary>The path <see cref="SuiteSuggestEndpoints.MapLuceneSuiteSuggestions"/> mapped, without PathBase; null until it is called.</summary>
internal sealed class SuiteSuggestPath
{
    public string? Value { get; set; }
}
