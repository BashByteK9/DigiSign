using System;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Management;

namespace DigiSign
{

    public class XmlData
    {
        public List<string> InputFilePaths { get; set; } = new List<string>();
        public string OutputFolderPath { get; set; }
        public string CommonName { get; set; }
        public string Pin { get; set; }
        public float XCoordinate { get; set; }
        public float YCoordinate { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string SignOnPage { get; set; } // F=First, E=Each, L=Last
        public string OpenOutputFolder { get; set; } // Y=Open, N=Not open
        public string SelfSignedPath { get; set; }
        public string SelfSignedPassword { get; set; }

    }

    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            string licensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.txt");
            string xmlFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IP.xml");
            var xmlData = ReadXmlData(xmlFilePath);
            //string pkcs11LibraryPath = @"C:\Windows\System32\Watchdata\PROXKey CSP India V3.0\wdpkcs.dll";


            //if (File.Exists(licensePath))
            //{
            //    if (ValidateLicense(licensePath))
            //    {
                    //Console.WriteLine("✅ License valid — Full Mode enabled.");

                    if (xmlData != null &&
                xmlData.InputFilePaths.Any() &&
                !string.IsNullOrEmpty(xmlData.OutputFolderPath) &&
                !string.IsNullOrEmpty(xmlData.CommonName))
            {
                string outputFolderPath = xmlData.OutputFolderPath;
                string commonName = xmlData.CommonName;
                string pin = xmlData.Pin;
                float xCoord = xmlData.XCoordinate;
                float yCoord = xmlData.YCoordinate;
                float width = xmlData.Width;
                float height = xmlData.Height;
                string signOnPage = xmlData.SignOnPage ?? "L"; // Default to Last page
                string openOutputFolder = xmlData.OpenOutputFolder ?? "Y"; // Default to Yes


                // Ensure output folder exists
                if (!Directory.Exists(outputFolderPath))
                {
                    Directory.CreateDirectory(outputFolderPath);
                }

                // Filter valid PDF files
                var validPdfFiles = xmlData.InputFilePaths
                    .Where(file => File.Exists(file) && Path.GetExtension(file).ToLower() == ".pdf")
                    .ToList();

                if (validPdfFiles.Any())
                {
                    //MessageBox.Show($"Found {validPdfFiles.Count} valid PDF files.");

                    // Use DigitalSignatureService for all signing operations
                    var signatureService = new DigitalSignatureService();
                    var cert = signatureService.LoadCertificate(commonName, pin, xmlData);

                    if (cert != null)
                    {
                        // Create signature configuration
                        var signatureConfig = new SignatureConfiguration(xCoord, yCoord, width, height, signOnPage);

                        // Process each PDF file
                        foreach (string inputPdfPath in validPdfFiles)
                        {
                            string inputFileName = Path.GetFileName(inputPdfPath);
                            string outputFileName = $"{inputFileName}";
                            string outputPdfPath = Path.Combine(outputFolderPath, outputFileName);

                            signatureService.SignPdf(inputPdfPath, outputPdfPath, cert, signatureConfig, pin, outputFolderPath);
                        }

                        // Open output folder if specified
                        if (openOutputFolder.Equals("Y", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                Process.Start("explorer.exe", outputFolderPath);

                            }
                            catch (Exception ex)
                            {
                                LogToFile($"Error opening output folder: {ex.Message}", outputFolderPath);
                            }
                        }
                    }
                    else
                    {
                        LogToFile($"Certificate not found {commonName}", outputFolderPath);
                    }
                }
                else
                {
                    LogToFile($"Error;File(s) Not Found", outputFolderPath); 
                }
            }
            else
            {
                LogToFile($"Error;Invalid XML data: Missing required fields.{xmlFilePath}", "");
            }
                }
        //        else
        //        {
        //            Console.WriteLine("License invalid or used on a different device — Demo Mode enabled.");
        //            // Restrict to demo features
        //        }
        //    }
        //    else
        //    {
        //        Console.WriteLine("Looking for license at: " + licensePath);
        //        Console.WriteLine("License file not found — Demo Mode enabled.");
        //        // Restrict to demo features
        //    }

        //}

        static bool ValidateLicense(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            var licenseData = lines.Select(line => line.Split('=')).ToDictionary(parts => parts[0], parts => parts[1]);

            string storedDeviceId = licenseData["DeviceID"];
            string storedHash = licenseData["DeviceHash"];
            string licenseNumber = licenseData["LicenseNumber"];
            string validUntil = licenseData["ValidUntil"];

            string currentDeviceId = GetDeviceId();

            if (storedDeviceId != currentDeviceId)
            {
                Console.WriteLine("Device mismatch.");
                return false;
            }

            string computedHash = GenerateDeviceHash(currentDeviceId, licenseNumber);
            if (computedHash != storedHash)
            {
                Console.WriteLine("Device hash mismatch.");
                return false;
            }

            if (!DateTime.TryParse(validUntil, out var validDate) || validDate < DateTime.Now)
            {
                Console.WriteLine("License expired.");
                return false;
            }

            return true;
        }

        static string GenerateDeviceHash(string deviceId, string licenseNumber)
        {
            string data = deviceId + "|" + licenseNumber;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(data));
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        static string GetDeviceId()
        {
            try
            {
                string cpuId = "";
                string diskId = "";

                var cpuSearcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                foreach (var obj in cpuSearcher.Get())
                {
                    cpuId = obj["ProcessorId"]?.ToString();
                    break;
                }

                var diskSearcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive");
                foreach (var obj in diskSearcher.Get())
                {
                    diskId = obj["SerialNumber"]?.ToString();
                    break;
                }

                return $"{cpuId}_{diskId}";
            }
            catch
            {
                return "UNKNOWN_DEVICE";
            }
        }

        static XmlData ReadXmlData(string xmlFilePath)
        {
            try
            {
                var xmlDoc = XDocument.Load(xmlFilePath);
                var envelope = xmlDoc.Element("ENVELOPE");
                if (envelope == null) return null;

                var fileNameLists = envelope.Element("FILENAMELIST")?.Elements("FILENAMELIST").ToList();
                if (fileNameLists == null || fileNameLists.Count < 10)
                {
                    LogToFile($"Error;Invalid or incomplete XML structure", "");
                    return null;
                }

                var xmlData = new XmlData();

                // 0: Input file paths
                foreach (var fileElem in fileNameLists[0].Elements("FILENAME"))
                {
                    string path = fileElem.Value.Trim();
                    if (!string.IsNullOrWhiteSpace(path))
                        xmlData.InputFilePaths.Add(path);
                }

                // 1: Output folder path
                xmlData.OutputFolderPath = fileNameLists[1].Element("FILENAME")?.Value.Trim();

                // 2: Common name
                xmlData.CommonName = fileNameLists[2].Element("FILENAME")?.Value.Trim();

                // 3: PIN
                xmlData.Pin = fileNameLists[3].Element("FILENAME")?.Value.Trim();

                // 4–7: Coordinates and dimensions
                float.TryParse(fileNameLists[4].Element("FILENAME")?.Value.Trim(), out float x);
                float.TryParse(fileNameLists[5].Element("FILENAME")?.Value.Trim(), out float y);
                float.TryParse(fileNameLists[6].Element("FILENAME")?.Value.Trim(), out float w);
                float.TryParse(fileNameLists[7].Element("FILENAME")?.Value.Trim(), out float h);

                xmlData.XCoordinate = x;
                xmlData.YCoordinate = y;
                xmlData.Width = w;
                xmlData.Height = h;

                // 8: Sign on page
                xmlData.SignOnPage = fileNameLists[8].Element("FILENAME")?.Value.Trim() ?? "L";

                // 9: Open output folder
                xmlData.OpenOutputFolder = fileNameLists[9].Element("FILENAME")?.Value.Trim() ?? "Y";

                // 10: Self-signed certificate path
                if (fileNameLists.Count > 10)
                    xmlData.SelfSignedPath = fileNameLists[10].Element("FILENAME")?.Value.Trim();

                // 11: Self-signed certificate password
                if (fileNameLists.Count > 11)
                    xmlData.SelfSignedPassword = fileNameLists[11].Element("FILENAME")?.Value.Trim();


                return xmlData;
            }
            catch (Exception ex)
            {
                LogToFile($"Error;parsing XML:{ex.Message}", "");
                return null;
            }
        }

        static void LogToFile(string message, string outputFolderPath)
        {
            try
            {
                string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plf.txt");
                string logMessage = $"{message}";
                File.WriteAllText(logFilePath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to write to log file: " + ex.Message);
            }
        }
    }
}
