using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace WPFSingleBorderWindowNoChrome
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Hook the WndProc for the main window so that we can intercept window messages
            WindowInteropHelper windowHelper = new WindowInteropHelper(this);
            HwndSource source = HwndSource.FromHwnd(windowHelper.Handle);
            source.AddHook(HwndSourceHook);
        }

        private IntPtr HwndSourceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case NativeMethods.WM_NCCALCSIZE:
                    // Do some size calculations to hide the default window chrome non-client area
                    return OnNCCalcSize(hwnd, wParam, lParam, ref handled);

                case NativeMethods.WM_NCUAHDRAWCAPTION:
                case NativeMethods.WM_NCUAHDRAWFRAME:
                    // Passing these internally-defined messages to DefWindowProc will result in
                    // drawing the caption or frame, so mark this as handled so we don't call
                    // DefWindowProc
                    handled = true;
                    break;
            }

            return IntPtr.Zero;
        }

        private IntPtr OnNCCalcSize(IntPtr hwnd, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            NativeMethods.WINDOWPLACEMENT placement = NativeMethods.GetWindowPlacement(hwnd);

            // Need to handle the maxmized case to remove the caption area or else when the window
            // is maximized the default chrome will come back
            if (placement.showCmd == NativeMethods.SW_SHOWMAXIMIZED)
            {
                NativeMethods.DefWindowProc(hwnd, NativeMethods.WM_NCCALCSIZE, wParam, lParam);

                NativeMethods.RECT screenRect = (NativeMethods.RECT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.RECT));

                // Here is where we remove the caption height to hide the default window chrome
                //
                // SystemParameters.CaptionHeight is in logical units and needs to be converted to
                // device units in order to work for all cases. As written, this only works for
                // displays that are set at 100% scale factor.
                screenRect.top -= (int)Math.Ceiling(SystemParameters.CaptionHeight + 1);

                // TODO: Should handle when the task bar is auto-hidden, but that's left as an
                // exercise to the reader.

                Marshal.StructureToPtr(screenRect, lParam, fDeleteOld: true);
            }

            // In the non-maximized case, keep the default size, which will fill the window's
            // entire rect with client area, allowing us to draw custom chrome. In both cases,
            // don't call DefWindowProc, which would overwrite these values.
            handled = true;
            return IntPtr.Zero;
        }

        #region WPF event callbacks, not needed for windowing behavior

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                e.Handled = true;

                DragMove();
            }
        }

        private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            switch (WindowState)
            {
                case WindowState.Normal:
                    WindowState = WindowState.Maximized;
                    break;

                case WindowState.Maximized:
                    WindowState = WindowState.Normal;
                    break;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            Close();
        }

        #endregion

        #region Native methods to pinvoke for supporting the windowing behavior

        private static class NativeMethods
        {
            public const int SW_SHOWMAXIMIZED = 0x0003;

            public const int WM_NCCALCSIZE       = 0x0083;
            public const int WM_NCUAHDRAWCAPTION = 0x00AE; // Internally-defined Windows message posted by Windows
            public const int WM_NCUAHDRAWFRAME   = 0x00AF; // Internally-defined Windows message posted by Windows

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr DefWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern bool GetWindowPlacement(IntPtr hwnd, WINDOWPLACEMENT lpwndpl);

            public static WINDOWPLACEMENT GetWindowPlacement(IntPtr hwnd)
            {
                WINDOWPLACEMENT placement = new WINDOWPLACEMENT();

                if (GetWindowPlacement(hwnd, placement))
                    return placement;

                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT
            {
                public int x;
                public int y;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int left;
                public int top;
                public int right;
                public int bottom;
            }

            [StructLayout(LayoutKind.Sequential)]
            public class WINDOWPLACEMENT
            {
                public int   length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
                public int   flags;
                public int   showCmd;
                public POINT ptMinPosition;
                public POINT ptMaxPosition;
                public RECT  rcNormalPosition;
            }
        }

        #endregion
    }
}
