namespace DenialsCommandCenter.Tests;

public static class TestData
{
    public static string Dir { get; } = FindDataDir();

    public static string PathOf(params string[] parts) => Path.Combine(new[] { Dir }.Concat(parts).ToArray());

    private static string FindDataDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DenialsCommandCenter.slnx")))
            dir = dir.Parent;
        if (dir is null) throw new InvalidOperationException("Repository root (DenialsCommandCenter.slnx) not found.");
        return Path.Combine(dir.FullName, "data");
    }
}
