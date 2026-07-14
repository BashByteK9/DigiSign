using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace DigiSign
{
    public static class AppSettingsLoader
    {
        public static string DefaultPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        public static AppSettings Load(string appSettingsPath = null, string legacyIpXmlPath = null)
        {
            appSettingsPath = appSettingsPath ?? DefaultPath;

            if (File.Exists(appSettingsPath))
            {
                try
                {
                    string json = File.ReadAllText(appSettingsPath);
                    return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
                catch (Exception ex)
                {
                    Logger.Error("Error parsing appsettings.json - using defaults", ex);
                    return new AppSettings();
                }
            }

            var migrated = TryMigrateFromLegacyIpXml(legacyIpXmlPath);
            var result = migrated ?? new AppSettings();
            Save(result, appSettingsPath);
            if (migrated != null)
                Logger.Info("Migrated legacy listener settings from IP.xml into appsettings.json");
            return result;
        }

        public static void Save(AppSettings settings, string appSettingsPath = null)
        {
            appSettingsPath = appSettingsPath ?? DefaultPath;
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(appSettingsPath, json);
        }

        private static AppSettings TryMigrateFromLegacyIpXml(string legacyIpXmlPath)
        {
            if (string.IsNullOrEmpty(legacyIpXmlPath) || !File.Exists(legacyIpXmlPath))
                return null;

            try
            {
                var xmlDoc = XDocument.Load(legacyIpXmlPath);
                var envelope = xmlDoc.Element("ENVELOPE");
                var fileNameLists = envelope?.Element("FILENAMELIST")?.Elements("FILENAMELIST").ToList();
                if (fileNameLists == null || fileNameLists.Count <= 15)
                    return null;

                var settings = new AppSettings();

                string verboseFlag = fileNameLists[11].Element("FILENAME")?.Value?.Trim().ToUpper();
                settings.VerboseMode = (verboseFlag == "Y");

                if (int.TryParse(fileNameLists[12].Element("FILENAME")?.Value?.Trim(), out int port) && port > 0)
                    settings.Port = port;

                settings.InvoiceApiBaseUrl = fileNameLists[13].Element("FILENAME")?.Value?.Trim();
                settings.InvoiceApiKey = fileNameLists[14].Element("FILENAME")?.Value?.Trim();

                string batchFlag = fileNameLists[15].Element("FILENAME")?.Value?.Trim().ToUpper();
                settings.LaunchInBatchMode = (batchFlag == "Y");

                return settings;
            }
            catch (Exception ex)
            {
                Logger.Error("Error migrating legacy IP.xml listener settings", ex);
                return null;
            }
        }
    }
}
