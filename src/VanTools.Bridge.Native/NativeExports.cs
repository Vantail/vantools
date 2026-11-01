using System.Runtime.InteropServices;
using VanTools.Pack;

namespace VanTools.Bridge.Native;

public static class NativeExports {
    [UnmanagedCallersOnly(EntryPoint = "van_pack_cs")]
    public static int Pack(nint sourcePtr, nint targetPtr) {
        try {
            if (sourcePtr == IntPtr.Zero || targetPtr == IntPtr.Zero) {
                return 1;
            }

            string? source = Marshal.PtrToStringUTF8(sourcePtr);
            string? target = Marshal.PtrToStringUTF8(targetPtr);

            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) {
                return 1;
            }

            var command = new PackCommand();
            return command.Execute(new[] { "--s", source, "--t", target });
        } catch (Exception ex) {
            // Log to stderr so the Rust process can capture it
            Console.Error.WriteLine($"error: Unexpected exception in C# bridge: {ex.Message}");
            return 1;
        }
    }
}
