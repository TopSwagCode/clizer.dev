using System.Runtime.InteropServices;
using HWND = System.IntPtr;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Text;
using System.Threading.Tasks;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.Drawing;
using System.Text.Json;
using System.IO;

namespace clizer
{
    public class WindowLocation
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    class Program
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow (IntPtr hWnd);

        [DllImport("user32.dll")]

        static extern int GetWindowRect(IntPtr hwnd, out Rectangle rect);

        private static string storagePath = "./storage.json";

        static async Task Main(string[] args)
        {
            var rootCommand = new RootCommand()
            {
                new Argument<string?>("name", "App name."),
                new Option<string?>(new[] { "--size", "-s" }, "The size of the window. Eg. 800*600 "),
                new Option<string?>(new[] { "--location", "-l" }, "The location of the window. Eg. 100,150"),
                new Option(new[] { "--save" }, "Save as default."),
                new Option(new[] { "--load" }, "Load as default."),
                new Option(new[] { "--list-windows" }, "List all found windows."),
                new Option(new[] { "--list-defaults" }, "List defaults.")
            };
            
            rootCommand.Description = "My sample app";

            // Note that the parameters of the handler method are matched according to the names of the options
            rootCommand.Handler = CommandHandler.Create<string, string?, string?, bool, bool, bool>((name, size, location, save, load, listwindows) =>
            {
                var (locationIsSet,x,y,sizeIsSet,width,height) = ValidateAndParseInput(size, location, save, load);

                var windowsLocations = new Dictionary<string, WindowLocation>();

                if (load)
                {
                    windowsLocations = LoadFile();
                }
                else if (locationIsSet && sizeIsSet && !string.IsNullOrEmpty(name))
                {
                    windowsLocations = LoadFile();
                    windowsLocations[name] = new WindowLocation
                    {
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height
                    };
                }

                foreach (KeyValuePair<IntPtr, string> window in OpenWindowGetter.GetOpenWindows())
                {
                    IntPtr handle = window.Key;
                    string title = window.Value;

                    foreach(var key in windowsLocations.Keys)
                    {
                        if (title.Contains(key, StringComparison.InvariantCultureIgnoreCase))
                        {
                            var windowLocation = windowsLocations[key];
                            MoveWindow(handle, windowLocation.X, windowLocation.Y, windowLocation.Width, windowLocation.Height, true);
                            SetForegroundWindow(handle);
                        }
                    }
                    
                    Rectangle rect;
                    GetWindowRect(handle, out rect);

                    if (listwindows)
                    {
                        Console.WriteLine("{0}: {1} - Location: {2},{3} - Size {4}*{5}", handle, title, rect.X, rect.Y, rect.Width - rect.X, rect.Height - rect.Y);
                    }
                }

                if (save)
                {
                    // TODO
                    // Overwrite all if more than one rule
                    // or
                    // Overwrite single field if only one rule

                    var json = JsonSerializer.Serialize(windowsLocations);
                    File.WriteAllText(storagePath, json);
                }
            });

            await rootCommand.InvokeAsync(args);
        }

        private static Dictionary<string, WindowLocation> LoadFile()
        {
            if (!File.Exists(storagePath))
            {
                return new Dictionary<string, WindowLocation>();
            }
            else
            {
                var json = File.ReadAllText(storagePath);
                return JsonSerializer.Deserialize<Dictionary<string, WindowLocation>>(json);
            }
        }

        private static (bool locationIsSet, int x, int y, bool sizeIsSet, int width, int height) ValidateAndParseInput(string size, string location, bool save, bool load)
        {
            Console.WriteLine($"Target size: {size}");
            Console.WriteLine($"Target location: {location}");
            Console.WriteLine($"Save as default: {save}");
            Console.WriteLine($"Load as default: {load}");

            var locationIsSet = !string.IsNullOrEmpty(location);
            var sizeIsSet = !string.IsNullOrEmpty(size);

            if (true)
            {
                // Nothing. Disable validation for now!
            }
            else if (locationIsSet && !sizeIsSet || sizeIsSet && !locationIsSet)
            {
                Console.Error.WriteLine($"{nameof(location)} and {nameof(size)} both need to be set");
            }
            else if (load && !(locationIsSet || sizeIsSet))
            {
                Console.Error.WriteLine($"{nameof(location)} and {nameof(size)} both are not allowed with {nameof(load)}");
            }
            else if (save && load)
            {
                Console.Error.WriteLine($"{nameof(load)} and {nameof(save)} cannot be run at the same time");
            }
            // TODO validate x,y and width*height
            // -s 1440*3440 -l 3840,-666 --save --list-windows
            var x = locationIsSet ? int.Parse(location.Split(",")[0]) : 0;
            var y = locationIsSet ? int.Parse(location.Split(",")[1]) : 0;
            
            var width = sizeIsSet ? int.Parse(size.Split("*")[0]) : 0;
            var height = sizeIsSet ? int.Parse(size.Split("*")[1]) : 0;

            return (locationIsSet, x, y, sizeIsSet, width, height);
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
