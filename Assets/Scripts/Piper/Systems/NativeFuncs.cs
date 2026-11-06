using System;
using System.Runtime.InteropServices;

namespace PipeWorld.Piper
{
    public static class NativeFuncs
    {
        [DllImport("msvcrt.dll", EntryPoint = "malloc")]
        public static extern IntPtr malloc(Int64 size);

        [DllImport("msvcrt.dll", EntryPoint = "free")]
        public static extern void free(IntPtr memblock);

        [DllImport("msvcrt.dll", EntryPoint = "realloc")]
        public static extern IntPtr realloc(IntPtr memblock, Int64 size);

        [DllImport("msvcrt.dll", EntryPoint = "memcpy")]
        public static extern IntPtr memcpy(IntPtr dest, IntPtr src, Int64 count);

        [DllImport("msvcrt.dll", EntryPoint = "memset")]
        public static extern IntPtr memset(IntPtr dest, int c, UIntPtr byteCount);

        [DllImport("msvcrt.dll", EntryPoint = "memcmp")]
        public static extern int memcmp(IntPtr b1, IntPtr b2, UIntPtr count);

        [DllImport("kernel32.dll", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetDllDirectory(string lpPathName);


        public const uint MB_OK = 0x00000000;
        public const uint MB_RETRYCANCEL = 0x00000005;
        public const uint IDOK = 1;
        public const uint IDCANCEL = 2;
        public const uint IDRETRY = 4;
        public const uint MB_TOPMOST = 0x00040000;
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);
    }
}
