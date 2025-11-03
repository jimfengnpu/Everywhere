using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Everywhere.Extensions;
using ObjCRuntime;

namespace Everywhere.Mac.Interop;

public static class InteropHelper
{
    // ReSharper disable once InconsistentNaming
    [field: AllowNull, MaybeNull]
    private static ConstructorInfo CGEventConstructorInfo =>
        field ??= typeof(CGEvent).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, [typeof(NativeHandle), typeof(bool)]).NotNull();

    /// <summary>
    /// `CGEvent(NativeHandle)` is mistakenly not compiled with `!NET` directive, making it inaccessible in .NET 5+ builds.
    /// We use reflection to access the other internal constructor `CGEvent(NativeHandle, bool)` instead.
    /// </summary>
    /// <param name="cgEventRef"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static CGEvent CGEventFromHandle(nint cgEventRef)
    {
        return CGEventConstructorInfo.Invoke([new NativeHandle(cgEventRef), false]) as CGEvent
               ?? throw new InvalidOperationException("Failed to create CGEvent from handle.");
    }
}