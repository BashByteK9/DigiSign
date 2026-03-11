using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DigiSign
{
    static class X509Certificate2Extension
    {
        public static void SetPinForPrivateKey(this X509Certificate2 certificate, string pin)
        {
            if (certificate == null) throw new ArgumentNullException("certificate");

            if (string.IsNullOrEmpty(pin))
            {
                throw new ArgumentException("PIN cannot be null or empty", "pin");
            }

            // Try CNG (modern) approach first
            try
            {
                using (var key = certificate.GetRSAPrivateKey())
                {
                    if (key != null)
                    {
                        // This is a CNG key
                        SetCngPin(certificate, pin);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"CNG PIN setting attempt failed: {ex.Message}");
            }

            // Fallback to legacy CSP approach
            try
            {
                if (certificate.PrivateKey == null)
                {
                    throw new InvalidOperationException("Certificate does not have a private key");
                }

                if (!(certificate.PrivateKey is RSACryptoServiceProvider))
                {
                    throw new InvalidOperationException("Certificate uses unsupported provider type");
                }

                var key = (RSACryptoServiceProvider)certificate.PrivateKey;

                var providerHandle = IntPtr.Zero;
                var pinBuffer = Encoding.ASCII.GetBytes(pin);

                // provider handle is implicitly released when the certificate handle is released.
                SafeNativeMethods.Execute(() => SafeNativeMethods.CryptAcquireContext(ref providerHandle,
                                                key.CspKeyContainerInfo.KeyContainerName,
                                                key.CspKeyContainerInfo.ProviderName,
                                                key.CspKeyContainerInfo.ProviderType,
                                                SafeNativeMethods.CryptContextFlags.Silent));
                SafeNativeMethods.Execute(() => SafeNativeMethods.CryptSetProvParam(providerHandle,
                                                SafeNativeMethods.CryptParameter.KeyExchangePin,
                                                pinBuffer, 0));
                SafeNativeMethods.Execute(() => SafeNativeMethods.CertSetCertificateContextProperty(
                                                certificate.Handle,
                                                SafeNativeMethods.CertificateProperty.CryptoProviderHandle,
                                                0, providerHandle));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to set PIN: {ex.Message}", ex);
            }
        }

        private static void SetCngPin(X509Certificate2 certificate, string pin)
        {
            IntPtr certContext = certificate.Handle;
            IntPtr keyHandle = IntPtr.Zero;
            int keySpec = 0;
            bool callerFree = false;

            try
            {
                // Get the private key handle from the certificate
                if (!SafeNativeMethods.CryptAcquireCertificatePrivateKey(
                    certContext,
                    SafeNativeMethods.CRYPT_ACQUIRE_CACHE_FLAG | SafeNativeMethods.CRYPT_ACQUIRE_ONLY_NCRYPT_KEY_FLAG,
                    IntPtr.Zero,
                    out keyHandle,
                    out keySpec,
                    out callerFree))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to acquire CNG private key");
                }

                // Set the PIN using NCryptSetProperty
                byte[] pinBytes = Encoding.Unicode.GetBytes(pin);
                int result = SafeNativeMethods.NCryptSetProperty(
                    keyHandle,
                    SafeNativeMethods.NCRYPT_PIN_PROPERTY,
                    pinBytes,
                    pinBytes.Length,
                    0);

                if (result != 0)
                {
                    throw new Win32Exception(result, "Failed to set CNG PIN property");
                }

                Logger.Debug("CNG PIN set successfully");
            }
            finally
            {
                if (callerFree && keyHandle != IntPtr.Zero)
                {
                    SafeNativeMethods.NCryptFreeObject(keyHandle);
                }
            }
        }
    }

    internal static class SafeNativeMethods
    {
        // CSP flags and enums
        internal enum CryptContextFlags
        {
            None = 0,
            Silent = 0x40
        }

        internal enum CertificateProperty
        {
            None = 0,
            CryptoProviderHandle = 0x1
        }

        internal enum CryptParameter
        {
            None = 0,
            KeyExchangePin = 0x20
        }

        // CNG constants
        internal const int CRYPT_ACQUIRE_CACHE_FLAG = 0x00000001;
        internal const int CRYPT_ACQUIRE_ONLY_NCRYPT_KEY_FLAG = 0x00040000;
        internal const string NCRYPT_PIN_PROPERTY = "SmartCardPin";

        // CSP P/Invoke declarations
        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool CryptAcquireContext(
            ref IntPtr hProv,
            string containerName,
            string providerName,
            int providerType,
            CryptContextFlags flags
            );

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool CryptSetProvParam(
            IntPtr hProv,
            CryptParameter dwParam,
            [In] byte[] pbData,
            uint dwFlags);

        [DllImport("CRYPT32.DLL", SetLastError = true)]
        internal static extern bool CertSetCertificateContextProperty(
            IntPtr pCertContext,
            CertificateProperty propertyId,
            uint dwFlags,
            IntPtr pvData
            );

        // CNG P/Invoke declarations
        [DllImport("crypt32.dll", SetLastError = true)]
        internal static extern bool CryptAcquireCertificatePrivateKey(
            IntPtr pCert,
            int dwFlags,
            IntPtr pvReserved,
            out IntPtr phCryptProvOrNCryptKey,
            out int dwKeySpec,
            out bool pfCallerFreeProvOrNCryptKey);

        [DllImport("ncrypt.dll", CharSet = CharSet.Unicode)]
        internal static extern int NCryptSetProperty(
            IntPtr hObject,
            string pszProperty,
            byte[] pbInput,
            int cbInput,
            int dwFlags);

        [DllImport("ncrypt.dll")]
        internal static extern int NCryptFreeObject(IntPtr hObject);

        public static void Execute(Func<bool> action)
        {
            if (!action())
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }
}
