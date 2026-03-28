using System;
using System.Runtime.InteropServices;

namespace FurniOx.SolidWorks.Core.Connection;

internal static class ComHelper
{
    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    public static object? GetActiveObject(string progId)
    {
        var clsid = Type.GetTypeFromProgID(progId)?.GUID ?? Guid.Empty;
        if (clsid == Guid.Empty)
        {
            return null;
        }

        GetActiveObject(ref clsid, IntPtr.Zero, out var obj);
        return obj;
    }
}
