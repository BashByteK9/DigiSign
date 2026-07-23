using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
                    var jobj = JObject.Parse(json);

                    // Back-compat: older appsettings.json files use "LaunchInBatchMode" (checked = batch).
                    // Migrate it into "EnableListenerMode" (checked = listener) by inverting the value, so
                    // an upgraded install preserves whatever mode the customer was actually running before -
                    // this must be presence detection, not a value check, since a missing key and an
                    // explicit "false" are different things and mixing them up would silently default
                    // every upgrade to batch mode regardless of the customer's prior setting.
                    if (jobj["LaunchInBatchMode"] != null && jobj["EnableListenerMode"] == null)
                    {
                        bool legacyBatchMode = jobj["LaunchInBatchMode"].Value<bool>();
                        jobj["EnableListenerMode"] = !legacyBatchMode;
                        jobj.Remove("LaunchInBatchMode");

                        var migratedSettings = jobj.ToObject<AppSettings>() ?? new AppSettings();
                        Save(migratedSettings, appSettingsPath);
                        Logger.Info("Migrated legacy LaunchInBatchMode flag to EnableListenerMode (inverted) in appsettings.json");
                        return migratedSettings;
                    }

                    return jobj.ToObject<AppSettings>() ?? new AppSettings();
                }
                catch (Exception ex)
                {
                    Logger.Error("Error parsing appsettings.json - using defaults", ex);
                    return new AppSettings();
                }
            }

            var migrated = TryMigrateFromLegacyIpXml(legacyIpXmlPath);
            var result = migrated ?? LoadTemplateOrDefaults(appSettingsPath);
            Save(result, appSettingsPath);
            if (migrated != null)
                Logger.Info("Migrated legacy listener settings from IP.xml into appsettings.json");
            return result;
        }

        // Seeds a brand-new appsettings.json from appsettings.json.example (shipped next to the
        // exe by the installer) so a fresh install's real defaults - e.g. EnableListenerMode,
        // InvoiceApiBaseUrl - come from that one template file instead of a second hardcoded
        // copy in AppSettings.cs. Falls back to the class defaults if the template is missing
        // or unparsable.
        private static AppSettings LoadTemplateOrDefaults(string appSettingsPath)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(appSettingsPath));
            string templatePath = Path.Combine(dir ?? AppDomain.CurrentDomain.BaseDirectory, "appsettings.json.example");

            if (File.Exists(templatePath))
            {
                try
                {
                    var settings = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(templatePath));
                    if (settings != null)
                        return settings;
                }
                catch (Exception ex)
                {
                    Logger.Error("Error parsing appsettings.json.example template - using built-in defaults", ex);
                }
            }

            return new AppSettings();
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

                // Legacy flag encoded the old "batch mode" semantics ("Y" = batch) - invert for EnableListenerMode.
                string batchFlag = fileNameLists[15].Element("FILENAME")?.Value?.Trim().ToUpper();
                settings.EnableListenerMode = !(batchFlag == "Y");

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
