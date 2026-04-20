using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.security;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;

namespace DigiSign
{
    public class DigitalSignatureService
    {
        /// <summary>
        /// Loads a certificate from USB token or falls back to self-signed certificate
        /// </summary>
        public X509Certificate2 LoadCertificate(string commonName, string pin, XmlData xmlData)
        {
            // Try searching in both CurrentUser and LocalMachine stores
            var storeLocations = new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine };

            foreach (var location in storeLocations)
            {
                X509Store store = new X509Store(StoreName.My, location);

                try
                {
                    store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                    Logger.Debug($"Searching for certificate '{commonName}' in {location} store");

                    // First try: exact subject name match
                    var certs = store.Certificates.Find(X509FindType.FindBySubjectName, commonName, false);

                    if (certs.Count > 0)
                    {
                        Logger.Info($"Found certificate '{commonName}' in {location} store");
                        var cert = certs[0];

                        // Only set PIN if certificate has a private key
                        if (cert.HasPrivateKey)
                        {
                            Logger.Debug("Certificate has private key, validating accessibility");

                            // Validate that private key is actually accessible (USB token connected)
                            try
                            {
                                using (var rsa = cert.GetRSAPrivateKey())
                                {
                                    if (rsa == null)
                                    {
                                        Logger.Error("Private key is not accessible - USB token may not be connected");
                                        LogToFile($"Error;Private key not accessible for '{commonName}' - USB token may not be connected", "");
                                        continue; // Try next store
                                    }
                                }
                            }
                            catch (Exception keyEx)
                            {
                                Logger.Error($"Private key is not accessible: {keyEx.Message} - USB token may not be connected");
                                LogToFile($"Error;Private key not accessible: {keyEx.Message}", "");
                                continue; // Try next store
                            }

                            // Now try to set PIN
                            try
                            {
                                if (!string.IsNullOrEmpty(pin))
                                {
                                    cert.SetPinForPrivateKey(pin);
                                    Logger.Debug("PIN set successfully");
                                }
                                else
                                {
                                    Logger.Info("No PIN provided - Windows will prompt for PIN during signing");
                                }
                            }
                            catch (Exception pinEx)
                            {
                                Logger.Warning($"Failed to set PIN: {pinEx.Message}");
                                Logger.Info("Certificate will be used anyway - Windows will prompt for PIN during signing");
                                LogToFile($"Warning;Failed to set PIN, user will be prompted: {pinEx.Message}", "");
                                // Continue and return the certificate - Windows will prompt for PIN
                            }
                        }
                        else
                        {
                            Logger.Warning("Certificate found but has no private key");
                        }
                        return cert;
                    }

                    // Second try: search by subject distinguished name (CN=...)
                    var certsWithCN = store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, $"CN={commonName}", false);

                    if (certsWithCN.Count > 0)
                    {
                        Logger.Info($"Found certificate with CN '{commonName}' in {location} store");
                        var cert = certsWithCN[0];

                        if (cert.HasPrivateKey)
                        {
                            Logger.Debug("Certificate has private key, validating accessibility");

                            // Validate that private key is actually accessible (USB token connected)
                            try
                            {
                                using (var rsa = cert.GetRSAPrivateKey())
                                {
                                    if (rsa == null)
                                    {
                                        Logger.Error("Private key is not accessible - USB token may not be connected");
                                        LogToFile($"Error;Private key not accessible for '{commonName}' - USB token may not be connected", "");
                                        continue; // Try next store
                                    }
                                }
                            }
                            catch (Exception keyEx)
                            {
                                Logger.Error($"Private key is not accessible: {keyEx.Message} - USB token may not be connected");
                                LogToFile($"Error;Private key not accessible: {keyEx.Message}", "");
                                continue; // Try next store
                            }

                            // Now try to set PIN
                            try
                            {
                                if (!string.IsNullOrEmpty(pin))
                                {
                                    cert.SetPinForPrivateKey(pin);
                                    Logger.Debug("PIN set successfully");
                                }
                                else
                                {
                                    Logger.Info("No PIN provided - Windows will prompt for PIN during signing");
                                }
                            }
                            catch (Exception pinEx)
                            {
                                Logger.Warning($"Failed to set PIN: {pinEx.Message}");
                                Logger.Info("Certificate will be used anyway - Windows will prompt for PIN during signing");
                                LogToFile($"Warning;Failed to set PIN, user will be prompted: {pinEx.Message}", "");
                                // Continue and return the certificate - Windows will prompt for PIN
                            }
                        }
                        else
                        {
                            Logger.Warning("Certificate found but has no private key");
                        }
                        return cert;
                    }

                    // Log available certificates for debugging
                    if (store.Certificates.Count > 0)
                    {
                        Logger.Debug($"Available certificates in {location} store: {store.Certificates.Count}");
                        foreach (X509Certificate2 availableCert in store.Certificates)
                        {
                            Logger.Debug($"  - Subject: {availableCert.Subject}");
                            Logger.Debug($"    HasPrivateKey: {availableCert.HasPrivateKey}, Thumbprint: {availableCert.Thumbprint}");
                        }
                    }
                    else
                    {
                        Logger.Debug($"No certificates found in {location} store");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Error searching {location} store: {ex.Message}");
                }
                finally
                {
                    store.Close();
                }
            }

            // No token found → try self-signed fallback
            if (!string.IsNullOrEmpty(xmlData.SelfSignedPath) && File.Exists(xmlData.SelfSignedPath))
            {
                Logger.Info($"Using self-signed certificate from {xmlData.SelfSignedPath}");
                return new X509Certificate2(
                    xmlData.SelfSignedPath,
                    xmlData.SelfSignedPassword,
                    X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet
                );
            }

            // Nothing found
            Logger.Error($"No USB token certificate or self-signed fallback found for '{commonName}'");
            return null;
        }

        /// <summary>
        /// Signs a PDF document with the provided certificate
        /// </summary>
        public void SignPdf(string inputPath, string outputPath, X509Certificate2 cert,
            SignatureConfiguration config, string certPassword, string outputFolderPath)
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
                    $"{cn}\n \nDate: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";

                // Setup PDF reader
                PdfReader reader = new PdfReader(inputPath);
                int pageCount = reader.NumberOfPages;

                // Determine which pages to sign
                var pagesToSign = config.GetPagesToSign(pageCount);

                using (FileStream os = new FileStream(outputPath, FileMode.Create))
                {
                    PdfStamper stamper = PdfStamper.CreateSignature(reader, os, '\0');
                    PdfSignatureAppearance appearance = stamper.SignatureAppearance;
                    appearance.CertificationLevel = PdfSignatureAppearance.CERTIFIED_NO_CHANGES_ALLOWED;
                    //appearance.Reason = "Digitally signed";
                    appearance.Acro6Layers = false;

                    // Sign each specified page
                    foreach (int page in pagesToSign)
                    {
                        iTextSharp.text.Rectangle pageSize = reader.GetPageSize(page);
                        float pageWidth = pageSize.Width;
                        float pageHeight = pageSize.Height;

                        // Validate and adjust coordinates
                        var adjustedCoords = ValidateAndAdjustCoordinates(
                            config.XCoordinate, config.YCoordinate,
                            config.Width, config.Height,
                            pageWidth, pageHeight, page);

                        // Define visible signature area for the current page
                        appearance.SetVisibleSignature(
                            new iTextSharp.text.Rectangle(
                                adjustedCoords.X,
                                adjustedCoords.Y,
                                adjustedCoords.X + adjustedCoords.Width,
                                adjustedCoords.Y + adjustedCoords.Height),
                            page,
                            $"sig_{page}");

                        // Disable default Layer2 text to avoid double rendering
                        appearance.Layer2Text = string.Empty;

                        // Draw signature appearance
                        DrawSignatureAppearance(stamper, page, adjustedCoords, cn, signatureText);
                    }

                    // Create signature with resilient TSA client
                    IExternalSignature externalSignature = new SignatureHelper.SafeCertificateSignature(cert, "SHA-256");
                    Org.BouncyCastle.X509.X509Certificate bcCert = DotNetUtilities.FromX509Certificate(cert);
                    var ocspClient = new OcspClientBouncyCastle();

                    // Use resilient TSA client that handles fallback internally
                    ITSAClient tsaClient = new SignatureHelper.ResilientTSAClient();

                    MakeSignature.SignDetached(
                        appearance,
                        externalSignature,
                        new[] { bcCert },
                        null,
                        ocspClient,
                        tsaClient,
                        0,
                        CryptoStandard.CMS
                    );

                    Logger.Info($"File(s) Signed Successfully - {Path.GetFileName(outputPath)}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"ERROR | [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] | Failed to sign '{inputPath}'. Exception: {ex.Message}");
                LogToFile($"Error;Failed to sign PDF: {ex.Message}", outputFolderPath);
                throw; // Re-throw so Program.cs can handle it properly
            }
        }

        /// <summary>
        /// Validates signatures in a PDF document
        /// </summary>
        public List<PdfSignatureValidator.SignatureValidationResult> ValidateSignatures(string pdfPath)
        {
            var results = new List<PdfSignatureValidator.SignatureValidationResult>();

            using (PdfReader reader = new PdfReader(pdfPath))
            {
                AcroFields af = reader.AcroFields;
                var signatureNames = af.GetSignatureNames();

                foreach (var name in signatureNames)
                {
                    PdfPKCS7 pkcs7 = af.VerifySignature(name);
                    bool valid = pkcs7.Verify();

                    results.Add(new PdfSignatureValidator.SignatureValidationResult
                    {
                        SignatureName = name,
                        IsValid = valid
                    });
                }
            }

            return results;
        }

        private (float X, float Y, float Width, float Height) ValidateAndAdjustCoordinates(
            float x, float y, float width, float height,
            float pageWidth, float pageHeight, int page)
        {
            float adjustedX = x;
            float adjustedY = y;
            float adjustedWidth = width;
            float adjustedHeight = height;

            if (x < 0 || y < 0 || x + width > pageWidth || y + height > pageHeight)
            {
                Logger.Error($"Error; Signature rectangle is outside page {page} boundaries. Adjusting coordinates");
                adjustedX = Math.Max(50, x);
                adjustedY = Math.Max(50, y);
                adjustedWidth = Math.Min(width, pageWidth - adjustedX - 50);
                adjustedHeight = Math.Min(height, pageHeight - adjustedY - 50);
            }

            return (adjustedX, adjustedY, adjustedWidth, adjustedHeight);
        }

        private void DrawSignatureAppearance(PdfStamper stamper, int page,
            (float X, float Y, float Width, float Height) coords,
            string cn, string signatureText)
        {
            PdfContentByte over = stamper.GetOverContent(page);
            over.SaveState();

            BaseFont baseFontCN = BaseFont.CreateFont(BaseFont.TIMES_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
            BaseFont baseFontText = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);

            float fontSizeCN = 10;
            float fontSizeText = 9;
            float padding = 3;
            float maxTextWidth = coords.Width - 2 * padding;
            float leadingCN = fontSizeCN + 3;
            float leadingText = fontSizeText + 1;
            float maxY = coords.Y + coords.Height - padding;
            float minY = coords.Y + padding;
            float currentY = maxY;

            over.BeginText();

            // Draw CN with wrapping
            over.SetFontAndSize(baseFontCN, fontSizeCN);
            over.SetColorFill(BaseColor.BLACK);
            string cnLine = cn.Trim();
            if (!string.IsNullOrEmpty(cnLine))
            {
                List<string> wrappedCNLines = WrapText(cnLine, baseFontCN, fontSizeCN, maxTextWidth);

                foreach (string wrappedLine in wrappedCNLines)
                {
                    if (currentY - leadingCN < minY) break;
                    over.ShowTextAligned(Element.ALIGN_LEFT, wrappedLine, coords.X + padding, currentY, 0);
                    currentY -= leadingCN;
                }
            }

            // Draw signature text (excluding the CN line)
            over.SetFontAndSize(baseFontText, fontSizeText);
            foreach (string rawLine in signatureText.Split('\n').Skip(1))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                List<string> wrappedLines = WrapText(line, baseFontText, fontSizeText, maxTextWidth);

                foreach (string wrappedLine in wrappedLines)
                {
                    if (currentY - leadingText < minY) break;
                    over.ShowTextAligned(Element.ALIGN_LEFT, wrappedLine, coords.X + padding, currentY, 0);
                    currentY -= leadingText;
                }
            }

            over.EndText();
            over.RestoreState();
        }

        private List<string> WrapText(string text, BaseFont font, float fontSize, float maxWidth)
        {
            List<string> wrappedLines = new List<string>();
            string[] words = text.Split(' ');
            string currentLine = "";

            foreach (string word in words)
            {
                string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                float lineWidth = font.GetWidthPoint(testLine, fontSize);

                if (lineWidth <= maxWidth)
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
                wrappedLines.Add(currentLine);

            return wrappedLines;
        }

        private void LogToFile(string message, string outputFolderPath)
        {
            try
            {
                string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plf.txt");
                string logMessage = $"{message}";
                File.WriteAllText(logFilePath, logMessage + Environment.NewLine);
            }
            catch
            {
                // Silently fail
            }
        }
    }
}
