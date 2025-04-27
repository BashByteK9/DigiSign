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
    }

    internal static class SafeNativeMethods
    {
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

        public static void Execute(Func<bool> action)
        {
            if (!action())
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }
}
