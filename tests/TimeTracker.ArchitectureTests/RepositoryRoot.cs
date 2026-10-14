using System.Reflection;

namespace TimeTracker.ArchitectureTests;

internal static class RepositoryRoot
{
    public static string Path { get; } = System.IO.Path.GetFullPath(
        typeof(RepositoryRoot).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "RepositoryRoot").Value!);
}
