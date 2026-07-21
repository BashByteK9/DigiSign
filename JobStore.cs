using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DigiSign
{
    /// <summary>
    /// Durable per-job checkpoint store - one JSON file per job under logs\jobs\{JobId}.json.
    /// This is the strongest durability .NET Framework 4.7.2 offers without a real write-ahead
    /// log (FileStream.Flush(true) + atomic rename): a drive with a volatile write cache could
    /// still lose the last few milliseconds of a write, but that's an accepted, documented risk
    /// for this low-volume, low-concurrency desktop app.
    /// </summary>
    internal static class JobStore
    {
        private static readonly string JobsFolder =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "jobs");

        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = { new StringEnumConverter() }
        };

        private static string PathFor(string jobId) => Path.Combine(JobsFolder, $"{jobId}.json");

        public static void Save(JobRecord record)
        {
            try
            {
                Directory.CreateDirectory(JobsFolder);
                string finalPath = PathFor(record.JobId);
                string tempPath = finalPath + ".tmp";
                string json = JsonConvert.SerializeObject(record, SerializerSettings);

                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                using (var writer = new StreamWriter(fs))
                {
                    writer.Write(json);
                    writer.Flush();
                    fs.Flush(true);
                }

                if (File.Exists(finalPath))
                    File.Replace(tempPath, finalPath, null);
                else
                    File.Move(tempPath, finalPath);
            }
            catch (Exception ex)
            {
                Logger.Warning($"JobStore.Save failed for job {record.JobId}: {ex.Message}");
            }
        }

        public static List<JobRecord> LoadAll()
        {
            var results = new List<JobRecord>();
            try
            {
                Directory.CreateDirectory(JobsFolder);
                foreach (var file in Directory.GetFiles(JobsFolder, "*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var record = JsonConvert.DeserializeObject<JobRecord>(json, SerializerSettings);
                        if (record != null)
                            results.Add(record);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"JobStore.LoadAll: corrupt job file '{file}' - {ex.Message}");
                        try
                        {
                            string corruptPath = file + ".corrupt";
                            if (File.Exists(corruptPath))
                                File.Delete(corruptPath);
                            File.Move(file, corruptPath);
                        }
                        catch (Exception moveEx)
                        {
                            Logger.Warning($"JobStore.LoadAll: could not quarantine corrupt file '{file}': {moveEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"JobStore.LoadAll failed: {ex.Message}");
            }
            return results;
        }

        public static void Delete(string jobId)
        {
            try
            {
                string path = PathFor(jobId);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                Logger.Warning($"JobStore.Delete failed for job {jobId}: {ex.Message}");
            }
        }

        /// <summary>Deletes on-disk terminal job files older than <paramref name="retention"/>. Never touches non-terminal (resumable) jobs.</summary>
        public static void Prune(TimeSpan retention)
        {
            try
            {
                Directory.CreateDirectory(JobsFolder);
                DateTime cutoffUtc = DateTime.UtcNow - retention;
                var terminalStages = new HashSet<JobStage> { JobStage.Completed, JobStage.Failed, JobStage.Cancelled };

                foreach (var file in Directory.GetFiles(JobsFolder, "*.json"))
                {
                    try
                    {
                        var record = JsonConvert.DeserializeObject<JobRecord>(File.ReadAllText(file), SerializerSettings);
                        if (record == null)
                            continue;

                        bool isOldTerminal = terminalStages.Contains(record.Stage)
                            && (record.CompletedAtUtc ?? record.StartedAtUtc) < cutoffUtc;

                        if (isOldTerminal)
                            File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"JobStore.Prune: skipping unreadable file '{file}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"JobStore.Prune failed: {ex.Message}");
            }
        }
    }
}
