using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AdminKeyTester
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("???????????????????????????????????????????????????????????");
            Console.WriteLine("  DigiSign Admin Key Tester & Generator");
            Console.WriteLine("  Uses EXACT same code as DigiSign application");
            Console.WriteLine("???????????????????????????????????????????????????????????");
            Console.WriteLine();

            string adminId = "TENINFOTECH";
            string validUntil = "2030-12-31";

            // Generate the key using EXACT same method as DigiSign
            string adminKey = GenerateAdminKey(adminId);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("? CORRECT Admin License Content:");
            Console.ResetColor();
            Console.WriteLine("???????????????????????????????????????????????????????????");
            Console.WriteLine($"AdminID={adminId}");
            Console.WriteLine($"AdminKey={adminKey}");
            Console.WriteLine($"ValidUntil={validUntil}");
            Console.WriteLine("???????????????????????????????????????????????????????????");
            Console.WriteLine();

            // Create the license file
            string content = $"AdminID={adminId}\r\nAdminKey={adminKey}\r\nValidUntil={validUntil}";
            
            try
            {
                string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "admin.license");
                File.WriteAllText(outputPath, content);
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("? admin.license file created successfully!");
                Console.ResetColor();
                Console.WriteLine($"Location: {outputPath}");
                Console.WriteLine();
                Console.WriteLine("Copy this file to your DigiSign directory:");
                Console.WriteLine("  D:\\Development\\DigiSign\\");
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
            // Simple hash for admin key - in production use a more secure method
            using (SHA256 sha = SHA256.Create())
            {
                string data = adminId + "|DIGISIGN_ADMIN_SECRET";
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }
    }
}
