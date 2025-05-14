using System;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Security;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Diagnostics;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;
using Spire.Pdf.Graphics;
using System.Security.AccessControl;

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
    }

    internal class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            string xmlFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IP.xml");
            var xmlData = ReadXmlData(xmlFilePath);
            //string pkcs11LibraryPath = @"C:\Windows\System32\Watchdata\PROXKey CSP India V3.0\wdpkcs.dll";

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
                    var cert = LoadCertificateFromUSBToken(commonName, pin);

                    if (cert != null)
                    {
                        // Process each PDF file
                        foreach (string inputPdfPath in validPdfFiles)
                        {
                            string inputFileName = Path.GetFileName(inputPdfPath);
                            string outputFileName = $"{inputFileName}";
                            string outputPdfPath = Path.Combine(outputFolderPath, outputFileName);

                            SignPdfWithITextSharp(inputPdfPath, outputPdfPath, cert, xCoord, yCoord, width, height, signOnPage, pin, outputFolderPath);
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

                return xmlData;
            }
            catch (Exception ex)
            {
                LogToFile($"Error;parsing XML:{ex.Message}", "");
                return null;
            }
        }

    static X509Certificate2 LoadCertificateFromUSBToken(string commonName, string pin)
        {
            X509Store store = new X509Store(StoreLocation.CurrentUser);

            try
            {
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                var certs = store.Certificates.Find(X509FindType.FindBySubjectName, commonName, false);

    

                if (certs.Count == 0)
                {
                    LogToFile($"Error;No matching certificates found.{commonName}", "");
                    return null;
                }

                var cert = certs[0];
                cert.SetPinForPrivateKey(pin);
                return cert;
            }
            catch (Exception ex)
            {
                LogToFile($"Error; loading certificate::{commonName}", "");
                return null;
            }
            finally
            {
                store.Close();
            }
        }


    static void SignPdfWithITextSharp(string inputPath, string outputPath, X509Certificate2 cert, float x, float y, float width, float height, string signOnPage, string certPassword, string outputFolderPath)
        {
            try
            {

                // Extract CN from the certificate subject
                string cn = cert.Subject
                    .Split(',')
                    .Select(p => p.Trim())
                    .FirstOrDefault(p => p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                    ?.Substring(3) ?? "Unknown";

                string signatureText =
                    $"{cn}\nDigitally signed by {cn}\nDate: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";

                // Setup PDF reader
                PdfReader reader = new PdfReader(inputPath);
                int pageCount = reader.NumberOfPages;
              

                // Determine which pages to sign
                var pagesToSign = new List<int>();
                switch (signOnPage?.ToUpper())
                {
                    case "F":
                        pagesToSign.Add(1); // First page
                        break;
                    case "E":
                        pagesToSign.AddRange(Enumerable.Range(1, pageCount)); // Each page
                        break;
                    case "L":
                    default:
                        pagesToSign.Add(pageCount); // Last page
                        break;
                }

                using (FileStream os = new FileStream(outputPath, FileMode.Create))
                {
                    PdfStamper stamper = PdfStamper.CreateSignature(reader, os, '\0');
                    PdfSignatureAppearance appearance = stamper.SignatureAppearance;
                    appearance.Reason = "Digitally signed";

                    // Sign each specified page
                    foreach (int page in pagesToSign)
                    {
                        iTextSharp.text.Rectangle pageSize = reader.GetPageSize(page);
                        float pageWidth = pageSize.Width;
                        float pageHeight = pageSize.Height;
                        

                        // Validate coordinates
                        float adjustedX = x;
                        float adjustedY = y;
                        float adjustedWidth = width;
                        float adjustedHeight = height;

                        if (x < 0 || y < 0 || x + width > pageWidth || y + height > pageHeight)
                        {
                            LogToFile($"Error; Signature rectangle is outside page {page} boundaries. Adjusting coordinates", "");
                            adjustedX = Math.Max(50, x);
                            adjustedY = Math.Max(50, y);
                            adjustedWidth = Math.Min(width, pageWidth - adjustedX - 50);
                            adjustedHeight = Math.Min(height, pageHeight - adjustedY - 50);
                        }
                        //Console.WriteLine($"Signature Rectangle on page {page}: x={adjustedX}, y={adjustedY}, width={adjustedWidth}, height={adjustedHeight}");

                        // Define visible signature area for the current page
                        appearance.SetVisibleSignature(new iTextSharp.text.Rectangle(adjustedX, adjustedY, adjustedX + adjustedWidth, adjustedY + adjustedHeight), page, $"sig_{page}");

                        // Disable default Layer2 text to avoid double rendering
                        appearance.Layer2Text = string.Empty;

                        // Draw on the actual page content
                        PdfContentByte over = stamper.GetOverContent(page);
                        over.SaveState();

                        // Draw text with wrapping
                        BaseFont baseFontCN = BaseFont.CreateFont(BaseFont.TIMES_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED); // Font for CN
                        BaseFont baseFontText = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED); // Font for signature text

                        float fontSizeCN = 10; // Slightly larger font size for CN
                        float fontSizeText = 9; // Regular font size for signature text
                        float padding = 3;
                        float maxTextWidth = adjustedWidth - 2 * padding;
                        float leadingCN = fontSizeCN + 3; // Line spacing for CN
                        float leadingText = fontSizeText + 1; // Line spacing for signature text
                        float maxY = adjustedY + adjustedHeight - padding;
                        float minY = adjustedY + padding;
                        float currentY = maxY;

                        over.BeginText();
                        // Draw CN with wrapping
                        over.SetFontAndSize(baseFontCN, fontSizeCN);
                        over.SetColorFill(BaseColor.BLACK);
                        string cnLine = cn.Trim();
                        if (!string.IsNullOrEmpty(cnLine))
                        {
                            List<string> wrappedCNLines = new List<string>();
                            string[] words = cnLine.Split(' ');
                            string currentLine = "";
                            foreach (string word in words)
                            {
                                string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                                float lineWidth = baseFontCN.GetWidthPoint(testLine, fontSizeCN);
                                if (lineWidth <= maxTextWidth)
                                {
                                    currentLine = testLine;
                                }
                                else
                                {
                                    wrappedCNLines.Add(currentLine);
                                    currentLine = word;
                                }
                            }
                            if (!string.IsNullOrEmpty(currentLine))
                                wrappedCNLines.Add(currentLine);

                            foreach (string wrappedLine in wrappedCNLines)
                            {
                                if (currentY - leadingCN < minY) break;
                                over.ShowTextAligned(Element.ALIGN_LEFT, wrappedLine, adjustedX + padding, currentY, 0);
                                currentY -= leadingCN;
                            }
                        }

                        // Draw signature text (excluding the CN line)
                        over.SetFontAndSize(baseFontText, fontSizeText);
                        foreach (string rawLine in signatureText.Split('\n').Skip(1)) // Skip the first line (CN)
                        {
                            string line = rawLine.Trim();
                            if (string.IsNullOrEmpty(line)) continue;

                            List<string> wrappedLines = new List<string>();
                            string[] words = line.Split(' ');
                            string currentLine = "";

                            foreach (string word in words)
                            {
                                string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                                float lineWidth = baseFontText.GetWidthPoint(testLine, fontSizeText);

                                if (lineWidth <= maxTextWidth)
                                {
                                    currentLine = testLine;
                                }
                                else
                                {
                                    wrappedLines.Add(currentLine);
                                    currentLine = word;
                                }
                            }

                            if (!string.IsNullOrEmpty(currentLine))
                            {
                                wrappedLines.Add(currentLine);
                            }

                            foreach (string wrappedLine in wrappedLines)
                            {
                                if (currentY - leadingText < minY) break;
                                over.ShowTextAligned(Element.ALIGN_LEFT, wrappedLine, adjustedX + padding, currentY, 0);
                                currentY -= leadingText;
                            }
                        }

                        over.EndText();

                        over.RestoreState();
                    }


                    // Create a custom implementation of IExternalSignature
                    IExternalSignature externalSignature = new SafeCertificateSignature(cert, "SHA-256");

                    // Convert the certificate to a BouncyCastle certificate
                    Org.BouncyCastle.X509.X509Certificate bcCert = DotNetUtilities.FromX509Certificate(cert);

                    // Sign the document
                    MakeSignature.SignDetached(appearance, externalSignature, new[] { bcCert }, null, null, null, 0, CryptoStandard.CMS);

                 
                    LogToFile($"File(s) Signed Successfully - {Path.GetFileName(outputPath)}", outputFolderPath);
                }
            }
            catch (Exception ex)
            {
               
                LogToFile($"ERROR | [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] | Failed to sign '{inputPath}'. Exception: {ex.Message}", outputFolderPath);

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



        public class SafeCertificateSignature : IExternalSignature
        {
            private readonly X509Certificate2 _certificate;
            private readonly string _hashAlgorithm;

            public SafeCertificateSignature(X509Certificate2 certificate, string hashAlgorithm)
            {
                _certificate = certificate;
                _hashAlgorithm = hashAlgorithm;
            }

            public string GetHashAlgorithm() => _hashAlgorithm;

            public string GetEncryptionAlgorithm() => "RSA";

            public byte[] Sign(byte[] message)
            {

                using (var rsa = _certificate.GetRSAPrivateKey())
                {
                    if (rsa == null)
                        throw new InvalidOperationException("RSA private key not found.");

                    HashAlgorithmName hashAlgorithm = HashAlgorithmName.SHA256;

                    // Use Windows to handle the PIN prompt and signing
                    return rsa.SignData(message, hashAlgorithm, RSASignaturePadding.Pkcs1);
                }
            }
        }
    }
}
