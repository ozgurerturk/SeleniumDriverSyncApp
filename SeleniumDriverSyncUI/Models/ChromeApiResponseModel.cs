using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeleniumDriverSync.Models
{
    public class ChromeApiResponseModel
    {
        public DateTime Timestamp { get; set; }
        public List<Versions>? Versions { get; set; }
    }
    public class Versions
    {
        public string? Version { get; set; }
        public string? Revision { get; set; }
        public Downloads? Downloads { get; set; }
    }

    public class Downloads
    {
        public List<Chrome>? Chrome { get; set; }
        public List<ChromeDriver>? Chromedriver { get; set; }

        [JsonProperty("chrome-headless-shell")]
        public List<ChromeHeadlessShell> Chromeheadlessshell { get; set; }
    }
    public class Chrome
    {
        public string? Platform { get; set; }
        public string? Url { get; set; }
    }

    public class ChromeDriver
    {
        public string? Platform { get; set; }
        public string? Url { get; set; }
    }

    public class ChromeHeadlessShell
    {
        public string? Platform { get; set; }
        public string? Url { get; set; }
    }
}
