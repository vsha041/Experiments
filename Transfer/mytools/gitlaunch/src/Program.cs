// gitlaunch: runs a Git for Windows (MSYS2) tool with every argument re-quoted.
//
// Why: Git's tools parse their own command line with MSYS rules, which don't match the
// Windows rules PowerShell uses when it builds one:
//   - PowerShell only quotes an argument that contains a space, so MSYS expands unquoted
//     * ? [ {a,b} ~ against the file system and treats ' as a quote (don't -> dont);
//   - with MSYS=noglob, MSYS no longer honours \" so "a \"b\" c" arrives as a \b\ c.
// .NET reads this exe's own arguments with the Windows rules, so they arrive exactly as
// typed. Each one is then passed on wrapped in double quotes with \" escapes, which MSYS
// (in its default mode) reads back unchanged.
//
// The tool to run comes from this exe's file name: GitTools\bin\grep.exe is a hard link
// to (or copy of) gitlaunch.exe and runs <Git>\usr\bin\grep.exe. stdin, stdout and stderr are passed
// straight to the tool, so pipes stay byte-for-byte, and its exit code is returned.

using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

namespace GitLaunch;

enum PathMode { None, First, Last }

static class Program
{
    // Tool name -> (path under the Git root, PATH change for the tool, strip CR from stdin).
    // Any other name runs usr\bin\<name>.exe unchanged.
    static readonly Dictionary<string, (string Target, PathMode Path, bool StripCr)> Special =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Git's launchers, which set up the MSYS environment (PATH with /usr/bin etc.).
            ["sh"]       = (@"bin\sh.exe",   PathMode.None, false),
            ["gbash"]    = (@"bin\bash.exe", PathMode.None, false),   // plain `bash` starts WSL

            // GNU versions of names that Windows / PowerShell already use.
            ["gsort"]    = (@"usr\bin\sort.exe", PathMode.None, false), // sort -> Sort-Object
            ["gdiff"]    = (@"usr\bin\diff.exe", PathMode.None, false), // diff -> Compare-Object

            // Tools that start other programs by name. Child processes can't see PowerShell
            // aliases, so usr\bin goes on the tool's PATH:
            //   First: `xargs sort` / `gfind -exec sort '{}' ';'` mean GNU sort;
            //   Last:  a wrapped program (dotnet, npm, ...) and its own scripts still get
            //          Windows find / sort / timeout.
            // PowerShell ends every line it pipes into a native exe with CRLF, which would
            // make xargs see "file.txt\r", so xargs gets CRLF turned into LF.
            ["xargs"]    = (@"usr\bin\xargs.exe",   PathMode.First, true),
            ["gfind"]    = (@"usr\bin\find.exe",    PathMode.First, false), // find -> System32\find.exe
            ["env"]      = (@"usr\bin\env.exe",     PathMode.Last,  false),
            ["gtimeout"] = (@"usr\bin\timeout.exe", PathMode.Last,  false), // timeout -> System32\timeout.exe
        };

    static int Main(string[] args)
    {
        string name = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "gitlaunch");
        if (name.Equals("gitlaunch", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("gitlaunch: run it through a hard link named after the tool, e.g. bin\\grep.exe.");
            return 2;
        }

        string gitRoot = FindGitRoot();
        var (relative, pathMode, stripCr) = Special.TryGetValue(name, out var special)
            ? special
            : ($@"usr\bin\{name}.exe", PathMode.None, false);
        string target = Path.Combine(gitRoot, relative);
        if (!File.Exists(target))
        {
            Console.Error.WriteLine($"gitlaunch: {target} not found (Git root: {gitRoot}; set GITTOOLS_GIT_ROOT to override).");
            return 127;
        }

        var psi = new ProcessStartInfo(target, string.Join(' ', args.Select(Quote)))
        {
            UseShellExecute = false,
        };

        // The quoting above relies on MSYS's default parsing; noglob turns off its \" handling.
        string? msys = Environment.GetEnvironmentVariable("MSYS");
        if (msys is not null)
        {
            string kept = string.Join(' ', msys.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(o => !o.Equals("noglob", StringComparison.OrdinalIgnoreCase)));
            if (kept.Length == 0) psi.Environment.Remove("MSYS");
            else psi.Environment["MSYS"] = kept;
        }

        if (pathMode != PathMode.None)
        {
            string usrBin = Path.Combine(gitRoot, @"usr\bin");
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            psi.Environment["PATH"] = pathMode == PathMode.First ? $"{usrBin};{path}" : $"{path};{usrBin}";
        }

        bool pumpStdin = stripCr && Console.IsInputRedirected;
        psi.RedirectStandardInput = pumpStdin;

        // Ctrl+C reaches the tool as well; let it decide, then report its exit code.
        Console.CancelKeyPress += (_, e) => e.Cancel = true;

        using var process = Process.Start(psi)!;
        if (pumpStdin)
        {
            // Background thread: if the tool exits before reading everything, Main returns
            // and the process ends without waiting for the rest of the input.
            _ = Task.Run(() => CopyStdinWithoutCr(process.StandardInput.BaseStream));
        }
        process.WaitForExit();
        return process.ExitCode;
    }

    // Wraps an argument in double quotes using the Windows (MS C runtime) escaping rules,
    // which MSYS also understands: \ before " is doubled and escaped, a trailing \ is doubled.
    static string Quote(string arg)
    {
        var sb = new StringBuilder(arg.Length + 2).Append('"');
        int backslashes = 0;
        foreach (char c in arg)
        {
            if (c == '\\') { backslashes++; continue; }
            if (c == '"') sb.Append('\\', backslashes * 2 + 1).Append('"');
            else sb.Append('\\', backslashes).Append(c);
            backslashes = 0;
        }
        return sb.Append('\\', backslashes * 2).Append('"').ToString();
    }

    static void CopyStdinWithoutCr(Stream output)
    {
        try
        {
            using Stream input = Console.OpenStandardInput();
            using (output)
            {
                var buffer = new byte[64 * 1024];
                var converted = new byte[buffer.Length + 1];
                bool pendingCr = false;
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    int count = 0;
                    for (int i = 0; i < read; i++)
                    {
                        byte b = buffer[i];
                        if (pendingCr)
                        {
                            if (b != (byte)'\n') converted[count++] = (byte)'\r'; // lone CR: keep it
                            pendingCr = false;
                        }
                        if (b == (byte)'\r') { pendingCr = true; continue; }
                        converted[count++] = b;
                    }
                    output.Write(converted, 0, count);
                    output.Flush();
                }
                if (pendingCr) output.WriteByte((byte)'\r');
            }
        }
        catch (IOException)
        {
            // The tool exited before reading all of its input.
        }
    }

    // GITTOOLS_GIT_ROOT, else the path Git for Windows' installer records, else the default.
    static string FindGitRoot()
    {
        string? root = Environment.GetEnvironmentVariable("GITTOOLS_GIT_ROOT");
        if (!string.IsNullOrWhiteSpace(root)) return root;

        foreach (RegistryKey hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using RegistryKey? key = hive.OpenSubKey(@"SOFTWARE\GitForWindows");
            if (key?.GetValue("InstallPath") is string installPath && Directory.Exists(installPath))
                return installPath;
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git");
    }
}
