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
            string xmlFilePath = @"D:\IP.xml";  // Replace with your actual XML file path
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

                Console.WriteLine("Pin: " + pin);
                Console.WriteLine("SignOnPage: " + signOnPage);
                Console.WriteLine("OpenOutputFolder: " + openOutputFolder);

                // Ensure output folder exists
                if (!Directory.Exists(outputFolderPath))
                {
                    Directory.CreateDirectory(outputFolderPath);
                    Console.WriteLine($"Created output folder: {outputFolderPath}");
                }

                // Filter valid PDF files
                var validPdfFiles = xmlData.InputFilePaths
                    .Where(file => File.Exists(file) && Path.GetExtension(file).ToLower() == ".pdf")
                    .ToList();

                if (validPdfFiles.Any())
                {
                    Console.WriteLine($"Found {validPdfFiles.Count} valid PDF files.");
                    var cert = LoadCertificateFromUSBToken(commonName, pin);

                    if (cert != null)
                    {
                        Console.WriteLine("Certificate found: " + cert.Subject);

                        // Process each PDF file
                        foreach (string inputPdfPath in validPdfFiles)
                        {
                            Console.WriteLine($"Processing PDF: {inputPdfPath}");
                            string inputFileName = Path.GetFileNameWithoutExtension(inputPdfPath);
                            string outputFileName = $"{inputFileName}_signed.pdf";
                            string outputPdfPath = Path.Combine(outputFolderPath, outputFileName);

                            SignPdfWithITextSharp(inputPdfPath, outputPdfPath, cert, xCoord, yCoord, width, height, signOnPage, pin, outputFolderPath);
                        }

                        // Open output folder if specified
                        if (openOutputFolder.Equals("Y", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                Process.Start("explorer.exe", outputFolderPath);
                                Console.WriteLine("Opened output folder.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error opening output folder: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Certificate not found.");
                    }
                }
                else
                {
                    Console.WriteLine("No valid PDF files found in the specified input paths.");
                }
            }
            else
            {
                Console.WriteLine("Invalid XML data: Missing required fields.");
            }

            Console.ReadKey();
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
                    Console.WriteLine("Invalid or incomplete XML structure.");
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

                // Debug output
                //Console.WriteLine("Parsed XML Data:");
                //Console.WriteLine($"InputFilePaths: {string.Join(", ", xmlData.InputFilePaths)}");
                //Console.WriteLine($"OutputFolderPath: {xmlData.OutputFolderPath}");
                //Console.WriteLine($"CommonName: {xmlData.CommonName}");
                //Console.WriteLine($"Pin: {xmlData.Pin}");
                //Console.WriteLine($"XCoordinate: {xmlData.XCoordinate}");
                //Console.WriteLine($"YCoordinate: {xmlData.YCoordinate}");
                //Console.WriteLine($"Width: {xmlData.Width}");
                //Console.WriteLine($"Height: {xmlData.Height}");
                //Console.WriteLine($"SignOnPage: {xmlData.SignOnPage}");
                //Console.WriteLine($"OpenOutputFolder: {xmlData.OpenOutputFolder}");

                return xmlData;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing XML: " + ex.Message);
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

                Console.WriteLine($"Found {certs.Count} certificate(s) for Common Name: {commonName}");

                if (certs.Count == 0)
                {
                    Console.WriteLine("No matching certificates found.");
                    return null;
                }

                var cert = certs[0];

                // Attempt to access the private key to trigger PIN prompt
                //try
                //{
                //    var privateKey = cert.PrivateKey;
                //}
                //catch (Exception ex)
                //{
                //    Console.WriteLine("Failed to access the private key. Ensure the USB token is inserted and unlocked.");
                //    Console.WriteLine($"Details: {ex.Message}");
                //    return null;
                //}
                cert.SetPinForPrivateKey(pin);
                return cert;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading certificate: {ex.Message}");
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
                Console.WriteLine($"Signature Text: {signatureText}");

                // Setup PDF reader
                PdfReader reader = new PdfReader(inputPath);
                int pageCount = reader.NumberOfPages;
                Console.WriteLine($"PDF has {pageCount} pages.");

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
                        Console.WriteLine($"Page {page} size: {pageWidth}x{pageHeight}");

                        // Validate coordinates
                        float adjustedX = x;
                        float adjustedY = y;
                        float adjustedWidth = width;
                        float adjustedHeight = height;

                        if (x < 0 || y < 0 || x + width > pageWidth || y + height > pageHeight)
                        {
                            Console.WriteLine($"Warning: Signature rectangle is outside page {page} boundaries. Adjusting coordinates.");
                            adjustedX = Math.Max(50, x);
                            adjustedY = Math.Max(50, y);
                            adjustedWidth = Math.Min(width, pageWidth - adjustedX - 50);
                            adjustedHeight = Math.Min(height, pageHeight - adjustedY - 50);
                        }
                        Console.WriteLine($"Signature Rectangle on page {page}: x={adjustedX}, y={adjustedY}, width={adjustedWidth}, height={adjustedHeight}");

                        // Define visible signature area for the current page
                        appearance.SetVisibleSignature(new iTextSharp.text.Rectangle(adjustedX, adjustedY, adjustedX + adjustedWidth, adjustedY + adjustedHeight), page, $"sig_{page}");

                        // Disable default Layer2 text to avoid double rendering
                        appearance.Layer2Text = string.Empty;

                        // Draw on the actual page content
                        PdfContentByte over = stamper.GetOverContent(page);
                        over.SaveState();                      

                        // Draw text with wrapping
                        BaseFont baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
                        float fontSize = 9;
                        float padding = 5;
                        float maxTextWidth = adjustedWidth - 2 * padding;
                        float leading = 11;
                        float maxY = adjustedY + adjustedHeight - padding;
                        float minY = adjustedY + padding;
                        float currentY = maxY - 5;

                        over.BeginText();
                        over.SetFontAndSize(baseFont, fontSize);
                        over.SetColorFill(BaseColor.BLACK);

                        foreach (string rawLine in signatureText.Split('\n'))
                        {
                            string line = rawLine.Trim();
                            if (string.IsNullOrEmpty(line)) continue;

                            string[] words = line.Split(' ');
                            string wrappedLine = "";

                            foreach (string word in words)
                            {
                                string testLine = string.IsNullOrEmpty(wrappedLine) ? word : wrappedLine + " " + word;
                                float lineWidth = baseFont.GetWidthPoint(testLine, fontSize);

                                if (lineWidth <= maxTextWidth)
                                {
                                    wrappedLine = testLine;
                                }
                                else
                                {
                                    if (currentY < minY) break;
                                    over.ShowTextAligned(Element.ALIGN_LEFT, wrappedLine, adjustedX + padding, currentY, 0);
                                    currentY -= leading;
                                    wrappedLine = word;
                                }
                            }

                            if (!string.IsNullOrEmpty(wrappedLine) && currentY >= minY)
                            {
                                over.ShowTextAligned(Element.ALIGN_LEFT, wrappedLine, adjustedX + padding, currentY, 0);
                                currentY -= leading;
                            }
                        }

                        over.EndText();
                        over.RestoreState();
                    }

                    // Ensure the certificate has the private key
                    if (!cert.HasPrivateKey)
                    {
                        Console.WriteLine("Certificate does not have a private key.");
                        return;
                    }

                    // Create a custom implementation of IExternalSignature
                    IExternalSignature externalSignature = new SafeCertificateSignature(cert, "SHA-256");

                    // Convert the certificate to a BouncyCastle certificate
                    Org.BouncyCastle.X509.X509Certificate bcCert = DotNetUtilities.FromX509Certificate(cert);

                    // Sign the document
                    MakeSignature.SignDetached(appearance, externalSignature, new[] { bcCert }, null, null, null, 0, CryptoStandard.CMS);

                    Console.WriteLine($"PDF signed successfully: {outputPath}");
                    LogToFile($"SUCCESS: Signed '{inputPath}' to '{outputPath}'", outputFolderPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while signing the PDF {inputPath}: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                LogToFile($"ERROR: Failed to sign '{inputPath}'. Exception: {ex.Message}", outputFolderPath);

            }


        }


        static void LogToFile(string message, string outputFolderPath)
        {
            try
            {
                string logFilePath = Path.Combine(outputFolderPath, "signing_log.txt");
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to write to log file: " + ex.Message);
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
