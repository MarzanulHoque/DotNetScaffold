namespace DotNetScaffold.Scaffolding;

/// <summary>
/// Pinned NuGet package versions used in scaffolded solutions. FluentAssertions is deliberately pinned
/// to the last Apache-2.0-licensed 7.x release — 8.0+ requires a commercial license above a revenue
/// threshold, which shouldn't be silently imposed on every solution this tool scaffolds.
/// </summary>
internal static class PackageVersions
{
    public const string EntityFrameworkCore = "8.0.29";
    public const string FluentAssertions = "7.2.2";
    public const string Moq = "4.20.72";
    public const string NetArchTestRules = "1.3.2";
}
