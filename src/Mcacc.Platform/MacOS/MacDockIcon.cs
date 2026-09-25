using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Mcacc.Platform.MacOS;

/// <summary>
/// Switches the activation policy of <c>NSApplication</c>: Regular has a Dock icon and an app menu, Accessory has
/// neither and leaves the menu bar item alone. Avalonia sets the policy only once at startup, so it goes through the
/// Objective-C runtime here. Must run on the main thread, like everything that touches <c>NSApp</c>.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed partial class MacDockIcon : IDockIcon
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    private const nint Regular = 0;
    private const nint Accessory = 1;

    public void SetVisible(bool visible)
    {
        var app = SendIntPtr(GetClass("NSApplication"), RegisterName("sharedApplication"));
        if (app == 0)
        {
            return;
        }

        SendBool(app, RegisterName("setActivationPolicy:"), visible ? Regular : Accessory);
        if (visible)
        {
            // A Regular app that was Accessory does not come to the front by itself.
            SendVoid(app, RegisterName("activateIgnoringOtherApps:"), activate: true);
        }
    }

    [LibraryImport(ObjC, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint GetClass(string name);

    [LibraryImport(ObjC, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint RegisterName(string name);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial nint SendIntPtr(nint receiver, nint selector);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SendBool(nint receiver, nint selector, nint value);

    [LibraryImport(ObjC, EntryPoint = "objc_msgSend")]
    private static partial void SendVoid(nint receiver, nint selector, [MarshalAs(UnmanagedType.U1)] bool activate);
}
