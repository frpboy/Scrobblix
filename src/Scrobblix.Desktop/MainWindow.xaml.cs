using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Scrobblix.Desktop;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public void SetStartupStatus(string message, InfoBarSeverity severity)
    {
        StartupInfo.Message = message;
        StartupInfo.Severity = severity;
    }
}
