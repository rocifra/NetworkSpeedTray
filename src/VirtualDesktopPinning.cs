using System;
using System.Runtime.InteropServices;

namespace NetworkSpeedTray
{
    /// <summary>
    /// Pins one window to every Windows virtual desktop. Windows does not expose
    /// this operation through its public IVirtualDesktopManager API, so this uses
    /// the same shell COM interfaces used by Task View's "Show this window on all
    /// desktops" command. Failure is deliberately non-fatal.
    /// </summary>
    internal static class VirtualDesktopPinning
    {
        private static readonly Guid ImmersiveShellClassId =
            new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239");
        private static readonly Guid PinnedAppsServiceId =
            new Guid("B5A399E7-1C87-46B8-88E9-FC5747B171BD");

        public static bool TryPinWindow(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return false;
            }

            object shellObject = null;
            object collectionObject = null;
            object pinnedAppsObject = null;
            IApplicationView view = null;

            try
            {
                Type shellType = Type.GetTypeFromCLSID(ImmersiveShellClassId, true);
                shellObject = Activator.CreateInstance(shellType);
                IServiceProvider shell = (IServiceProvider)shellObject;

                Guid collectionId = typeof(IApplicationViewCollection).GUID;
                collectionObject = shell.QueryService(ref collectionId, ref collectionId);
                IApplicationViewCollection collection =
                    (IApplicationViewCollection)collectionObject;

                Guid pinnedAppsInterfaceId = typeof(IVirtualDesktopPinnedApps).GUID;
                Guid pinnedAppsServiceId = PinnedAppsServiceId;
                pinnedAppsObject = shell.QueryService(
                    ref pinnedAppsServiceId,
                    ref pinnedAppsInterfaceId);
                IVirtualDesktopPinnedApps pinnedApps =
                    (IVirtualDesktopPinnedApps)pinnedAppsObject;

                int result = collection.GetViewForHwnd(windowHandle, out view);
                if (result != 0 || view == null)
                {
                    return false;
                }

                if (!pinnedApps.IsViewPinned(view))
                {
                    pinnedApps.PinView(view);
                }
                return true;
            }
            catch (COMException)
            {
                return false;
            }
            catch (InvalidCastException)
            {
                return false;
            }
            catch (TypeLoadException)
            {
                return false;
            }
            catch (Exception)
            {
                // Pinning is optional; never let a shell-version mismatch stop
                // the widget or the tray application.
                return false;
            }
            finally
            {
                ReleaseComObject(view);
                ReleaseComObject(pinnedAppsObject);
                ReleaseComObject(collectionObject);
                ReleaseComObject(shellObject);
            }
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.ReleaseComObject(value);
            }
        }

        [ComImport]
        [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IServiceProvider
        {
            [return: MarshalAs(UnmanagedType.IUnknown)]
            object QueryService(ref Guid serviceId, ref Guid interfaceId);
        }

        [ComImport]
        [Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IApplicationViewCollection
        {
            int GetViews(out IntPtr views);
            int GetViewsByZOrder(out IntPtr views);
            int GetViewsByAppUserModelId(
                [MarshalAs(UnmanagedType.LPWStr)] string appId,
                out IntPtr views);
            int GetViewForHwnd(IntPtr windowHandle, out IApplicationView view);
        }

        [ComImport]
        [Guid("372E1D3B-38D3-42E4-A15B-8AB2B178F513")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IApplicationView
        {
        }

        [ComImport]
        [Guid("4CE81583-1E4C-4632-A621-07A53543148F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IVirtualDesktopPinnedApps
        {
            [return: MarshalAs(UnmanagedType.Bool)]
            bool IsAppIdPinned([MarshalAs(UnmanagedType.LPWStr)] string appId);
            void PinAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);
            void UnpinAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);
            [return: MarshalAs(UnmanagedType.Bool)]
            bool IsViewPinned(IApplicationView applicationView);
            void PinView(IApplicationView applicationView);
            void UnpinView(IApplicationView applicationView);
        }
    }
}
