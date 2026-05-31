using System.Runtime.InteropServices;

namespace UnturnedRedistUpdateTool;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            AssertPlatformSupported();

#if DEBUG
            if (args.Length == 0)
            {
                // Local debug-run convenience: point UNTURNED_PATH at your Unturned
                // install; the redist target is the committed TempRedist/Client sample.
                var debugUnturnedPath = Environment.GetEnvironmentVariable("UNTURNED_PATH")
                    ?? throw new InvalidOperationException(
                        "Set the UNTURNED_PATH environment variable to your local Unturned install to debug-run without args.");
                args =
                [
                    debugUnturnedPath,
                    Path.Combine(AppContext.BaseDirectory, "TempRedist", "Client"),
                    "304930",
                    "--force",
                    "-publicize", "Assembly-CSharp.dll",
                    "-update-files", "Assembly-CSharp.dll,UnturnedDat.dll,UnityEx.dll,SystemEx.dll,SDG.NetTransport.dll,SDG.NetPak.Runtime.dll,SDG.HostBans.Runtime.dll,SDG.Glazier.Runtime.dll,com.rlabrecque.steamworks.net.dll"
                ];
            }
#endif

            if (args.Length < 3)
            {
                Console.WriteLine("Wrong usage. Correct usage: UnturnedRedistUpdateTool.exe <unturned_path> <redist_path> <app_id> [args]");
                return 1;
            }
            var unturnedPath = args[0];
            var redistPath = args[1];
            var appId = args[2];
            var force = args.Any(x => x.Equals("--force", StringComparison.OrdinalIgnoreCase));
            var preview = args.Any(x => x.Equals("--preview", StringComparison.OrdinalIgnoreCase));
            var publicizeAssemblies = ParseArrayArg(args, "-publicize");
            var updateFiles = ParseArrayArg(args, "-update-files");
            if (updateFiles.Count == 0)
            {
                Console.WriteLine("-update-files is not specified");
                return 1;
            }
            if (string.IsNullOrWhiteSpace(appId))
            {
                Console.WriteLine("AppId is not specified.");
                return 1;
            }

            return await new UpdateRunner().RunAsync(
                unturnedPath, redistPath, appId, force, preview, publicizeAssemblies, updateFiles);
        }
        catch (Exception ex)
        {
            // Clean, single-line error for CI logs instead of a raw stack trace.
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static void AssertPlatformSupported()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException();
        }
    }

    private static List<string> ParseArrayArg(string[] args, string argName)
    {
        var index = Array.FindIndex(args, x => x.Equals(argName, StringComparison.OrdinalIgnoreCase));
        if (index != -1 && index + 1 < args.Length)
        {
            var argValue = args[index + 1];
            if (!argValue.StartsWith('-'))
            {
                return argValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
            }
        }
        return [];
    }
}
