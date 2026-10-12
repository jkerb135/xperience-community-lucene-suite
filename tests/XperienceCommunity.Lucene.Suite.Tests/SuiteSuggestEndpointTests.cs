using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Suggestions;
using XperienceCommunity.Lucene.Suite.Widgets;
using XperienceCommunity.Lucene.Suite.Widgets.Suggestions;

namespace XperienceCommunity.Lucene.Suite.Tests;

/// <summary>Widgets suggest endpoint on an in-process host over a fake <see cref="ISuiteSuggestions"/>.</summary>
public sealed class SuiteSuggestEndpointTests
{
    private const string Url = "/lucenesuite/suggest";

    [Fact]
    public async Task ListedIndex_Returns200WithAStringArray_AndPublicMaxAge()
    {
        await using var host = await Host.Start();

        var response = await host.Client.GetAsync(Url + "?index=dancinggoatsample&q=coff");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=60", response.Headers.CacheControl?.ToString());
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Equal(["coffee", "coff <b>"], JsonSerializer.Deserialize<string[]>(await response.Content.ReadAsStringAsync())!);
        Assert.Equal(("DancingGoatSample", "coff", (int?)null), Assert.Single(host.Suggestions.Calls));
        Assert.Empty(host.Logger.Calls);
    }

    [Theory]
    [InlineData("?index=Other&q=coff")]
    [InlineData("?q=coff")]
    public async Task UnlistedIndex_Is404(string query)
    {
        await using var host = await Host.Start();

        var response = await host.Client.GetAsync(Url + query);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(host.Suggestions.Calls);
    }

    [Fact]
    public async Task EmptySuggestionIndexes_TurnTheEndpointOff()
    {
        await using var host = await Host.Start(o => o.SuggestionIndexes = []);

        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync(Url + "?index=DancingGoatSample&q=coff")).StatusCode);
    }

    [Fact]
    public async Task QueryOver100Characters_Is400_And100IsAccepted()
    {
        await using var host = await Host.Start();

        var tooLong = await host.Client.GetAsync(Url + "?index=DancingGoatSample&q=" + new string('a', ISuiteSuggestions.MaxInputLength + 1));
        var longest = await host.Client.GetAsync(Url + "?index=DancingGoatSample&q=" + new string('a', ISuiteSuggestions.MaxInputLength));

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.OK), (tooLong.StatusCode, longest.StatusCode));
        Assert.Equal("no-store", tooLong.Headers.CacheControl?.ToString());
        Assert.Single(host.Suggestions.Calls);
    }

    [Theory]
    [InlineData("?index=DancingGoatSample")]
    [InlineData("?index=DancingGoatSample&q=")]
    public async Task MissingQuery_Is200WithAnEmptyArray(string query)
    {
        await using var host = await Host.Start();

        var response = await host.Client.GetAsync(Url + query);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
        Assert.Empty(host.Suggestions.Calls);
    }

    [Fact]
    public async Task ZeroMaxAge_SendsNoStore()
    {
        await using var host = await Host.Start(o => o.SuggestionsMaxAgeSeconds = 0);

        var response = await host.Client.GetAsync(Url + "?index=DancingGoatSample&q=coff");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task RateLimit_Is429WithNoStore_SeparateFromEvents_AndLeavesHostOptionsAlone()
    {
        await using var host = await Host.Start(o =>
        {
            o.SuggestionsPerMinute = 2;
            o.EventsPerMinute = 1;
        });

        var responses = new List<HttpResponseMessage>();
        for (int i = 0; i < 3; i++)
        {
            responses.Add(await host.Client.GetAsync(Url + "?index=DancingGoatSample&q=coff"));
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], responses.Select(r => r.StatusCode));
        Assert.Equal("no-store", responses[2].Headers.CacheControl?.ToString());
        Assert.Equal(503, host.App.Services.GetRequiredService<IOptions<RateLimiterOptions>>().Value.RejectionStatusCode);
        Assert.Empty(host.App.Services.GetServices<IConfigureOptions<RateLimiterOptions>>());
    }

    [Fact]
    public async Task ZeroSuggestionsPerMinute_TurnsTheLimitOff()
    {
        await using var host = await Host.Start(o => o.SuggestionsPerMinute = 0);

        for (int i = 0; i < 400; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync(Url + "?index=DancingGoatSample&q=coff")).StatusCode);
        }
    }

    [Theory]
    [InlineData(-1, 60)]
    [InlineData(10_001, 60)]
    [InlineData(300, -1)]
    [InlineData(300, 3601)]
    public async Task SuggestionSettingsOutOfRange_FailAtStartup(int perMinute, int maxAge) =>
        await Assert.ThrowsAsync<OptionsValidationException>(() => Host.Start(o =>
        {
            o.SuggestionsPerMinute = perMinute;
            o.SuggestionsMaxAgeSeconds = maxAge;
        }));

    [Fact]
    public void SuggestionDefaults_MatchTheDocumentedValues()
    {
        var options = new SuiteWidgetsOptions();

        Assert.Equal((0, 300, 60), (options.SuggestionIndexes.Length, options.SuggestionsPerMinute, options.SuggestionsMaxAgeSeconds));
    }

    private sealed class Host(WebApplication app, FakeSuggestions suggestions, NoInteractions logger) : IAsyncDisposable
    {
        public WebApplication App { get; } = app;

        public HttpClient Client { get; } = app.GetTestClient();

        public FakeSuggestions Suggestions { get; } = suggestions;

        public NoInteractions Logger { get; } = logger;

        public static async Task<Host> Start(Action<SuiteWidgetsOptions>? configure = null)
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseTestServer();
            var suggestions = new FakeSuggestions();
            var logger = new NoInteractions();
            builder.Services.AddSingleton<ISearchInteractionLogger>(logger);
            builder.Services.AddSingleton<ISuiteSuggestions>(suggestions);
            builder.Services.AddLuceneSuiteWidgets(o =>
            {
                o.SuggestionIndexes = ["DancingGoatSample"];
                configure?.Invoke(o);
            });
            var app = builder.Build();
            app.MapLuceneSuiteSuggestions();
            try
            {
                await app.StartAsync();
            }
            catch
            {
                await app.DisposeAsync();
                throw;
            }

            return new Host(app, suggestions, logger);
        }

        public async ValueTask DisposeAsync() => await App.DisposeAsync();
    }

    private sealed class FakeSuggestions : ISuiteSuggestions
    {
        public List<(string IndexName, string? Input, int? MaxResults)> Calls { get; } = [];

        public Task<IReadOnlyList<string>> SuggestAsync(string indexName, string? input, int? maxResults = null, CancellationToken cancellationToken = default)
        {
            Calls.Add((indexName, input, maxResults));
            return Task.FromResult<IReadOnlyList<string>>(["coffee", "coff <b>"]);
        }
    }

    private sealed class NoInteractions : ISearchInteractionLogger
    {
        public List<string> Calls { get; } = [];

        public void LogClick(Guid searchId, int sequence, Guid itemGuid, int position, Guid? eventId = null) => Calls.Add("click");

        public void LogConversion(Guid searchId, int sequence, Guid itemGuid, int? position = null, Guid? eventId = null) => Calls.Add("conversion");
    }
}
