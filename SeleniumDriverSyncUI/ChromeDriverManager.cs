using Microsoft.Win32;
using Newtonsoft.Json;
using SeleniumDriverSync.Models;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows;
using System.Configuration;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using SeleniumDriverSyncUI.Models;

namespace SeleniumDriverSyncUI;

public class ChromeDriverResult
{
    public bool Success { get; set; }
    public required string Message { get; set; }
    public Enums.Status Status { get; set; } = Enums.Status.Normal; // Default status
}

public class ChromeDriverManager
{
    private readonly HttpClient httpClient = new();

    private HttpResponseMessage _driverZipResponse = new(HttpStatusCode.OK);

    private string _errorMessage = string.Empty;

    private string ChromeVersion { get; set; }
    private string ZipName { get; set; }
    private string DriverName { get; set; }
    private string ZipFileName { get; set; }

    public async Task<ChromeDriverResult> CheckandInstall(bool forceDownload, string targetPath)
    {
        // Get the Chrome version using the GetChromeVersion method
        ChromeDriverResult chromeVersionDriverResult = GetChromeVersion();

        // Check if the Chrome version was successfully retrieved
        if (!chromeVersionDriverResult.Success)
        {
            return new ChromeDriverResult { Success = false, Message = chromeVersionDriverResult.Message, Status = chromeVersionDriverResult.Status };
        }

        // Get the current Chrome version
        ChromeVersion = chromeVersionDriverResult.Message;
        string chromeVersion = ChromeVersion[..ChromeVersion.LastIndexOf('.')];

        // Set the properties for the zip file and driver name based on the OS
        SetZipProps();
        // Combine the target path with the driver name
        targetPath = Path.Combine(targetPath, DriverName);


        bool isValid = await CheckValidity(targetPath, chromeVersion, forceDownload);
        if (!isValid) return new ChromeDriverResult { Success = false, Message = _errorMessage , Status = Enums.Status.Error };

        bool isFileValid = await GetDriverZipFile(chromeVersion);
        if (!isFileValid) return new ChromeDriverResult { Success = false, Message = _errorMessage, Status = Enums.Status.Error };

        if (!_driverZipResponse.IsSuccessStatusCode)
            return new ChromeDriverResult { Success = false, Message = $"Download failed: {_driverZipResponse.StatusCode}", Status = Enums.Status.Error };

        bool isExtractSuccessful = await ExtractAndWrite(_driverZipResponse, targetPath);
        if (!isExtractSuccessful) return new ChromeDriverResult { Success = false, Message = _errorMessage, Status = Enums.Status.Error };

        return new ChromeDriverResult { Success = true, Message = "ChromeDriver synced successfully.", Status = Enums.Status.Success };

        void SetZipProps()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ZipName = "chromedriver_win32.zip";
                ZipFileName = "chromedriver-win64";
                DriverName = "chromedriver.exe";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ZipName = "chromedriver_linux64.zip";
                ZipFileName = "chromedriver-linux64";
                DriverName = "chromedriver";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ZipName = "chromedriver_mac64.zip";
                ZipFileName = "chromedriver-mac-arm64";
                DriverName = "chromedriver";
            }
            else
            {
                throw new PlatformNotSupportedException("Your operating system is not supported.");
            }
        }
    }

    public static ChromeDriverResult GetChromeVersion()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                string? chromePath = (string?)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\chrome.exe", null, null);
                if (chromePath == null)
                {
                    return new ChromeDriverResult { Success = false, Message = "Google Chrome not found in registry", Status = Enums.Status.Error };
                }
                else
                {
                    FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(chromePath);
                    if (fileVersionInfo.FileVersion != null)
                    {
                        return new ChromeDriverResult { Success = true, Message = fileVersionInfo.FileVersion };
                    }
                    else
                    {
                        return new ChromeDriverResult { Success = false, Message = "File does not contain version number", Status = Enums.Status.Error };
                    }
                }
            }
            catch (Exception ex)
            {
                return new ChromeDriverResult { Success = true, Message = ex.Message, Status = Enums.Status.Error};
            }
        }
        else
        {
            return new ChromeDriverResult { Success = true, Message = "Your operating system is not supported.", Status = Enums.Status.Warning };
        }
    }

    private async Task<bool> ExtractAndWrite(HttpResponseMessage driverZipResponse, string targetPath)
    {
        try
        {
            using Stream zipFileStream = await driverZipResponse.Content.ReadAsStreamAsync();
            using ZipArchive zipArchive = new ZipArchive(zipFileStream, ZipArchiveMode.Read);
            using FileStream chromeDriverWriter = new FileStream(targetPath, FileMode.Create);
            ZipArchiveEntry? entry;

            if (Convert.ToInt32(ChromeVersion[..ChromeVersion.IndexOf('.')]) > 114)
            {
                entry = zipArchive.GetEntry(ZipFileName + "/" + DriverName);
            }
            else
            {
                entry = zipArchive.GetEntry(DriverName);
            }
            if (entry != null)
            {
                using Stream chromeDriverStream = entry.Open();
                await chromeDriverStream.CopyToAsync(chromeDriverWriter);

                return true;
            }
            else
            {
                _errorMessage = "Couldn't write or extract the zip file";
                return false;
            }

        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
            return false;
        }
    }

    private async Task<bool> CheckValidity(string targetPath, string chromeVersion, bool forceDownload)
    {
        if (!File.Exists(targetPath) && !forceDownload)
        {
            _errorMessage = $"Cannot find the driver at given path: {targetPath}";
            return false;
        }
        else
        {
            if (!forceDownload)
            {
                string chromeVersionNumber = chromeVersion[..chromeVersion.IndexOf('.')];

                var existingChromeDriverVersionResult = await GetChromeDriverVersion();

                if (!existingChromeDriverVersionResult.Success)
                {
                    _errorMessage = $"Error retrieving ChromeDriver version: {existingChromeDriverVersionResult.Message}";
                    return false;
                }

                string existingChromeDriverVersion = existingChromeDriverVersionResult.Message;

                try
                {
                    string existingDriverVersionNumber = existingChromeDriverVersion[..existingChromeDriverVersion.IndexOf('.')];

                    if (chromeVersionNumber == existingDriverVersionNumber)
                    {
                        _errorMessage = "Chrome driver and chrome versions are already in sync.";
                        return false;
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    _errorMessage = ex.Message;
                    return false;
                }
            }
            else
            {
                return true; // Force download, so we proceed with the download
            }
        }
    }

    private async Task<bool> GetDriverZipFile(string chromeVersion)
    {
        HttpResponseMessage chromeDriverVersionResponse = new(HttpStatusCode.OK);

        //If current chrome version is 115 or higher use https://googlechromelabs.github.io/chrome-for-testing/last-known-good-versions-with-downloads.json
        if (Convert.ToInt32(chromeVersion[..chromeVersion.IndexOf('.')]) > 114)
        {
            try
            {
                chromeDriverVersionResponse = await httpClient.GetAsync("https://googlechromelabs.github.io/chrome-for-testing/known-good-versions-with-downloads.json");

                if (!chromeDriverVersionResponse.IsSuccessStatusCode)
                {
                    _errorMessage = chromeDriverVersionResponse.StatusCode == HttpStatusCode.NotFound
                        ? $"ChromeDriver version not found for Chrome version {chromeVersion}"
                        : $"ChromeDriver version request failed with status code: {chromeDriverVersionResponse.StatusCode}" +
                        $", reason phrase: {chromeDriverVersionResponse.ReasonPhrase}";
                    return false;
                }

                string chromeDriverVersionJson = await chromeDriverVersionResponse.Content.ReadAsStringAsync();

                ChromeApiResponseModel? chromeVersionInfo = JsonConvert.DeserializeObject<ChromeApiResponseModel>(chromeDriverVersionJson);

                string? stableBuildDownloadUrl = null;

                if (chromeVersionInfo == null)
                {
                    _errorMessage = "Chrome version info cannot be taken";
                    return false;
                }
                else
                {
                    if (chromeVersionInfo.Versions != null)
                    {
                        List<Versions> chromedriverVersionList = chromeVersionInfo.Versions;

                        Versions? versions = chromedriverVersionList.LastOrDefault(v => v.Version.Contains(chromeVersion));

                        if (versions == null)
                        {
                            string chV = chromeVersion[..chromeVersion.IndexOf('.')];
                            List<Versions> newVersions = chromedriverVersionList.Where(x => x.Version.StartsWith(chV)).ToList();

                            versions = newVersions.FirstOrDefault();
                        }

                        if (versions == null)
                        {
                            _errorMessage = "Cannot sync chromedriver, please install manually!";
                            return false;
                        }

                        List<ChromeDriver>? drivers = versions.Downloads.Chromedriver;

                        ChromeDriver? win64Chromedriver = drivers.FirstOrDefault(d => d.Platform.Contains("win64"));

                        if (win64Chromedriver != null)
                        {
                            stableBuildDownloadUrl = win64Chromedriver.Url;
                        }
                        else
                        {
                            // Handle the case where win64Chromedriver is null
                            _errorMessage = "Chromedriver cannot be found for this version of google chrome, please" +
                                "either update the chrome browser to a driver compatible version or if exists install compatible driver manually!";
                            return false;
                        }

                        _driverZipResponse = await httpClient.GetAsync(stableBuildDownloadUrl);

                        if (_driverZipResponse == null)
                        {
                            _errorMessage = "Error getting stable chrome driver through url download";
                            return false;
                        }

                        if (!_driverZipResponse.IsSuccessStatusCode)
                        {
                            _errorMessage = $"Couldn't download the stable chrome driver through url: {stableBuildDownloadUrl}" +
                                $", Returned Status Code: {_driverZipResponse.StatusCode}, Reason Phrase: {_driverZipResponse.ReasonPhrase}";
                            return false;
                        }

                        return true;
                    }
                    else
                    {
                        _errorMessage = "Chrome version info cannot be taken";
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error when requesting chrome driver versions from api: {ex.Message}";
                return false;
            }
        }
        else
        {
            try
            {
                chromeDriverVersionResponse = await httpClient.GetAsync($"https://chromedriver.storage.googleapis.com/LATEST_RELEASE_{chromeVersion}");
                string chromeDriverVersionLatest = await chromeDriverVersionResponse.Content.ReadAsStringAsync();

                _driverZipResponse = await httpClient.GetAsync($"https://chromedriver.storage.googleapis.com/{chromeDriverVersionLatest}/{ZipName}");
                return true;
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error when requesting chrome driver versions from api: {ex.Message}";
                return false;
            }
        }
    }

    public static async Task<ChromeDriverResult> GetChromeDriverVersion()
    {
        try
        {
            string chromedriverPath = ConfigurationManager.AppSettings["DownloadPath"] + "\\chromedriver.exe";
            string versionFile = Path.Combine(Path.GetTempPath(), "chromever.txt");
            string batPath = Path.Combine(Path.GetTempPath(), "get-chromedriver-version.bat");

            // Create the batch file to get the ChromeDriver version
            string batContent = $"@echo off\r\n\"{chromedriverPath}\" --version > \"{versionFile}\"";
            File.WriteAllText(batPath, batContent);

            // Ensure the version file does not exist before running the batch file
            if (File.Exists(versionFile))
                File.Delete(versionFile);

            // Start the batch file to get the ChromeDriver version
            var psi = new ProcessStartInfo
            {
                FileName = batPath,
                UseShellExecute = true,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(psi);
            if (process != null)
            {
                process.WaitForExit();
            }
            else
            {
                return new ChromeDriverResult
                {
                    Success = false,
                    Message = "Failed to start the batch process.",
                    Status = Enums.Status.Error
                };
            }

            // Wait for the version file to be created
            var waitTask = Task.Run(async () =>
            {
                while (!File.Exists(versionFile))
                    await Task.Delay(100);
            });

            // Wait for the batch file to complete or timeout after 5 seconds
            Task completedTask = await Task.WhenAny(waitTask, Task.Delay(5000));
            if (completedTask != waitTask)
                return new ChromeDriverResult
                {
                    Success = false,
                    Message = "Timeout waiting for chromever.txt",
                    Status = Enums.Status.Error
                };

            string version = File.ReadAllText(versionFile);

            if (!version.StartsWith("ChromeDriver"))
            {
                return new ChromeDriverResult
                {
                    Success = false,
                    Message = "Unexpected format in version file.",
                    Status = Enums.Status.Error
                };
            }

            // Extract the version number from the output
            version = version.Split(' ')[1].Trim();

            // Clean up the batch file and version file
            File.Delete(batPath);
            File.Delete(versionFile);

            return new ChromeDriverResult
            {
                Success = true,
                Message = version,
                Status = Enums.Status.Success
            };
        }
        catch (Exception ex)
        {
            return new ChromeDriverResult
            {
                Success = false,
                Message = $"Error retrieving ChromeDriver version: {ex.Message}",
                Status = Enums.Status.Error
            };
        }
    }
}
