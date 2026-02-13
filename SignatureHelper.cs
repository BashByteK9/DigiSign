using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using iTextSharp.text.pdf.security;

namespace DigiSign
{
    /// <summary>
    /// Helper classes for digital signature operations
    /// </summary>
    public static class SignatureHelper
    {
        /// <summary>
        /// Safe implementation of IExternalSignature for certificate-based signing with PIN caching support
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
                // Use legacy PrivateKey property to work with PIN caching
                if (_certificate.PrivateKey is RSACryptoServiceProvider rsaCsp)
                {
                    Logger.Debug("Using RSACryptoServiceProvider for signing (supports PIN caching)");

                    // Compute hash of the message
                    using (var sha256 = SHA256.Create())
                    {
                        byte[] hash = sha256.ComputeHash(message);
                        // Sign the hash using the private key (PIN already cached via SetPinForPrivateKey)
                        return rsaCsp.SignHash(hash, CryptoConfig.MapNameToOID("SHA256"));
                    }
                }
                else
                {
                    // Fallback to modern API if legacy provider not available
                    Logger.Debug("Using GetRSAPrivateKey for signing");
                    using (var rsa = _certificate.GetRSAPrivateKey())
                    {
                        if (rsa == null)
                            throw new InvalidOperationException("RSA private key not found.");

                        HashAlgorithmName hashAlgorithm = HashAlgorithmName.SHA256;

                        // This may trigger PIN prompt if PIN not cached
                        return rsa.SignData(message, hashAlgorithm, RSASignaturePadding.Pkcs1);
                    }
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
