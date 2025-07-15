using Microsoft.Win32;
using SeleniumDriverSyncUI.Commands;
using SeleniumDriverSyncUI.Models;
using System;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SeleniumDriverSyncUI.ViewModels;

public class SyncingViewModel : INotifyPropertyChanged
{
    public ICommand SyncChromeCommand { get; set; }
    public ICommand SyncFirefoxCommand { get; set; }
    public ICommand SyncEdgeCommand { get; set; }
    public ICommand BrowseCommand { get; set; }

    public SyncingViewModel()
    {
        SyncChromeCommand = new RelayCommand(async obj => await SyncExecute(), CanChromeSyncExecute);
        SyncFirefoxCommand = new RelayCommand(DoNothing, CanSyncExecute);
        SyncEdgeCommand = new RelayCommand(DoNothing, CanSyncExecute);
        BrowseCommand = new RelayCommand(BrowseExecute, CanBrowseExecute);

        // Initialize _statusMessage to avoid CS8618 error  
        _statusMessage = string.Empty;
        _statusEnum = Enums.Status.Normal; // Default status

        // Read from App.config on startup to show the default download path  
        var defaultPath = ConfigurationManager.AppSettings["DownloadPath"];
        _filePath = !string.IsNullOrEmpty(defaultPath) ? defaultPath : Directory.GetCurrentDirectory();

        // Initialize versions

        var chrResponse = ChromeDriverManager.GetChromeVersion();

        _chromeVersion = chrResponse.Success
            ? chrResponse.Message
            : "Unable to retrieve Chrome version";

        var chrDrResponse = ChromeDriverManager.GetChromeDriverVersion();

        _chromeDriverVersion = chrDrResponse.Result.Success
            ? chrDrResponse.Result.Message
            : "Unable to retrieve ChromeDriver version";

        _firefoxVersion = "Not yet implemented"; // Placeholder
        _edgeVersion = "Not yet implemented"; // Placeholder
        _edgeDriverVersion = "Not yet implemented"; // Placeholder
        _geckoDriverVersion = "Not yet implemented"; // Placeholder

        // Set the initial status message
        StatusMessage = "Ready to sync drivers.";
    }

    private string _statusMessage;
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); OnPropertyChanged(nameof(StatusFontColor)); }
    }

    private Enums.Status _statusEnum;
    public Enums.Status StatusEnum
    {
        get => _statusEnum;
        set
        {
            if (_statusEnum != value)
            {
                _statusEnum = value;
                OnPropertyChanged(nameof(StatusEnum));
                OnPropertyChanged(nameof(StatusFontColor));
            }
        }
    }

    public System.Windows.Media.Brush StatusFontColor
    {
        get
        {
            return StatusEnum switch
            {
                Enums.Status.Success => System.Windows.Media.Brushes.Green,
                Enums.Status.Error => System.Windows.Media.Brushes.Red,
                Enums.Status.Warning => System.Windows.Media.Brushes.DarkOrange,
                Enums.Status.Info => System.Windows.Media.Brushes.DodgerBlue,
                Enums.Status.Normal => System.Windows.Media.Brushes.Black,
                _ => System.Windows.Media.Brushes.Black
            };
        }
    }

    private string _chromeVersion;
    public string ChromeVersion
    {
        get => _chromeVersion;
        set { _chromeVersion = value; OnPropertyChanged(nameof(ChromeVersion)); }
    }

    private string _chromeDriverVersion;
    public string ChromeDriverVersion
    {
        get => _chromeDriverVersion;
        set { _chromeDriverVersion = value; OnPropertyChanged(nameof(ChromeDriverVersion)); }
    }

    private string _firefoxVersion;
    public string FirefoxVersion
    {
        get => _firefoxVersion;
        set { _firefoxVersion = value; OnPropertyChanged(nameof(FirefoxVersion)); }
    }

    private string _geckoDriverVersion;
    public string GeckoDriverVersion
    {
        get => _geckoDriverVersion;
        set { _geckoDriverVersion = value; OnPropertyChanged(nameof(GeckoDriverVersion)); }
    }

    private string _edgeVersion;
    public string EdgeVersion
    {
        get => _edgeVersion;
        set { _edgeVersion = value; OnPropertyChanged(nameof(EdgeVersion)); }
    }

    private string _edgeDriverVersion;
    public string EdgeDriverVersion
    {
        get => _edgeDriverVersion;
        set { _edgeDriverVersion = value; OnPropertyChanged(nameof(EdgeDriverVersion)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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

    private string _filePath;
    public string FilePath
    {
        get { return _filePath; }
        set
        {
            if (_filePath == value) { return; }
            _filePath = value;
            OnPropertyChanged(nameof(FilePath));
            UpdateDefaultDownloadPath(_filePath);
        }
    }

    // Update App.config value
    private static void UpdateDefaultDownloadPath(string newPath)
    {
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        config.AppSettings.Settings["DownloadPath"].Value = newPath;
        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");
    }


    private void DoNothing(object obj) { }
    private bool CanBrowseExecute(object obj)
    {
        return true; // Implement logic if needed
    }

    private bool CanSyncExecute(object obj)
    {
        return true;
    }

    private bool CanChromeSyncExecute(object obj)
    {
        var chromeVersion = ChromeDriverManager.GetChromeVersion();

        // Check if the Chrome version was successfully retrieved
        if (!chromeVersion.Success)
        {
            StatusMessage = chromeVersion.Message;
            return false;
        }

        return true;
    }

    private async Task SyncExecute()
    {
        StatusMessage = "Syncing Chrome driver...";
        StatusEnum = Enums.Status.Info;

        ChromeDriverManager chromeDriverManager = new();
        string? path = ConfigurationManager.AppSettings["DownloadPath"];

        if (!string.IsNullOrEmpty(path))
        {
            var result = await chromeDriverManager.CheckandInstall(_isSelected, path);
            StatusMessage = result.Message;
            StatusEnum = result.Status;
        }
        else
        {
            StatusMessage = "Path finding error";
            StatusEnum = Enums.Status.Error;
        }
    }

    private void BrowseExecute(object obj)
    {

        var dialog = new FolderBrowserDialog
        {
            Description = "Klasör seçiniz",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            FilePath = dialog.SelectedPath;
        }
    }
}
