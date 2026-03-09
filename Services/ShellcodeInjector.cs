using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PresenterShield.Services
{
    public static class ShellcodeInjector
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);
        
        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process([In] IntPtr process, [Out] out bool wow64Process);

        const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
        const uint MEM_COMMIT = 0x1000;
        const uint MEM_RESERVE = 0x2000;
        const uint PAGE_EXECUTE_READWRITE = 0x40;

        [StructLayout(LayoutKind.Sequential)]
        struct ThreadParams
        {
            public IntPtr hWnd;
            public uint dwAffinity;
            public uint padding;
            public IntPtr pSetWindowDisplayAffinity;
        }

        public static bool SetWindowDisplayAffinityRemote(IntPtr hWnd, uint processId, uint dwAffinity)
        {
            // Only works for x64 target process from x64 host
            if (IntPtr.Size != 8) return false;

            IntPtr hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, processId);
            if (hProcess == IntPtr.Zero) return false;

            try
            {
                IsWow64Process(hProcess, out bool isWow64);
                if (isWow64)
                {
                    // Target is 32-bit. We can't easily inject 64-bit shellcode.
                    return false;
                }

                IntPtr user32 = GetModuleHandle("user32.dll");
                IntPtr pSetAffinity = GetProcAddress(user32, "SetWindowDisplayAffinity");
                if (pSetAffinity == IntPtr.Zero) return false;

                ThreadParams args = new ThreadParams
                {
                    hWnd = hWnd,
                    dwAffinity = dwAffinity,
                    padding = 0,
                    pSetWindowDisplayAffinity = pSetAffinity
                };

                int structSize = Marshal.SizeOf(args);
                IntPtr pArgs = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)structSize, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
                if (pArgs == IntPtr.Zero) return false;

                byte[] argsBytes = new byte[structSize];
                IntPtr ptr = Marshal.AllocHGlobal(structSize);
                Marshal.StructureToPtr(args, ptr, false);
                Marshal.Copy(ptr, argsBytes, 0, structSize);
                Marshal.FreeHGlobal(ptr);

                WriteProcessMemory(hProcess, pArgs, argsBytes, (uint)structSize, out _);

                // x64 shellcode
                byte[] shellcode = new byte[]
                {
                    0x48, 0x8B, 0x51, 0x08,       // mov rdx, [rcx+8] (dwAffinity)
                    0x4C, 0x8B, 0x41, 0x10,       // mov r8, [rcx+16] (pSetWindowDisplayAffinity)
                    0x48, 0x8B, 0x09,             // mov rcx, [rcx] (hWnd)
                    0x48, 0x83, 0xEC, 0x28,       // sub rsp, 40
                    0x41, 0xFF, 0xD0,             // call r8
                    0x48, 0x83, 0xC4, 0x28,       // add rsp, 40
                    0xC3                          // ret
                };

                IntPtr pShellcode = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)shellcode.Length, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
                if (pShellcode == IntPtr.Zero) return false;

                WriteProcessMemory(hProcess, pShellcode, shellcode, (uint)shellcode.Length, out _);

                IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, pShellcode, pArgs, 0, out _);
                if (hThread != IntPtr.Zero)
                {
                    WaitForSingleObject(hThread, 5000);
                    GetExitCodeThread(hThread, out uint exitCode);
                    CloseHandle(hThread);
                    return exitCode == 1; // 1 = TRUE from SetWindowDisplayAffinity
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
            
            return false;
        }
    }
}
