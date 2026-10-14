using TimeTracker.Application.Authentication;

namespace TimeTracker.Api.Authentication;

// DateTime con Kind=Utc: System.Text.Json lo escribe con «Z»; un DateTimeOffset saldría con «+00:00».
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAt, UserResponse User)
{
    public static LoginResponse From(LoginResult result)
        => new(result.Token.Value, result.Token.ExpiresAt.UtcDateTime, UserResponse.From(result.User));
}
