namespace SerialportCli.Natives
{
    using System;
    using System.Runtime.InteropServices;

    internal static class kernel32
    {
        internal const int STD_INPUT_HANDLE = -10;

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CancelIoEx(IntPtr handle, IntPtr lpOverlapped);
    }
}