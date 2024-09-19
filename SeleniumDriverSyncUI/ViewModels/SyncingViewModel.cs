using SeleniumDriverSyncUI.Commands;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace SeleniumDriverSyncUI.ViewModels;

public class SyncingViewModel
{
    public ICommand SyncChromeCommand { get; set; }
    public ICommand SyncFirefoxCommand { get; set; }
    public ICommand SyncEdgeCommand { get; set; }

    public SyncingViewModel()
    {
        SyncChromeCommand = new RelayCommand(SyncExecute, CanSyncExecute);
        SyncFirefoxCommand = new RelayCommand(DoNothing, CanSyncExecute);
        SyncEdgeCommand = new RelayCommand(DoNothing, CanSyncExecute);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get { return _isSelected; }
        set
        {
            if (_isSelected == value) { return; }

            _isSelected = value;
        }
    }

    private void DoNothing(object obj) { }

    private bool CanSyncExecute(object obj)
    {
        return true;
    }

    private async void SyncExecute(object obj)
    {
        ChromeDriverManager chromeDriverManager = new();

        string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        if (!String.IsNullOrEmpty(path))
        {
            await chromeDriverManager.CheckandInstall(_isSelected, path);
        }
        else
        {
            MessageBox.Show("Path finding error");
        }
    }
}
