using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using iTextSharp.text.pdf.security;
using Org.BouncyCastle.Crypto;

namespace DigiSign
{
    /// <summary>
    /// Helper class for digital signature operations
    /// </summary>
    public static class SignatureHelper
    {
        /// <summary>
        /// Safe implementation of IExternalSignature for certificate-based signing
        /// </summary>
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
                HashAlgorithmName hashAlgorithm = HashAlgorithmName.SHA256;

                // Try to use the legacy PrivateKey property for CSP-based certificates
                // This ensures PIN settings are respected for legacy USB tokens
                try
                {
                    var privateKey = _certificate.PrivateKey;
                    if (privateKey is RSACryptoServiceProvider rsaCsp)
                    {
                        Logger.Debug("Using legacy RSACryptoServiceProvider for signing");
                        return rsaCsp.SignData(message, hashAlgorithm, RSASignaturePadding.Pkcs1);
                    }
                }
                catch (Exception ex)
                {
                    // PrivateKey property throws exception for CNG certificates
                    Logger.Debug($"PrivateKey access failed (likely CNG certificate): {ex.Message}");
                }

                // Use GetRSAPrivateKey for CNG-based certificates (modern USB tokens)
                Logger.Debug("Using GetRSAPrivateKey for signing (CNG)");
                using (var rsa = _certificate.GetRSAPrivateKey())
                {
                    if (rsa == null)
                        throw new InvalidOperationException("RSA private key not found.");

                    // For CNG keys, Windows will prompt for PIN if needed
                    return rsa.SignData(message, hashAlgorithm, RSASignaturePadding.Pkcs1);
                }
            }
        }

        /// <summary>
        /// TSA Client with automatic fallback to multiple timestamp servers
        /// </summary>
        public class ResilientTSAClient : ITSAClient
        {
            private static readonly string[] TsaServers = new[]
            {
                "http://timestamp.digicert.com",
                "http://timestamp.globalsign.com/tsa/r6advanced1",
                "http://timestamp.sectigo.com",
                "http://time.certum.pl",
                "http://tsa.startssl.com/rfc3161"
            };

            public int GetTokenSizeEstimate()
            {
                return 4096; // Standard estimate for TSA token size
            }

            public IDigest GetMessageDigest()
            {
                // Return SHA-256 digest for timestamp
                return new Org.BouncyCastle.Crypto.Digests.Sha256Digest();
            }

            public byte[] GetTimeStampToken(byte[] imprint)
            {
                Exception lastException = null;

                // Try each TSA server in sequence
                foreach (var tsaUrl in TsaServers)
                {
                    try
                    {
                        Logger.Debug($"Attempting to get timestamp from: {tsaUrl}");
                        var tsaClient = new TSAClientBouncyCastle(tsaUrl);
                        var token = tsaClient.GetTimeStampToken(imprint);

                        if (token != null && token.Length > 0)
                        {
                            Logger.Info($"Successfully obtained timestamp from: {tsaUrl}");
                            return token;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;

                        // Check if it's a network/DNS error
                        if (ex.Message.Contains("could not be resolved") || 
                            ex.Message.Contains("Unable to connect") ||
                            ex.Message.Contains("timeout") ||
                            ex.InnerException?.Message.Contains("could not be resolved") == true)
                        {
                            Logger.Warning($"TSA server {tsaUrl} unreachable: {ex.Message}");
                            // Continue to next server
                        }
                        else
                        {
                            // Log but continue to try other servers
                            Logger.Warning($"TSA server {tsaUrl} error: {ex.Message}");
                        }
                    }
                }

                // All servers failed - return null to sign without timestamp
                Logger.Warning($"All TSA servers failed. Last error: {lastException?.Message}");
                Logger.Warning("Signing without timestamp (signature will still be valid)");
                return null; // iTextSharp will handle null gracefully
            }
        }
    }

    /// <summary>
    /// Validator for PDF signatures
    /// </summary>
    public class PdfSignatureValidator
    {
        public class SignatureValidationResult
        {
            public string SignatureName { get; set; }
            public bool IsValid { get; set; }
        }
    }
}
