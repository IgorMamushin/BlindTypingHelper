using System.Windows;

namespace BlindTypingHelper;

public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var mainView = new MainWindow();
        mainView.Show();
    }
}