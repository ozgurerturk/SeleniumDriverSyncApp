using SeleniumDriverSyncUI.Commands;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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

        string path = Assembly.GetExecutingAssembly().Location;

        await chromeDriverManager.CheckandInstall(_isSelected, path);
    }
}
