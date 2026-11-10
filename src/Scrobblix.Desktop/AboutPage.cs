using System.Reflection;

namespace Scrobblix.Desktop;

public sealed partial class MainWindow
{
    private void InitializeAboutBuildIdentity()
    {
        var assembly = typeof(MainWindow).Assembly;
        var information = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var sourceSeparator = information?.IndexOf('+') ?? -1;
        var version = sourceSeparator >= 0 ? information![..sourceSeparator] : information;
        var source = sourceSeparator >= 0 ? information![(sourceSeparator + 1)..] : null;
        AboutVersionText.Text = $"Version: {version ?? assembly.GetName().Version?.ToString() ?? "unknown"}";
        AboutSourceText.Text = string.IsNullOrWhiteSpace(source) ? "Source revision: not recorded in this build" : $"Source revision: {source}";
    }
}
