
using ACO_Optimizer.ViewModels;
using System.Windows.Controls;

namespace ACO_Optimizer.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            DataContext = new SettingsViewModel();
        }
    }
}
