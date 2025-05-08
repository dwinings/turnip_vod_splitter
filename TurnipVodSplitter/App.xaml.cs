using System;
using System.IO;
using System.Windows;

namespace TurnipVodSplitter {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        private void App_OnStartup(object sender, StartupEventArgs e) {
            MainWindow mainWindow;
            if (
                e.Args.Length > 0
                 && File.Exists(e.Args[0])
            ) {
                var uri = new Uri(e.Args[0]);
                mainWindow = new MainWindow(uri);
            } else {
                mainWindow = new MainWindow();
            }

            mainWindow.Show();
        }
    }
}
