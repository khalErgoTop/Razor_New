namespace TOPqwWeb_Core.Legacy.Bridge;

public sealed class AuthResponse
{
    public int UserId { get; init; }
    public int LoginStatus { get; init; }
    public int UserType { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? Email { get; init; }
    public bool IsDeactivated { get; init; }
    public bool IsAdmin { get; init; }
    public bool IsKosoz { get; init; }
    public IReadOnlyList<int> Modules { get; init; } = Array.Empty<int>();

    public bool IsSuccessful => LoginStatus > 0;
    public int WaitSeconds => LoginStatus < -2 ? Math.Abs(LoginStatus) : 0;
}

public sealed class PasswordRequestResponse
{
    public bool Requested { get; init; }
    public int WaitSeconds { get; init; }
    public string? Email { get; init; }
    public string? Hash { get; init; }
}

public sealed class LegacyMailSettings
{
    public string? MailServerEmail { get; init; }
    public string? MailSupport { get; init; }
    public string? MailServerAddress { get; init; }
    public bool MailServerMitSSL { get; init; }
    public string? MailServerPassword { get; init; }
    public int MailServerPort { get; init; }
}

public sealed class PasswordPolicy
{
    public int MinVal { get; init; } = 1;
    public int MaxVal { get; init; } = 20;
    public int WithSizedLetters { get; init; }
    public int WithSonderzeichen { get; init; }
    public int WithZiffern { get; init; }

    public bool IsValid(string password)
    {
        return password.Length >= MinVal &&
               password.Length <= MaxVal &&
               (WithSizedLetters <= 0 || password.Count(char.IsUpper) >= WithSizedLetters) &&
               (WithSonderzeichen <= 0 || password.Count(character => !char.IsLetterOrDigit(character) && character != '_') >= WithSonderzeichen) &&
               (WithZiffern <= 0 || password.Count(char.IsDigit) >= WithZiffern);
    }

    public string GetHint()
    {
        var hint = $"Mindestlänge: {MinVal} Zeichen<br>Maximallänge: {MaxVal} Zeichen<br>";
        if (WithSizedLetters > 0) hint += $"Mit Groß- und Kleinschreibung: mind. {WithSizedLetters} Zeichen<br>";
        if (WithSonderzeichen > 0) hint += $"Mit Sonderzeichen: mind. {WithSonderzeichen} Zeichen<br>";
        if (WithZiffern > 0) hint += $"Mit Ziffern: mind. {WithZiffern} Zeichen";
        return hint;
    }
}
