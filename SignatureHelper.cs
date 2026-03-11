using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using iTextSharp.text.pdf.security;

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
