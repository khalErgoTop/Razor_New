using System.Text.Json;

namespace TOPqwWeb_Core.Infrastructure.Security;

public sealed class LegacyUserSession
{
    public string Id { get; init; } = string.Empty;
    public int BEID { get; init; }
    public string Username { get; init; } = "Gast";
    public string? Email { get; init; }
    public bool IsDeactivated { get; init; }
    public bool KeepLoggedIn { get; set; }
    public string LastPage { get; set; } = string.Empty;
    public string Previouspage { get; set; } = string.Empty;
    public IReadOnlyList<int> Modules { get; init; } = Array.Empty<int>();

    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

    public static LegacyUserSession? FromJson(string value)
    {
        return JsonSerializer.Deserialize<LegacyUserSession>(value);
    }
}
