using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using YantaBackEnd.Services;

namespace YantaBackEnd.Authorization;

public static class TableSessionDefaults
{
    public const string Scheme = "TableSession";
    public const string HeaderName = "X-Session-Token";
    public const string SessionIdClaim = "session_id";
}

/// <summary>ยืนยันตัวตนลูกค้าด้วย token จากการสแกน QR (header X-Session-Token)</summary>
public sealed class TableSessionAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly TableSessionTokenService _tokens;
    private readonly SessionService _sessions;

    public TableSessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TableSessionTokenService tokens,
        SessionService sessions)
        : base(options, logger, encoder)
    {
        _tokens = tokens;
        _sessions = sessions;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(TableSessionDefaults.HeaderName, out var header))
        {
            return AuthenticateResult.NoResult();
        }

        if (!_tokens.TryParse(header.ToString(), out var sessionId))
        {
            return AuthenticateResult.Fail("Invalid session token.");
        }

        var session = await _sessions.GetOpenAsync(sessionId);

        if (session is null)
        {
            return AuthenticateResult.Fail("Session is closed.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(TableSessionDefaults.SessionIdClaim, session.Id.ToString()),
                new Claim("table_id", session.TableId.ToString())
            ],
            Scheme.Name);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
