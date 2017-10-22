using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ILUnpacker
{
    internal static class Memory
    {
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern bool VirtualProtect(
            IntPtr lpAddress,
            IntPtr dwSize,
            uint flNewProtect,
            out uint lpflOldProtect
        );

        internal static unsafe void Hook(MethodBase from, MethodBase to)
        {
            IntPtr intPtr = GetAddress(from);
            IntPtr intPtr2 = GetAddress(to);

            VirtualProtect(intPtr, (IntPtr) 5, 0x40, out var lpflOldProtect);

            if (IntPtr.Size == 8)
            {
                byte* ptr = (byte*) intPtr.ToPointer();
                *ptr = 0x49;
                ptr[1] = 0xbb;
                *(long*) (ptr + 0x2) = intPtr2.ToInt64();
                ptr[10] = 0x41;
                ptr[11] = 0xff;
                ptr[12] = 0xe3;
            }
            else if (IntPtr.Size == 4)
            {
                byte* ptr = (byte*) intPtr.ToPointer();
                *ptr = 0xe9;
                *(long*) (ptr + 0x1) = intPtr2.ToInt32() - intPtr.ToInt32() - 5;
                ptr[5] = 0xc3;
            }

            VirtualProtect(intPtr, (IntPtr) 5, lpflOldProtect, out var _);
        }

        private static IntPtr GetAddress(MethodBase methodBase)
        {
            RuntimeHelpers.PrepareMethod(methodBase.MethodHandle);
            return methodBase.MethodHandle.GetFunctionPointer();
        }
    }
}