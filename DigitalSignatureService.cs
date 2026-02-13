using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.security;
using Org.BouncyCastle.Security;

namespace DigiSign
{
    /// <summary>
    /// Service for digital signature operations on PDF documents
    /// </summary>
    public class DigitalSignatureService
    {
        /// <summary>
        /// Loads a certificate from the certificate store (USB token or system store)
        /// </summary>
        public X509Certificate2 LoadCertificate(string commonName, string pin, XmlData xmlData)
        {
            Logger.Debug($"Loading certificate with CN: {commonName}");
            X509Store[] stores = new X509Store[]
            {
                new X509Store(StoreName.My, StoreLocation.CurrentUser),
                new X509Store(StoreName.My, StoreLocation.LocalMachine)
            };

            foreach (var store in stores)
            {
                try
                {
                    Logger.Debug($"Searching certificates in {store.Location}\\{store.Name} store");
                    store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

                    // Step 1: Try USB token certificate first
                    var certs = store.Certificates.Find(X509FindType.FindBySubjectName, commonName, false);
                    Logger.Debug($"Found {certs.Count} certificate(s) matching name: {commonName}");

                    foreach (var cert in certs.Cast<X509Certificate2>())
                    {
                        Logger.Debug($"Certificate found - Subject: {cert.Subject}, Issuer: {cert.Issuer}, HasPrivateKey: {cert.HasPrivateKey}");

                        if (CertificateMatchesCN(cert, commonName))
                        {
                            if (cert.HasPrivateKey)
                            {
                                try
                                {
                                    // Try to get the legacy private key for PIN setup
                                    if (cert.PrivateKey is RSACryptoServiceProvider rsaCsp && rsaCsp.CspKeyContainerInfo.HardwareDevice)
                                    {
                                        if (!string.IsNullOrEmpty(pin))
                                        {
                                            cert.SetPinForPrivateKey(pin);
                                            Logger.Info("PIN set for hardware token certificate from IP.xml");
                                        }
                                        else
                                        {
                                            Logger.Warning("Hardware token detected but no PIN provided in IP.xml - user may be prompted");
                                        }
                                    }
                                    else
                                    {
                                        Logger.Debug("Certificate has private key, but not hardware token. No PIN needed");
                                    }
                                }
                                catch (CryptographicException ex)
                                {
                                    Logger.Warning($"Unable to access private key: {ex.Message}");
                                }
                            }

                            Logger.Info($"Certificate matched CN='{commonName}': {cert.Subject}");
                            return cert;
                        }

                        // Step 2: Check self-signed certificate if allowed
                        else if (xmlData.UseSelfSigned)
                        {
                            var selfSignedCert = store.Certificates
                                .Cast<X509Certificate2>()
                                .FirstOrDefault(c =>
                                    c.Subject == c.Issuer &&
                                    CertificateMatchesCN(c, commonName));

                            if (selfSignedCert != null)
                            {
                                Logger.Info($"Selected self-signed certificate: {selfSignedCert.Subject}");
                                return selfSignedCert;
                            }
                            else
                            {
                                Logger.Debug("No matching self-signed certificate found in this store");
                            }
                        }
                        else
                        {
                            Logger.Debug("Self-signed certificate selection disabled");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to open {store.Location}\\{store.Name} store", ex);
                }
                finally
                {
                    store.Close();
                }
            }

            // Not found
            Logger.Error($"No certificate found for CN='{commonName}' in any store");
            return null;
        }

        /// <summary>
        /// Signs a PDF document with a digital certificate
        /// </summary>
        public void SignPdf(string inputPath, string outputPath, X509Certificate2 cert,
            SignatureConfiguration config, string certPassword, string outputFolderPath,
            bool isVerboseMode = false, VerboseProgressForm verboseForm = null)
        {
            try
            {
                Logger.Info("Starting PDF processing - Full cryptographic signing mode");

                if (isVerboseMode && verboseForm != null)
                {
                    verboseForm.AppendInfo("Reading PDF file...");
                }

                // Extract CN from the certificate subject
                string cn = cert.Subject
                    .Split(',')
                    .Select(p => p.Trim())
                    .FirstOrDefault(p => p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                    ?.Substring(3) ?? "Unknown";

                string signatureText = $"{cn}\nDigitally signed by {cn}\nDate: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";

                Logger.Debug($"Signature text: {signatureText.Replace("\n", " | ")}");

                // Setup PDF reader
                PdfReader reader = new PdfReader(inputPath);
                int pageCount = reader.NumberOfPages;

                Logger.Debug($"PDF has {pageCount} pages");

                if (isVerboseMode && verboseForm != null)
                {
                    verboseForm.AppendInfo($"Pages: {pageCount}");
                    verboseForm.AppendInfo("Signature mode: FULL (cryptographic)");
                }

                // Determine which pages to process
                var pagesToProcess = config.GetPagesToSign(pageCount);

                if (isVerboseMode && verboseForm != null)
                {
                    string pageDesc;
                    switch (config.SignOnPage?.ToUpper())
                    {
                        case "F":
                            pageDesc = "First page only";
                            break;
                        case "E":
                            pageDesc = $"All {pageCount} pages";
                            break;
                        default:
                            pageDesc = "Last page only";
                            break;
                    }
                    verboseForm.AppendInfo($"Signing: {pageDesc}");
                }

                // Apply actual digital signature
                Logger.Info("Full mode: Applying cryptographic digital signature");

                if (isVerboseMode && verboseForm != null)
                {
                    verboseForm.AppendInfo("Creating signature...");
                }

                using (FileStream os = new FileStream(outputPath, FileMode.Create))
                {
                    PdfStamper stamper = PdfStamper.CreateSignature(reader, os, '\0');
                    PdfSignatureAppearance appearance = stamper.SignatureAppearance;
                    appearance.CertificationLevel = PdfSignatureAppearance.CERTIFIED_NO_CHANGES_ALLOWED;
                    appearance.Reason = "Digitally signed";
                    appearance.Acro6Layers = false;

                    // Sign each specified page
                    foreach (int page in pagesToProcess)
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
                        PdfContentByte over = stamper.GetOverContent(page);
                        DrawSignatureText(over, cn, signatureText, adjustedCoords.X, adjustedCoords.Y,
                            adjustedCoords.Width, adjustedCoords.Height);
                    }

                    if (isVerboseMode && verboseForm != null)
                    {
                        verboseForm.AppendInfo("Applying cryptographic signature...");
                    }

                    // Create a custom implementation of IExternalSignature
                    IExternalSignature externalSignature = new SignatureHelper.SafeCertificateSignature(cert, "SHA-256");

                    // Convert the certificate to a BouncyCastle certificate
                    Org.BouncyCastle.X509.X509Certificate bcCert = DotNetUtilities.FromX509Certificate(cert);

                    // Sign the document
                    var ocspClient = new OcspClientBouncyCastle();
                    ITSAClient tsaClient = null;

                    try
                    {
                        Logger.Debug("Attempting to get timestamp from DigiCert");
                        if (isVerboseMode && verboseForm != null)
                        {
                            verboseForm.AppendInfo("Requesting timestamp...");
                        }
                        tsaClient = new TSAClientBouncyCastle("http://timestamp.digicert.com");
                        Logger.Info("Timestamp service connected successfully");
                        if (isVerboseMode && verboseForm != null)
                        {
                            verboseForm.AppendSuccess("Timestamp acquired");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"TSA not available, proceeding without timestamp: {ex.Message}");
                        if (isVerboseMode && verboseForm != null)
                        {
                            verboseForm.AppendWarning("Timestamp unavailable (continuing without)");
                        }
                    }

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

                    Logger.Info($"PDF digitally signed successfully: {Path.GetFileName(outputPath)}");

                    if (isVerboseMode && verboseForm != null)
                    {
                        verboseForm.AppendInfo($"Saving signed PDF: {Path.GetFileName(outputPath)}");
                    }
                }

                reader.Close();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to process PDF: {Path.GetFileName(inputPath)}", ex);
                Logger.LogToPlf($"ERROR: Failed to process '{Path.GetFileName(inputPath)}' - {ex.Message}", isError: true);
                throw; // Re-throw to be caught in calling method
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

        private bool CertificateMatchesCN(X509Certificate2 cert, string commonName)
        {
            // Extract CN part from the subject
            var cnPart = cert.Subject
                .Split(',')
                .Select(p => p.Trim())
                .FirstOrDefault(p => p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))?
                .Substring(3);

            return string.Equals(cnPart, commonName, StringComparison.OrdinalIgnoreCase);
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
                Logger.Warning($"Signature rectangle outside page {page} boundaries. Adjusting coordinates");
                Logger.Debug($"Original: X={x}, Y={y}, W={width}, H={height}, PageSize: {pageWidth}x{pageHeight}");
                adjustedX = Math.Max(50, x);
                adjustedY = Math.Max(50, y);
                adjustedWidth = Math.Min(width, pageWidth - adjustedX - 50);
                adjustedHeight = Math.Min(height, pageHeight - adjustedY - 50);
                Logger.Debug($"Adjusted: X={adjustedX}, Y={adjustedY}, W={adjustedWidth}, H={adjustedHeight}");
            }

            return (adjustedX, adjustedY, adjustedWidth, adjustedHeight);
        }

        private void DrawSignatureText(PdfContentByte over, string cn, string signatureText,
            float adjustedX, float adjustedY, float adjustedWidth, float adjustedHeight)
        {
            over.SaveState();

            // Draw text with wrapping
            BaseFont baseFontCN = BaseFont.CreateFont(BaseFont.TIMES_BOLD, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
            BaseFont baseFontText = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);

            float fontSizeCN = 10;
            float fontSizeText = 9;
            float padding = 3;
            float maxTextWidth = adjustedWidth - 2 * padding;
            float leadingCN = fontSizeCN + 3;
            float leadingText = fontSizeText + 1;
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
                List<string> wrappedCNLines = WrapText(cnLine, baseFontCN, fontSizeCN, maxTextWidth);

                foreach (string wrappedLine in wrappedCNLines)
                {
                    if (currentY - leadingCN < minY) break;
                    over.ShowTextAligned(Element.ALIGN_LEFT, wrappedLine, adjustedX + padding, currentY, 0);
                    currentY -= leadingCN;
                }
            }

            // Draw signature text (skip first CN line since we already drew it)
            over.SetFontAndSize(baseFontText, fontSizeText);
            var signatureLines = signatureText.Split('\n').Skip(1).ToList();

            Logger.Debug($"Drawing {signatureLines.Count} signature text lines");

            foreach (string rawLine in signatureLines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                Logger.Debug($"Processing signature line: {line}");

                List<string> wrappedLines = WrapText(line, baseFontText, fontSizeText, maxTextWidth);

                foreach (string wrappedLine in wrappedLines)
                {
                    if (currentY - leadingText < minY) break;

                    over.SetColorFill(BaseColor.BLACK);
                    over.ShowTextAligned(Element.ALIGN_LEFT, wrappedLine, adjustedX + padding, currentY, 0);
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
    }
}
