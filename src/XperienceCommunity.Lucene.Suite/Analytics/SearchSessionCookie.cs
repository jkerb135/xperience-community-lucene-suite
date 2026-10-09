using CMS.Helpers;
using CMS.Websites;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace XperienceCommunity.Lucene.Suite.Analytics;

/// <summary>Random, 30-minute sliding Suite session id, issued only at Kentico cookie level Visitor or All.</summary>
internal sealed class SearchSessionCookie(IHttpContextAccessor httpContextAccessor, TimeProvider timeProvider)
{
    /// <summary>Cookie name; registered with Kentico at Visitor level by AddLuceneSuite.</summary>
    internal const string Name = "lucenesuite.session";

    internal static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(30);

    /// <summary>Reads or creates the session id for the current request; null outside a request or below Visitor.</summary>
    public Guid? GetSessionId()
    {
        // Kentico's provider returns normally after the response has started (observed in xbk, plan 02-02), so it is called first.
        if (httpContextAccessor.HttpContext is not { } context
            || context.RequestServices?.GetService<ICurrentCookieLevelProvider>() is not { } cookieLevel
            || cookieLevel.GetCurrentCookieLevel() < CookieLevelConstants.VISITOR)
        {
            return null;
        }

        Guid? existing = Guid.TryParse(context.Request.Cookies[Name], out var parsed) && parsed != Guid.Empty ? parsed : null;
        if (context.Response.HasStarted)
        {
            return existing;
        }

        var id = existing ?? Guid.NewGuid();
        context.Response.Cookies.Append(Name, id.ToString("N"), new CookieOptions
        {
            Expires = timeProvider.GetUtcNow() + SlidingExpiration,
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
        });
        return id;
    }
}
