namespace DenialsCommandCenter.Api.Auth;

public static class Roles
{
    public const string Manager = nameof(Manager);
    public const string Specialist = nameof(Specialist);
}

public static class Policies
{
    public const string Manager = nameof(Manager);
}

public static class RateLimits
{
    public const string Login = nameof(Login);
    public const string Expensive = nameof(Expensive);
}
