using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace AppiumBuilder.Utils
{
    /// <summary>
    /// Windows DPAPI(CurrentUser)를 사용해 현재 Windows 계정에서만 복호화 가능한 형태로 값을 보호한다.
    /// </summary>
    public static class SecretStore
    {
        private const int CryptProtectUiForbidden = 0x1;

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int Size;
            public IntPtr Data;
        }

        [DllImport("Crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptProtectData(
            ref DataBlob dataIn,
            string? description,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            int flags,
            out DataBlob dataOut);

        [DllImport("Crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUnprotectData(
            ref DataBlob dataIn,
            IntPtr description,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            int flags,
            out DataBlob dataOut);

        [DllImport("Kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr memory);

        public static string ProtectToBase64(string plaintext)
        {
            byte[] input = Encoding.UTF8.GetBytes(plaintext);
            DataBlob inputBlob = CreateBlob(input);
            try
            {
                if (!CryptProtectData(ref inputBlob, "AppiumBuilder secret", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out DataBlob outputBlob))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                try
                {
                    byte[] output = new byte[outputBlob.Size];
                    Marshal.Copy(outputBlob.Data, output, 0, output.Length);
                    return Convert.ToBase64String(output);
                }
                finally
                {
                    if (outputBlob.Data != IntPtr.Zero) LocalFree(outputBlob.Data);
                }
            }
            finally
            {
                if (inputBlob.Data != IntPtr.Zero) Marshal.FreeHGlobal(inputBlob.Data);
            }
        }

        public static string UnprotectFromBase64(string protectedBase64)
        {
            byte[] input = Convert.FromBase64String(protectedBase64);
            DataBlob inputBlob = CreateBlob(input);
            try
            {
                if (!CryptUnprotectData(ref inputBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out DataBlob outputBlob))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                try
                {
                    byte[] output = new byte[outputBlob.Size];
                    Marshal.Copy(outputBlob.Data, output, 0, output.Length);
                    return Encoding.UTF8.GetString(output);
                }
                finally
                {
                    if (outputBlob.Data != IntPtr.Zero) LocalFree(outputBlob.Data);
                }
            }
            finally
            {
                if (inputBlob.Data != IntPtr.Zero) Marshal.FreeHGlobal(inputBlob.Data);
            }
        }

        private static DataBlob CreateBlob(byte[] data)
        {
            IntPtr pointer = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, pointer, data.Length);
            return new DataBlob { Size = data.Length, Data = pointer };
        }
    }
}
