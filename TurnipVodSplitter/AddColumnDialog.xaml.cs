using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TurnipVodSplitter
{
    public partial class AddColumnDialog : Window
    {
        public string? NewColumnName { get; set; }

        public AddColumnDialog()
        {
            InitializeComponent();
        }

        private void OnOkClick(object sender, RoutedEventArgs e) {
            this.DialogResult = true;
            this.Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) {
            this.DialogResult = false;
            this.Close();
        }

        private void OnWindowClosed(object? sender, CancelEventArgs e) {
            this.NewColumnName = columnNameTextBox.Text;
        }
    }
}
