public static class RouteKey
{
    public static string Make(string a, string b)
        => string.CompareOrdinal(a, b) < 0 ? $"{a}|{b}" : $"{b}|{a}";
}
