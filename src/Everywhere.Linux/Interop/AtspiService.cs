using System.Runtime.InteropServices;
namespace Everywhere.Linux.Interop;

/// <summary>
/// AT-SPI（Accessibility Toolkit）
/// </summary>
public partial class AtspiService
{
    private bool initialized = false;
    
    public AtspiService()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AT_SPI_BUS")))
            Environment.SetEnvironmentVariable("AT_SPI_BUS", "session");
        Environment.SetEnvironmentVariable("NO_AT_BRIDGE", "1");
        if (atspi_init() < 0)
            throw new InvalidOperationException("Failed to initialize AT-SPI");
    }
    public void Dispose()
    {
        atspi_exit();
    }

    public static IntPtr FindAccessibleApplication(int pid)
    {
        if (pid <= 0) return IntPtr.Zero;
        var desktop = atspi_get_desktop(0);
        if (desktop == IntPtr.Zero) return IntPtr.Zero;
        var count = atspi_accessible_get_child_count(desktop, IntPtr.Zero);
        for (int i = 0; i < count; i++)
        {
            var child = atspi_accessible_get_child_at_index(desktop, i, IntPtr.Zero);
            if (child == IntPtr.Zero) continue;
            if (atspi_accessible_is_application(child) != 0 && atspi_accessible_get_process_id(child, IntPtr.Zero) == pid)
            {
                return child;
            }
            g_object_unref(child);
        }
        return IntPtr.Zero;
    }
    private const string LibAtspi = "libatspi.so.0";
    public const int AtspiCoordTypeScreen = 0;
    public const int AtspiCoordTypeWindow = 1;
    public const int AtspiCoordTypeParent = 2;
    [StructLayout(LayoutKind.Sequential)]
    public struct AtspiRect
    {
        public int x;
        public int y;
        public int width;
        public int height;
    }

    [LibraryImport(LibAtspi)]
    public static partial int atspi_init();

    [LibraryImport(LibAtspi)]
    public static partial void atspi_exit();

    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_get_desktop(int i);


    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_accessible_get_name(IntPtr accessible, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_accessible_get_role_name(IntPtr accessible, IntPtr error);
    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_accessible_get_parent(IntPtr accessible, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_get_child_count(IntPtr accessible, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_accessible_get_child_at_index(IntPtr accessible, int index, IntPtr error);
    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_get_index_in_parent(IntPtr accessible, IntPtr error);
    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_get_process_id(IntPtr accessible, IntPtr error);
    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_get_id(IntPtr accessible, IntPtr error);
    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_accessible_get_accessible_id(IntPtr accessible, IntPtr error);

    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_is_application(IntPtr accessible);
    [LibraryImport(LibAtspi)]
    public static partial int atspi_accessible_is_component(IntPtr accessible);
    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_component_get_accessible_at_point(IntPtr component, int x, int y, int coordType, IntPtr error);
    [LibraryImport(LibAtspi)]
    public static partial IntPtr atspi_component_get_extents(IntPtr component, int coordType, IntPtr error);

    [LibraryImport("libgobject-2.0.so.0")]
    public static partial void g_object_unref(IntPtr obj);

    [LibraryImport("libglib-2.0.so.0")]
    public static partial void g_free(IntPtr mem);
}