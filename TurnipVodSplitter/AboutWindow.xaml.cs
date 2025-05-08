using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.Input;

namespace TurnipVodSplitter
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        public ICommand CloseThisCommand => new RelayCommand(this.Close);

        public string? VersionInfo {
            get {
                var version = Assembly.GetAssembly(this.GetType())?.GetName().Version;
                return version == null ? "unknown" : $"{version.Major}.{version.Minor}.{version.Revision}";
            }
        }

        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e) {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) {UseShellExecute = true});
            e.Handled = true;
        }
    }
}
