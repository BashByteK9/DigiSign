using System;
using System.IO;
using System.Reflection;

namespace DigiSign
{
    /// <summary>
    /// Version information helper class with auto-incrementing build number
    /// Provides version strings and build date information for the application
    /// </summary>
    public static class VersionInfo
    {
        private static readonly System.Version _assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
        private static readonly DateTime _buildDate = GetBuildDate(Assembly.GetExecutingAssembly());
        
        /// <summary>
        /// Gets the build date from the assembly
        /// </summary>
        /// <param name="assembly">The assembly to extract build date from</param>
        /// <returns>Build date from assembly metadata or file timestamp</returns>
        private static DateTime GetBuildDate(Assembly assembly)
        {
            const string BuildVersionMetadataPrefix = "+build";
            
            var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (attribute?.InformationalVersion != null)
            {
                var value = attribute.InformationalVersion;
                var index = value.IndexOf(BuildVersionMetadataPrefix);
                if (index > 0)
                {
                    value = value.Substring(index + BuildVersionMetadataPrefix.Length);
                    if (DateTime.TryParseExact(value, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var result))
                    {
                        return result;
                    }
                }
            }
            
            // Fallback: Use assembly file's last write time
            return File.GetLastWriteTime(assembly.Location);
        }
        
        /// <summary>
        /// Gets the calculated build number based on date (days since 2000-01-01)
        /// </summary>
        private static int BuildNumber
        {
            get
            {
                var baseDate = new DateTime(2000, 1, 1);
                var days = (int)(_buildDate - baseDate).TotalDays;
                return days;
            }
        }
        
        /// <summary>
        /// Gets the calculated revision number (seconds since midnight / 2)
        /// </summary>
        private static int RevisionNumber
        {
            get
            {
                var midnight = _buildDate.Date;
                var seconds = (int)(_buildDate - midnight).TotalSeconds;
                return seconds / 2;
            }
        }
        
        /// <summary>
        /// Gets the full version string (e.g., "1.0.9145.31234")
        /// </summary>
        public static string FullVersion => $"{_assemblyVersion.Major}.{_assemblyVersion.Minor}.{BuildNumber}.{RevisionNumber}";
        
        /// <summary>
        /// Gets the short version string (e.g., "1.0.9145")
        /// </summary>
        public static string ShortVersion => $"{_assemblyVersion.Major}.{_assemblyVersion.Minor}.{BuildNumber}";
        
        /// <summary>
        /// Gets the version for display in title bars (e.g., "v1.0.9145")
        /// </summary>
        public static string DisplayVersion => $"v{ShortVersion}";
        
        /// <summary>
        /// Gets the application title with version (e.g., "DigiSign v1.0.9145")
        /// </summary>
        public static string TitleWithVersion => $"DigiSign {DisplayVersion}";
        
        /// <summary>
        /// Gets the build date and time
        /// </summary>
        public static string BuildDate => _buildDate.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
