using System.Runtime.InteropServices;

namespace VanTools.Bridge.Native;

public static class NativeExports {
    [UnmanagedCallersOnly(EntryPoint = "van_pack_cs")]
    public static int Pack(nint sourcePtr, nint targetPtr) {
        // Dummy implementation for scaffolding
        return 0;
    }
}
