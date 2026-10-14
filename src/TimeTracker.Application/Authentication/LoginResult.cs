namespace TimeTracker.Application.Authentication;

public sealed record LoginResult(AccessToken Token, UserSummary User);
