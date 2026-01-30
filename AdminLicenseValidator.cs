using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;

namespace AdminLicenseValidator
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("???????????????????????????????????????????????????????????");
            Console.WriteLine("  DigiSign Admin License Validator");
            Console.WriteLine("???????????????????????????????????????????????????????????");
            Console.WriteLine();

            string licenseFile = "admin.license";
            
            if (!File.Exists(licenseFile))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("? admin.license file not found in current directory!");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("Please run this tool in the same folder as admin.license");
                Console.WriteLine("or specify the path as a command line argument:");
                Console.WriteLine("  AdminLicenseValidator.exe \"C:\\path\\to\\admin.license\"");
                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            if (args.Length > 0 && File.Exists(args[0]))
            {
                licenseFile = args[0];
            }

            Console.WriteLine($"Validating: {Path.GetFullPath(licenseFile)}");
            Console.WriteLine();

            try
            {
                // Read and parse the file
                var lines = File.ReadAllLines(licenseFile);
                var data = new Dictionary<string, string>();

                Console.WriteLine("File Contents:");
                Console.WriteLine("???????????????????????????????????????????????????????????");
                foreach (var line in lines)
                {
                    Console.WriteLine($"  {line}");
                    
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                    
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        data[parts[0].Trim()] = parts[1].Trim();
                    }
                }
                Console.WriteLine("???????????????????????????????????????????????????????????");
                Console.WriteLine();

                // Check required fields
                Console.WriteLine("Validation Results:");
                Console.WriteLine("???????????????????????????????????????????????????????????");
                
                bool hasAdminId = data.ContainsKey("AdminID");
                bool hasAdminKey = data.ContainsKey("AdminKey");
                bool hasValidUntil = data.ContainsKey("ValidUntil");

                Console.WriteLine($"  AdminID field: {(hasAdminId ? "? Found" : "? Missing")}");
                Console.WriteLine($"  AdminKey field: {(hasAdminKey ? "? Found" : "? Missing")}");
                Console.WriteLine($"  ValidUntil field: {(hasValidUntil ? "? Found" : "? Missing")}");
                Console.WriteLine();

                if (!hasAdminId || !hasAdminKey || !hasValidUntil)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("? VALIDATION FAILED: Missing required fields!");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }

                string adminId = data["AdminID"];
                string adminKey = data["AdminKey"];
                string validUntil = data["ValidUntil"];

                Console.WriteLine("Field Values:");
                Console.WriteLine($"  AdminID: {adminId}");
                Console.WriteLine($"  AdminKey: {adminKey}");
                Console.WriteLine($"  ValidUntil: {validUntil}");
                Console.WriteLine();

                // Validate expiration
                if (DateTime.TryParse(validUntil, out var validDate))
                {
                    if (validDate < DateTime.Now)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"? License EXPIRED on: {validDate:yyyy-MM-dd}");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"? License valid until: {validDate:yyyy-MM-dd}");
                        Console.ResetColor();
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"? Invalid date format: {validUntil}");
                    Console.ResetColor();
                }
                Console.WriteLine();

                // Validate AdminKey
                string expectedKey = GenerateAdminKey(adminId);
                Console.WriteLine("AdminKey Validation:");
                Console.WriteLine($"  Expected: {expectedKey}");
                Console.WriteLine($"  Provided: {adminKey}");
                Console.WriteLine();

                if (adminKey == expectedKey)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("? AdminKey is CORRECT!");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("? AdminKey is INCORRECT!");
                    Console.ResetColor();
                    Console.WriteLine();
                    Console.WriteLine("The AdminKey in the file doesn't match the expected hash.");
                    Console.WriteLine("Use AdminKeyTester.exe to generate the correct key.");
                }

                Console.WriteLine("???????????????????????????????????????????????????????????");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"? Error: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        // EXACT SAME METHOD AS DIGISIGN APPLICATION
        static string GenerateAdminKey(string adminId)
        {
            using (SHA256 sha = SHA256.Create())
            {
                string data = adminId + "|DIGISIGN_ADMIN_SECRET";
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }
    }
}
