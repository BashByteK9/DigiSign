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
                // Accessing PrivateKey can throw for CNG certificates
                var privateKey = certificate.PrivateKey;

                if (privateKey == null)
                {
                    throw new InvalidOperationException("Certificate does not have a private key");
                }

                if (!(privateKey is RSACryptoServiceProvider))
                {
                    throw new InvalidOperationException("Certificate uses CNG provider, CSP PIN setting not applicable");
                }

                var key = (RSACryptoServiceProvider)privateKey;

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

                Logger.Debug("CSP PIN set successfully");
            }
            catch (CryptographicException cex) when (cex.Message.Contains("Invalid provider type"))
            {
                // This is a CNG certificate, not a CSP certificate
                // CNG PIN setting already failed above, so we can't set the PIN
                Logger.Debug("Certificate uses CNG provider - PIN cannot be set programmatically");
                throw new InvalidOperationException($"Failed to set PIN: {cex.Message}", cex);
            }
            catch (Exception ex)
            {
                Logger.Debug($"CSP PIN setting failed: {ex.Message}");
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
                // Use CRYPT_ACQUIRE_ALLOW_NCRYPT_KEY_FLAG to properly work with CNG keys
                // Remove SILENT flag to allow UI if needed for initial authentication
                if (!SafeNativeMethods.CryptAcquireCertificatePrivateKey(
                    certContext,
                    SafeNativeMethods.CRYPT_ACQUIRE_CACHE_FLAG | SafeNativeMethods.CRYPT_ACQUIRE_ALLOW_NCRYPT_KEY_FLAG,
                    IntPtr.Zero,
                    out keyHandle,
                    out keySpec,
                    out callerFree))
                {
                    int error = Marshal.GetLastWin32Error();
                    Logger.Debug($"Failed to acquire private key, error code: {error} (0x{error:X})");
                    throw new Win32Exception(error, "Failed to acquire CNG private key");
                }

                Logger.Debug($"Acquired key handle, keySpec: {keySpec} (0x{keySpec:X}), callerFree: {callerFree}");

                // KeySpec will be CERT_NCRYPT_KEY_SPEC (0xFFFFFFFF) for CNG keys
                if (keySpec != SafeNativeMethods.CERT_NCRYPT_KEY_SPEC)
                {
                    Logger.Debug($"Not a pure CNG key (keySpec={keySpec}), skipping CNG PIN setting");
                    throw new InvalidOperationException("Not a CNG key");
                }

                // Set the PIN using NCryptSetProperty
                // Use Unicode encoding as required by NCrypt API
                byte[] pinBytes = Encoding.Unicode.GetBytes(pin);
                int result = SafeNativeMethods.NCryptSetProperty(
                    keyHandle,
                    SafeNativeMethods.NCRYPT_PIN_PROPERTY,
                    pinBytes,
                    pinBytes.Length,
                    SafeNativeMethods.NCRYPT_SILENT_FLAG);

                if (result != 0)
                {
                    Logger.Debug($"NCryptSetProperty failed with error code: {result} (0x{result:X})");
                    throw new Win32Exception(result, "Failed to set CNG PIN property");
                }

                Logger.Debug("CNG PIN set successfully - PIN will be cached for subsequent operations");
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
        internal const int CRYPT_ACQUIRE_COMPARE_KEY_FLAG = 0x00000004;
        internal const int CRYPT_ACQUIRE_SILENT_FLAG = 0x00000040;
        internal const int CRYPT_ACQUIRE_ALLOW_NCRYPT_KEY_FLAG = 0x00010000;
        internal const int CERT_NCRYPT_KEY_SPEC = unchecked((int)0xFFFFFFFF);
        internal const string NCRYPT_PIN_PROPERTY = "SmartCardPin";
        internal const int NCRYPT_SILENT_FLAG = 0x00000001;

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
