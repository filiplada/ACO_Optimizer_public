using ACO_Optimizer.Models;
using System.ComponentModel;

namespace ACO_Optimizer.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        public ACOParameters Parameters { get; set; } = new ACOParameters();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
