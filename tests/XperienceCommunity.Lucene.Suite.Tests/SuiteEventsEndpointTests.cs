using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using XperienceCommunity.Lucene.Suite.Analytics;
using XperienceCommunity.Lucene.Suite.Search;
using XperienceCommunity.Lucene.Suite.Widgets;
using XperienceCommunity.Lucene.Suite.Widgets.Events;

namespace XperienceCommunity.Lucene.Suite.Tests;

/// <summary>Widgets events and settings endpoints on an in-process host (no UseRateLimiter: the package limits on its own).</summary>
public sealed class SuiteEventsEndpointTests
{
    private const string Url = "/lucenesuite/events";
    private static readonly Guid SearchId = Guid.Parse("8b7e1f6c-2a3d-4e5f-9a1b-0c2d3e4f5a6b");
    private static readonly Guid ItemGuid = Guid.Parse("1f2e3d4c-5b6a-4978-8a9b-0c1d2e3f4a5b");
    private static readonly Guid EventId = Guid.Parse("c0ffee00-1234-4abc-8def-0123456789ab");

    [Fact]
    public async Task Click_IsLogged_And202WithNoStore()
    {
        await using var host = await Host.Start();

        var response = await host.Client.PostAsJsonAsync(Url, new { type = "click", searchId = SearchId, sequence = 3, itemGuid = ItemGuid, position = 12, eventId = EventId });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(await response.Content.ReadAsStringAsync());
        Assert.Equal(("click", SearchId, 3, ItemGuid, (int?)12, (Guid?)EventId), Assert.Single(host.Logger.Calls));
        Assert.Equal((null, null), Assert.Single(host.Logger.ClickTexts));
    }

    [Fact]
    public async Task ClickV2_PassesQueryAndTitle()
    {
        await using var host = await Host.Start();

        var response = await host.Client.PostAsJsonAsync(Url, new { type = "click", searchId = SearchId, sequence = 3, itemGuid = ItemGuid, position = 12, query = "coffee grinder", title = "Burr grinder" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(("coffee grinder", "Burr grinder"), Assert.Single(host.Logger.ClickTexts));
    }

    [Theory]
    [InlineData("\"query\":123,\"title\":{\"a\":1}")]
    [InlineData("\"query\":[\"x\"],\"title\":true")]
    [InlineData("\"query\":null,\"title\":\"\"")]
    [InlineData("\"query\":\"\\ud800\",\"title\":\"\\udc00x\"")]
    [InlineData("\"query\":\"xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx\",\"title\":\"yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy\"")]
    public async Task ClickV2_MalformedOrOversizedText_IsDropped_Not400(string fields)
    {
        await using var host = await Host.Start();
        string body = $$"""{"type":"click","searchId":"{{SearchId}}","sequence":1,"itemGuid":"{{ItemGuid}}","position":2,{{fields}}}""";

        var response = await host.Client.PostAsync(Url, new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Single(host.Logger.Calls);
        Assert.Equal((null, null), Assert.Single(host.Logger.ClickTexts));
    }

    [Fact]
    public async Task ConversionAsTextPlain_WithoutPositionOrEventId_IsLogged()
    {
        await using var host = await Host.Start();
        string body = $$"""{"type":"conversion","searchId":"{{SearchId}}","sequence":1,"itemGuid":"{{ItemGuid}}"}""";

        var response = await host.Client.PostAsync(Url, new StringContent(body, Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(("conversion", SearchId, 1, ItemGuid, (int?)null, (Guid?)null), Assert.Single(host.Logger.Calls));
    }

    [Theory]
    [InlineData("""{"type":"view","searchId":"8b7e1f6c-2a3d-4e5f-9a1b-0c2d3e4f5a6b","sequence":1,"itemGuid":"1f2e3d4c-5b6a-4978-8a9b-0c1d2e3f4a5b","position":1}""")]
    [InlineData("""{"type":"click","searchId":"8b7e1f6c-2a3d-4e5f-9a1b-0c2d3e4f5a6b","sequence":1,"itemGuid":"1f2e3d4c-5b6a-4978-8a9b-0c1d2e3f4a5b"}""")]
    [InlineData("""{"type":"click","searchId":"not-a-guid","sequence":1,"itemGuid":"1f2e3d4c-5b6a-4978-8a9b-0c1d2e3f4a5b","position":1}""")]
    [InlineData("""{"type":"click","searchId":"00000000-0000-0000-0000-000000000000","sequence":1,"itemGuid":"1f2e3d4c-5b6a-4978-8a9b-0c1d2e3f4a5b","position":1}""")]
    [InlineData("""{"type":"click","searchId":"8b7e1f6c-2a3d-4e5f-9a1b-0c2d3e4f5a6b","sequence":0,"itemGuid":"1f2e3d4c-5b6a-4978-8a9b-0c1d2e3f4a5b","position":1}""")]
    [InlineData("""{"type":"click","sequence":99999999999}""")]
    [InlineData("""{"type":""")]
    [InlineData("null")]
    [InlineData("")]
    public async Task InvalidEvent_Is400WithShortMessage(string body)
    {
        await using var host = await Host.Start();

        var response = await host.Client.PostAsync(Url, new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Invalid event.", await response.Content.ReadAsStringAsync());
        Assert.Empty(host.Logger.Calls);
    }

    [Fact]
    public async Task BodyOver1Kb_Is413_WithAndWithoutContentLength()
    {
        await using var host = await Host.Start();
        var large = Encoding.UTF8.GetBytes($$"""{"type":"click","pad":"{{new string('x', SuiteEventsEndpoints.MaxBodyBytes)}}"}""");

        var withLength = await host.Client.PostAsync(Url, new ByteArrayContent(large));
        var chunked = await host.Client.PostAsync(Url, new ChunkedContent(Encoding.UTF8.GetBytes(new string(' ', 2048))));

        Assert.Equal((HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.RequestEntityTooLarge), (withLength.StatusCode, chunked.StatusCode));
    }

    [Fact]
    public async Task Get_OnEventsRoute_Is405()
    {
        await using var host = await Host.Start();

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await host.Client.GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task RateLimit_AppliesWithoutHostMiddleware_AndLeavesHostOptionsAlone()
    {
        await using var host = await Host.Start(o => o.EventsPerMinute = 2);

        var codes = new List<HttpStatusCode>();
        for (int i = 0; i < 3; i++)
        {
            codes.Add((await host.Client.PostAsJsonAsync(Url, new { type = "conversion", searchId = SearchId, sequence = 1, itemGuid = ItemGuid })).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Accepted, HttpStatusCode.Accepted, HttpStatusCode.TooManyRequests], codes);
        Assert.Equal(503, host.App.Services.GetRequiredService<IOptions<RateLimiterOptions>>().Value.RejectionStatusCode);
        Assert.Empty(host.App.Services.GetServices<IConfigureOptions<RateLimiterOptions>>());
    }

    [Fact]
    public async Task ZeroEventsPerMinute_TurnsTheLimitOff()
    {
        await using var host = await Host.Start(o => o.EventsPerMinute = 0);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(HttpStatusCode.Accepted, (await host.Client.PostAsJsonAsync(Url, new { type = "conversion", searchId = SearchId, sequence = 1, itemGuid = ItemGuid })).StatusCode);
        }
    }

    [Fact]
    public async Task Settings_ReturnServerValues_Cacheable()
    {
        await using var host = await Host.Start();

        var response = await host.Client.GetAsync(Url + "/settings");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        Assert.Equal("public, max-age=300", response.Headers.CacheControl?.ToString());
        Assert.Equal(
            (45000L, 5, 8, 0.5, 3600000L, Url),
            (root.GetProperty("textChangeTimeoutMs").GetInt64(), root.GetProperty("typoOneEditMinLength").GetInt32(), root.GetProperty("typoTwoEditMinLength").GetInt32(),
             root.GetProperty("deepBackspaceRatio").GetDouble(), root.GetProperty("attributionWindowMs").GetInt64(), root.GetProperty("eventsUrl").GetString()));
    }

    [Fact]
    public void WithoutAddLuceneSuite_RegistrationThrows()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddLuceneSuiteWidgets());

        Assert.Contains("AddLuceneSuite()", error.Message);
    }

    [Fact]
    public async Task EventsPerMinuteOutOfRange_FailsAtStartup() =>
        await Assert.ThrowsAsync<OptionsValidationException>(() => Host.Start(o => o.EventsPerMinute = -1));

    [Fact]
    public void ResultReference_RoundTrips()
    {
        string value = SuiteResultReference.Format(SearchId, 4, 1000);

        Assert.Equal("8b7e1f6c2a3d4e5f9a1b0c2d3e4f5a6b.4.1000", value);
        Assert.True(SuiteResultReference.TryParse(value, out var reference));
        Assert.Equal(new SuiteResultReference(SearchId, 4, 1000), reference);
        Assert.Equal(value, reference.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("8b7e1f6c2a3d4e5f9a1b0c2d3e4f5a6b.4")]
    [InlineData("8b7e1f6c2a3d4e5f9a1b0c2d3e4f5a6b.4.1.1")]
    [InlineData("8b7e1f6c-2a3d-4e5f-9a1b-0c2d3e4f5a6b.4.1")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz.4.1")]
    [InlineData("00000000000000000000000000000000.4.1")]
    [InlineData("8b7e1f6c2a3d4e5f9a1b0c2d3e4f5a6b.0.1")]
    [InlineData("8b7e1f6c2a3d4e5f9a1b0c2d3e4f5a6b.-1.1")]
    [InlineData("8b7e1f6c2a3d4e5f9a1b0c2d3e4f5a6b.+1.1")]
    [InlineData("8b7e1f6c2a3d4e5f9a1b0c2d3e4f5a6b.1.0")]
    [InlineData("8b7e1f6c2a3d4e5f9a1b0c2d3e4f5a6b.1.1001")]
    [InlineData("8b7e1f6c2a3d4e5f9a1b0c2d3e4f5a6b.1. 1")]
    [InlineData("8b7e1f6c2a3d4e5f9a1b0c2d3e4f5a6b.1.99999999999")]
    [InlineData("8b7e1f6c2a3d4e5f9a1b0c2d3e4f5a6b.1.000000000000000000000000000001")]
    public void ResultReference_RejectsInvalidInput(string? value)
    {
        Assert.False(SuiteResultReference.TryParse(value, out var reference));
        Assert.Equal(default, reference);
    }

    private sealed class Host(WebApplication app, RecordingLogger logger) : IAsyncDisposable
    {
        public WebApplication App { get; } = app;

        public HttpClient Client { get; } = app.GetTestClient();

        public RecordingLogger Logger { get; } = logger;

        public static async Task<Host> Start(Action<SuiteWidgetsOptions>? configure = null)
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseTestServer();
            var logger = new RecordingLogger();
            builder.Services.AddSingleton<ISearchInteractionLogger>(logger);
            builder.Services.AddOptions<SuiteSearchOptions>().Configure(o =>
            {
                o.Analytics.Merge.TextChangeWindow = TimeSpan.FromSeconds(45);
                o.Analytics.Merge.TypoOneEditMinLength = 5;
            });
            builder.Services.AddLuceneSuiteWidgets(configure);
            var app = builder.Build();
            app.MapLuceneSuiteEvents();
            try
            {
                await app.StartAsync();
            }
            catch
            {
                await app.DisposeAsync();
                throw;
            }

            return new Host(app, logger);
        }

        public async ValueTask DisposeAsync() => await App.DisposeAsync();
    }

    /// <summary>Records calls and validates like the real logger: argument errors throw synchronously.</summary>
    private sealed class RecordingLogger : ISearchInteractionLogger
    {
        public List<(string Type, Guid SearchId, int Sequence, Guid ItemGuid, int? Position, Guid? EventId)> Calls { get; } = [];

        public List<(string? Query, string? Title)> ClickTexts { get; } = [];

        public void LogClick(SearchClick click)
        {
            Record("click", click.SearchId, click.Sequence, click.ItemGuid, click.Position, click.EventId);
            ClickTexts.Add((click.QueryText, click.ResultTitle));
        }

        public void LogConversion(Guid searchId, int sequence, Guid itemGuid, int? position = null, Guid? eventId = null) =>
            Record("conversion", searchId, sequence, itemGuid, position, eventId);

        private void Record(string type, Guid searchId, int sequence, Guid itemGuid, int? position, Guid? eventId)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(searchId, Guid.Empty);
            ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
            Calls.Add((type, searchId, sequence, itemGuid, position, eventId));
        }
    }

    /// <summary>A body sent without Content-Length.</summary>
    private sealed class ChunkedContent(byte[] data) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await stream.WriteAsync(data);
            await stream.FlushAsync();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
