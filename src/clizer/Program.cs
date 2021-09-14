using System.Runtime.InteropServices;
using HWND = System.IntPtr;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Text;
using System.Threading.Tasks;
using System.CommandLine.Invocation;
using System.CommandLine.IO;


namespace clizer
{
       class Program
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow (IntPtr hWnd);
        
        static async Task Main(string[] args)
        {
            var rootCommand = new RootCommand()
            {
                new Argument<string>("name", "App name."),
                new Option<int?>(new[] { "--size", "-s" }, "The size of the window"),
                new Option(new[] { "--save", "-sv" }, "Save as default.")
            };
            
            rootCommand.Description = "My sample app";

            // Note that the parameters of the handler method are matched according to the names of the options
            rootCommand.Handler = CommandHandler.Create<string, int?, bool>((name, size, saveAsDefault) =>
            {
                Console.WriteLine($"App name: {name}");
                Console.WriteLine($"Target size: {size}");
                Console.WriteLine($"Save as default: {saveAsDefault}");
                
                foreach(KeyValuePair<IntPtr, string> window in OpenWindowGetter.GetOpenWindows())
                {
                    IntPtr handle = window.Key;
                    string title = window.Value;

                    if (title.Contains(name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        MoveWindow(handle, 0, 0, 600, 600, true);
                        SetForegroundWindow(handle);
                    }
                    
                    Console.WriteLine("{0}: {1}", handle, title);
                }
                
            });

            await rootCommand.InvokeAsync(args);
        }
    }

    /// <summary>Contains functionality to get all the open windows.</summary>
    public static class OpenWindowGetter
    {
        /// <summary>Returns a dictionary that contains the handle and title of all the open windows.</summary>
        /// <returns>A dictionary that contains the handle and title of all the open windows.</returns>
        public static IDictionary<HWND, string> GetOpenWindows()
        {
            HWND shellWindow = GetShellWindow();
            Dictionary<HWND, string> windows = new Dictionary<HWND, string>();

            EnumWindows(delegate(HWND hWnd, int lParam)
            {
                if (hWnd == shellWindow) return true;
                if (!IsWindowVisible(hWnd)) return true;

                int length = GetWindowTextLength(hWnd);
                if (length == 0) return true;

                StringBuilder builder = new StringBuilder(length);
                GetWindowText(hWnd, builder, length + 1);

                windows[hWnd] = builder.ToString();
                return true;

            }, 0);

            return windows;
        }

        private delegate bool EnumWindowsProc(HWND hWnd, int lParam);

        [DllImport("USER32.DLL")]
        private static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

        [DllImport("USER32.DLL")]
        private static extern int GetWindowText(HWND hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("USER32.DLL")]
        private static extern int GetWindowTextLength(HWND hWnd);

        [DllImport("USER32.DLL")]
        private static extern bool IsWindowVisible(HWND hWnd);

        [DllImport("USER32.DLL")]
        private static extern IntPtr GetShellWindow();
        
        
    }
}
