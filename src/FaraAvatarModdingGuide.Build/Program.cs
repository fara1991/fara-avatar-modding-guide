using FaraAvatarModdingGuide.Build;

// 使い方:
//   dotnet run --project src/FaraAvatarModdingGuide.Build -c Release -- \
//     --template <dotnet publish の wwwroot/index.html> --out <dotnet publish の wwwroot> --env production|develop \
//     [--repo .] [--dev-origin https://avatar-preview.fara-labs.com] [--kit external/fara-site-kit] [--node node]
// 詳しくは SiteBuilder.cs。
try
{
    var values = ParseArgs(args);
    string Get(string key, string? fallback = null) =>
        values.TryGetValue(key, out var v) ? v : fallback ?? throw new ArgumentException($"--{key} を指定してください");

    var repo = Path.GetFullPath(Get("repo", "."));
    SiteBuilder.Run(new BuildOptions(
        repo,
        Path.GetFullPath(Get("template")),
        Path.GetFullPath(Get("out")),
        Get("env"),
        values.GetValueOrDefault("dev-origin"),
        values.TryGetValue("kit", out var kit) ? Path.GetFullPath(kit) : null,
        Get("node", "node")));
    return 0;
}
catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or InvalidOperationException or InvalidDataException or KeyNotFoundException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static Dictionary<string, string> ParseArgs(string[] args)
{
    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i < args.Length; i += 2)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
            throw new ArgumentException($"引数が不正です: {args[i]}");
        values[args[i][2..]] = args[i + 1];
    }
    return values;
}
