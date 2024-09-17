using Microsoft.Win32;
using Newtonsoft.Json;
using SeleniumDriverSync.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SeleniumDriverSyncUI;

public class ChromeDriverManager
{
    private readonly HttpClient httpClient = new();

    private string ChromeVersion { get; set; }
    private string ZipName { get; set; }
    private string DriverName { get; set; }
    private string ZipFileName { get; set; }

    public async Task CheckandInstall(bool forceDownload, string targetPath)
    {
        ChromeVersion = await GetChromeVersion();

        string chromeVersion = ChromeVersion[..ChromeVersion.LastIndexOf('.')];

        SetZipProps();

        targetPath = Path.Combine(targetPath, DriverName);

        bool IsValid = await CheckValidity(targetPath, chromeVersion, forceDownload);

        if (IsValid == false)
        {
            return;
        }

        HttpResponseMessage chromeDriverVersionResponse = new(HttpStatusCode.OK);
        HttpResponseMessage driverZipResponse = new(HttpStatusCode.OK);

        //If current chrome version is 115 or higher use https://googlechromelabs.github.io/chrome-for-testing/last-known-good-versions-with-downloads.json
        if (Convert.ToInt32(chromeVersion[..chromeVersion.IndexOf('.')]) > 114)
        {
            chromeDriverVersionResponse = await httpClient.GetAsync("https://googlechromelabs.github.io/chrome-for-testing/known-good-versions-with-downloads.json");

            if (!chromeDriverVersionResponse.IsSuccessStatusCode)
            {
                if (chromeDriverVersionResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new Exception($"ChromeDriver version not found for Chrome version {chromeVersion}");
                }
                else
                {
                    throw new Exception($"ChromeDriver version request failed with status code: {chromeDriverVersionResponse.StatusCode}, reason phrase: {chromeDriverVersionResponse.ReasonPhrase}");
                }
            }

            var chromeDriverVersionJson = await chromeDriverVersionResponse
                .Content.ReadAsStringAsync();

            ChromeApiResponseModel? chromeVersionInfo = JsonConvert
                .DeserializeObject<ChromeApiResponseModel>(chromeDriverVersionJson);

            string? stableBuildDownloadUrl = null;

            List<Versions>? chromedriverVersionList = chromeVersionInfo?.Versions;

            Versions? versions = chromedriverVersionList?.LastOrDefault(v => v.Version.Contains(chromeVersion));

            if (versions == null)
            {
                string chV = chromeVersion[..chromeVersion.IndexOf('.')];
                List<Versions> newVersions = chromedriverVersionList.Where(x => x.Version.StartsWith(chV)).ToList();

                versions = newVersions.FirstOrDefault();
            }

            if (versions == null)
            {
                throw new Exception("Cannot sync chromedriver, please install manually!");
            }

            var drivers = versions.Downloads.Chromedriver;

            var win64Chromedriver = drivers.FirstOrDefault(d => d.Platform.Contains("win64"));

            if (win64Chromedriver != null)
            {
                stableBuildDownloadUrl = win64Chromedriver.Url;
            }
            else
            {
                // Handle the case where win64Chromedriver is null
                throw new Exception("Chromedriver cannot be found for this version of google chrome, please" +
                    "either update the chrome browser to a driver compatible version or if exists install compatible driver manually!");
            }

            driverZipResponse = await httpClient.GetAsync(stableBuildDownloadUrl);
        }
        else
        {
            chromeDriverVersionResponse = await httpClient.GetAsync($"https://chromedriver.storage.googleapis.com/LATEST_RELEASE_{chromeVersion}");
            string chromeDriverVersionLatest = await chromeDriverVersionResponse.Content.ReadAsStringAsync();

            driverZipResponse = await httpClient.GetAsync($"https://chromedriver.storage.googleapis.com/{chromeDriverVersionLatest}/{ZipName}");
        }

        var chromeDriverVersion = await chromeDriverVersionResponse.Content.ReadAsStringAsync();

        if (!driverZipResponse.IsSuccessStatusCode)
        {
            throw new Exception($"ChromeDriver download request failed with status code: {driverZipResponse.StatusCode}, reason phrase: {driverZipResponse.ReasonPhrase}");
        }

        bool IsExtractSuccessful = await ExtractAndWrite(driverZipResponse, targetPath);

        if (IsExtractSuccessful)
        {
            MessageBox.Show("ChromeDriver version synced with current Chrome version");
        }
        else
        {
            MessageBox.Show($"Failed to extract {targetPath}");
        }

        // on Linux/macOS, you need to add the executable permission (+x) to allow the execution of the chromedriver
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            using (var process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = String.Format("+x {0}", targetPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            ))
            {
                string error = await process.StandardError.ReadToEndAsync();

                try
                {
                    process.WaitForExit();
                    process.Kill();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);

                    return;
                    throw;
                }

                if (!string.IsNullOrEmpty(error))
                {
                    throw new Exception("Failed to make chromedriver executable");
                }
            }
        }

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

    public static async Task<string> GetChromeVersion()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                string? chromePath = (string?)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\chrome.exe", null, null);
                if (chromePath == null)
                {
                    throw new Exception("Google Chrome not found in registry");
                }
                else
                {
                    FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(chromePath);
                    if (fileVersionInfo.FileVersion != null)
                    {
                        return fileVersionInfo.FileVersion;
                    }
                    else
                    {
                        throw new Exception("File does not contain version number");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return string.Empty;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                using (var process = Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "google-chrome",
                        Arguments = "--product-version",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
                ))
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();

                    try
                    {
                        process.WaitForExit();
                        process.Kill();
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Error when reading version");
                    }

                    if (!string.IsNullOrEmpty(error))
                    {
                        throw new Exception(error);
                    }

                    return output;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred trying to execute 'google-chrome --product-version'", ex);
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                using var process = Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
                        Arguments = "--version",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
                );
                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                try
                {
                    process.WaitForExit();
                    process.Kill();
                }
                catch (Exception)
                {
                }

                if (!string.IsNullOrEmpty(error))
                {
                    throw new Exception(error);
                }

                output = output.Replace("Google Chrome ", "");
                return output;
            }
            catch (Exception ex)
            {
                throw new Exception($"An error occurred trying to execute '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome --version'", ex);
            }
        }
        else
        {
            throw new PlatformNotSupportedException("Your operating system is not supported.");
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
                MessageBox.Show("Couldn't write or extract the zip file");
                return false;
            }

        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
            return false;
        }
    }

    private async Task<bool> CheckValidity(string targetPath, string chromeVersion, bool forceDownload)
    {
        if (File.Exists(targetPath))
        {
            if (!forceDownload)
            {
                string chromeVersionNumber = chromeVersion[..chromeVersion.IndexOf('.')];

                using var process = Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = targetPath,
                        Arguments = "--version",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    }
                );

                if (process != null)
                {
                    string existingChromeDriverVersion = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();

                    try
                    {
                        process.WaitForExit();
                        process.Kill();

                        existingChromeDriverVersion = existingChromeDriverVersion.Split(' ')[1];
                        string existingDriverVersionNumber = existingChromeDriverVersion[..existingChromeDriverVersion.IndexOf('.')];

                        if (chromeVersionNumber == existingDriverVersionNumber)
                        {
                            MessageBox.Show("Chrome driver and chrome versions are already in sync.");
                            return false;
                        }

                        if (!string.IsNullOrEmpty(error))
                        {
                            throw new Exception($"Failed to execute {DriverName} --version");
                        }
                        return true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                        return false;
                    }
                }
                else
                {
                    MessageBox.Show("Couldn't check for chrome driver version");
                    return false;
                }
            }
            else
            {
                return true;
            }
        }
        else
        {
            MessageBox.Show($"Cannot find the driver at given path: {targetPath}");
            return false;
        }
    }
}
