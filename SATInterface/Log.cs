using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Microsoft.Windows.Sdk;

namespace SATInterface
{
    static class VT100Extensions
    {
        public static string? StyleDarkRed(this string? _s) => Log.VTEnabled ? $"\u001b[31m{_s}\u001b[39m" : _s;
        public static string? StyleDarkGreen(this string? _s) => Log.VTEnabled ? $"\u001b[32m{_s}\u001b[39m" : _s;
        public static string? StyleDarkYellow(this string? _s) => Log.VTEnabled ? $"\u001b[33m{_s}\u001b[39m" : _s;
        public static string? StyleDarkBlue(this string? _s) => Log.VTEnabled ? $"\u001b[34m{_s}\u001b[39m" : _s;
        public static string? StyleDarkMagenta(this string? _s) => Log.VTEnabled ? $"\u001b[35m{_s}\u001b[39m" : _s;
        public static string? StyleDarkCyan(this string? _s) => Log.VTEnabled ? $"\u001b[36m{_s}\u001b[39m" : _s;

        public static string? StyleBrightRed(this string? _s) => Log.VTEnabled ? $"\u001b[91m{_s}\u001b[39m" : _s;
        public static string? StyleBrightGreen(this string? _s) => Log.VTEnabled ? $"\u001b[92m{_s}\u001b[39m" : _s;
        public static string? StyleBrightYellow(this string? _s) => Log.VTEnabled ? $"\u001b[93m{_s}\u001b[39m" : _s;
        public static string? StyleBrightBlue(this string? _s) => Log.VTEnabled ? $"\u001b[94m{_s}\u001b[39m" : _s;
        public static string? StyleBrightMagenta(this string? _s) => Log.VTEnabled ? $"\u001b[95m{_s}\u001b[39m" : _s;
        public static string? StyleBrightCyan(this string? _s) => Log.VTEnabled ? $"\u001b[96m{_s}\u001b[39m" : _s;
        public static string? StyleBrightWhite(this string? _s) => Log.VTEnabled ? $"\u001b[97m{_s}\u001b[39m" : _s;

        public static string? StyleUnderline(this string? _s) => Log.VTEnabled ? $"\u001b[4m{_s}\u001b[24m" : _s;
        public static string? StyleBold(this string? _s) => Log.VTEnabled ? $"\u001b[1m{_s}\u001b[22m" : _s;
    }

    class Log
    {
        public static readonly bool VTEnabled;
        public static bool VerboseEnabled = true;

        static Log()
        {
            Console.OutputEncoding = Encoding.UTF8;

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                if (!Console.IsOutputRedirected)
                {
                    var h = new SafeFileHandle(PInvoke.GetStdHandle(STD_HANDLE_TYPE.STD_OUTPUT_HANDLE).Value, false);
                    if (!h.IsInvalid && PInvoke.GetConsoleMode(h, out var mode))
                        VTEnabled = mode.HasFlag(CONSOLE_MODE.ENABLE_VIRTUAL_TERMINAL_PROCESSING)
                            || PInvoke.SetConsoleMode(h, mode | CONSOLE_MODE.ENABLE_VIRTUAL_TERMINAL_PROCESSING);
                }
            }
            else
                VTEnabled = !Console.IsOutputRedirected;
        }

        internal static void SetTitle(string _x)
        {
            Console.Title = _x;
        }

        internal static void WriteLine() => WriteLine("");
        internal static void WriteLine(object? _o) => WriteLine(_o?.ToString());
        internal static void WriteLine(string? _x)
        {
            Console.WriteLine(_x);
        }

        internal static void VerboseLine() => VerboseLine("");
        internal static void VerboseLine(object? _o) => VerboseLine($"{_o}");
        internal static void VerboseLine(string? _x)
        {
            if (VerboseEnabled)
                Console.WriteLine($"       > {_x}".StyleDarkYellow());
        }

        [Conditional("DEBUG")]
        internal static void Debug(string _x)
        {
            Console.WriteLine(_x);
        }

        internal static void LimitOutputTo(int? _lines = null)
        {
            if (!VTEnabled)
                return;

            if (_lines is null)
            {
                var cursorY = Console.CursorTop;
                Console.Write("\u001b[;r");
                Console.CursorTop = cursorY;
                return;
            }

            if (_lines < 1)
                throw new ArgumentOutOfRangeException(nameof(_lines));

            var lines = Math.Min(_lines.Value, Console.WindowHeight);
            Console.Write(string.Join("", Enumerable.Repeat(Environment.NewLine, lines)));

            {
                var cursorY = Console.CursorTop;
                var marginEnd = Math.Min(Console.WindowHeight, cursorY + 1);
                Console.Write($"\u001b[{marginEnd - lines};{marginEnd}r");
                Console.CursorTop = cursorY;
            }
        }
    }
}
