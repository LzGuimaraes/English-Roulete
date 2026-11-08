namespace EnglishRoulette.Contracts;

// Opaque capability for the current solo page. Never persist in browser storage.
public static class SoloSessionHeaders
{
    public const string Credential = "X-English-Roulette-Solo";
}
