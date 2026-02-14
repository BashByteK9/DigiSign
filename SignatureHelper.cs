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
                // Use the legacy PrivateKey property to ensure PIN settings are respected
                // This is necessary for USB tokens where PIN has been set via SetPinForPrivateKey
                if (_certificate.PrivateKey is RSACryptoServiceProvider rsaCsp)
                {
                    HashAlgorithmName hashAlgorithm = HashAlgorithmName.SHA256;
                    return rsaCsp.SignData(message, hashAlgorithm, RSASignaturePadding.Pkcs1);
                }

                // Fallback to GetRSAPrivateKey for certificates not using CSP
                using (var rsa = _certificate.GetRSAPrivateKey())
                {
                    if (rsa == null)
                        throw new InvalidOperationException("RSA private key not found.");

                    HashAlgorithmName hashAlgorithm = HashAlgorithmName.SHA256;
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
