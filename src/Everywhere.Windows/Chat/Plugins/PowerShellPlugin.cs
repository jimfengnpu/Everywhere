using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DynamicData;
using Everywhere.Chat.Permissions;
using Everywhere.Chat.Plugins;
using Everywhere.Common;
using Everywhere.Extensions;
using Everywhere.I18N;
using Lucide.Avalonia;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using ZLinq;

namespace Everywhere.Windows.Chat.Plugins;

public partial class PowerShellPlugin : BuiltInChatPlugin
{
    public override DynamicResourceKeyBase HeaderKey { get; } = new DynamicResourceKey(LocaleKey.NativeChatPlugin_Shell_Header);

    public override DynamicResourceKeyBase DescriptionKey { get; } = new DynamicResourceKey(LocaleKey.NativeChatPlugin_Shell_Description);

    public override LucideIconKind? Icon => LucideIconKind.SquareTerminal;

    public override string BeautifulIcon => "avares://Everywhere.Windows/Assets/Icons/PowerShell.svg";

    private readonly ILogger<PowerShellPlugin> _logger;

    private string? _powerShellExcutablePath;

    public PowerShellPlugin(ILogger<PowerShellPlugin> logger) : base("powershell")
    {
        _logger = logger;

        _functionsSource.Add(
            new NativeChatFunction(
                ExecuteScriptAsync,
                ChatFunctionPermissions.ShellExecute));
    }

    [KernelFunction("execute_script")]
    [Description("Execute PowerShell script and obtain its output.")]
    [DynamicResourceKey(LocaleKey.NativeChatPlugin_PowerShell_ExecuteScript_Header)]
    private async Task<string> ExecuteScriptAsync(
        [FromKernelServices] IChatPluginUserInterface userInterface,
        [Description("A concise description for user, explaining what you are doing")] string description,
        [Description("Single or multi-line")] string script,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing PowerShell script with description: {Description}", description);

        if (string.IsNullOrWhiteSpace(script))
        {
            throw new ArgumentException("Script cannot be null or empty.", nameof(script));
        }

        string? consentKey;
        var trimmedScript = script.AsSpan().Trim();
        if (trimmedScript.Count('\n') == 0)
        {
            // single line script, confirm with user
            var command = trimmedScript[trimmedScript.Split(' ').FirstOrDefault(new Range(0, trimmedScript.Length))].ToString();
            consentKey = $"single.{command}";
        }
        else
        {
            // multi-line script, ask every time
            consentKey = null;
        }

        var detailBlock = new ChatPluginContainerDisplayBlock
        {
            new ChatPluginTextDisplayBlock(description),
            new ChatPluginCodeBlockDisplayBlock(script, "powershell"),
        };

        var consent = await userInterface.RequestConsentAsync(
            consentKey,
            new DynamicResourceKey(LocaleKey.NativeChatPlugin_PowerShell_ExecuteScript_ScriptConsent_Header),
            detailBlock,
            cancellationToken);
        if (!consent)
        {
            throw new HandledException(
                new UnauthorizedAccessException("User denied consent for PowerShell script execution."),
                new DynamicResourceKey(LocaleKey.NativeChatPlugin_PowerShell_ExecuteScript_DenyMessage),
                showDetails: false);
        }

        userInterface.DisplaySink.AppendBlocks(detailBlock.Children);

        _powerShellExcutablePath = FindPowerShellExecutable();
        if (string.IsNullOrEmpty(_powerShellExcutablePath))
        {
            throw new FileNotFoundException("Cannot find PowerShell executable on the system.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = _powerShellExcutablePath,
            Arguments = "-NoProfile -NonInteractive -NoLogo -Command -",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        using (var process = new Process())
        {
            process.StartInfo = psi;
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) outputBuilder.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) errorBuilder.AppendLine(e.Data);
            };

            try
            {
                process.Start();

                var pid = process.Id;
                await using var registration = cancellationToken.Register(() =>
                {
                    // ReSharper disable once MethodSupportsCancellation
                    Task.Run(() =>
                    {
                        try
                        {
                            Process.Start(
                                new ProcessStartInfo
                                {
                                    FileName = "taskkill",
                                    Arguments = $"/PID {pid} /T /F",
                                    RedirectStandardError = true,
                                    RedirectStandardOutput = true,
                                    UseShellExecute = false,
                                    CreateNoWindow = true,
                                });
                        }
                        catch
                        {
                            // ignore
                        }
                    });
                });

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await using (var stdin = process.StandardInput)
                {
                    await stdin.WriteLineAsync("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8");
                    await stdin.WriteLineAsync(script);
                    await stdin.WriteLineAsync("exit");
                }

                await process.WaitForExitAsync(cancellationToken);
            }
            catch (Win32Exception ex)
            {
                throw new Exception($"启动 PowerShell 失败: {ex.Message}", ex);
            }
        }

        if (errorBuilder.Length > 0)
        {
            var errorOutput = errorBuilder.ToString().Trim();
            throw new HandledException(
                new SystemException($"PowerShell script execution failed: {errorOutput}"),
                new FormattedDynamicResourceKey(
                    LocaleKey.NativeChatPlugin_PowerShell_ExecuteScript_ErrorMessage,
                    new DirectResourceKey(errorOutput)),
                showDetails: false);
        }

        var result = outputBuilder.ToString().Trim();
        userInterface.DisplaySink.AppendCodeBlock(result, "log");
        return result;
    }

    /// <summary>
    /// Search for PowerShell executable in the system.
    /// </summary>
    private static string? FindPowerShellExecutable()
    {
        // 1. Use PATH first
        var pwshInPath = FindInPath("pwsh.exe");
        if (!string.IsNullOrEmpty(pwshInPath)) return pwshInPath;

        // 2. Search in Program Files
        var bestProgramFilesVersion = FindBestVersionInProgramFiles();
        if (!string.IsNullOrEmpty(bestProgramFilesVersion)) return bestProgramFilesVersion;

        // 3. Fallback to Windows PowerShell
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var windowsPowerShell = Path.Combine(systemRoot, "WindowsPowerShell", "v1.0", "powershell.exe");

        if (File.Exists(windowsPowerShell)) return windowsPowerShell;

        return null;
    }

    /// <summary>
    /// Search for the best PowerShell version installed in Program Files directories.
    /// </summary>
    private static string FindBestVersionInProgramFiles()
    {
        var roots = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };

        var foundExes = new List<(Version Ver, string Path)>();

        foreach (var psRoot in roots
                     .AsValueEnumerable()
                     .Where(root => !string.IsNullOrEmpty(root))
                     .Select(root => Path.Combine(root, "PowerShell"))
                     .Where(Directory.Exists))
        {
            try
            {
                // Get all subdirectories (versions)
                var dirs = Directory.GetDirectories(psRoot);

                foreach (var dir in dirs)
                {
                    var folderName = Path.GetFileName(dir);
                    var exePath = Path.Combine(dir, "pwsh.exe");

                    if (!File.Exists(exePath)) continue;

                    // Try parse version
                    // Use regex to extract leading numeric part to handle "7-preview" etc.
                    // Match "7", "7.1", "7.2.3" etc.
                    var match = VersionRegex().Match(folderName);

                    if (match.Success && Version.TryParse(match.Value, out var v))
                    {
                        foundExes.Add((v, exePath));
                    }
                }
            }
            catch
            {
                // Ignore
            }
        }

        // 7.2 > 7.1 > 7.0 > 6.0
        var bestMatch = foundExes.OrderByDescending(x => x.Ver).FirstOrDefault();
        return bestMatch.Path;
    }

    /// <summary>
    /// Find a file in the system PATH environment variable.
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    private static string? FindInPath(string fileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return null;

        var paths = pathEnv.Split(Path.PathSeparator);
        foreach (var path in paths)
        {
            try
            {
                var fullPath = Path.Combine(path.Trim(), fileName);
                if (File.Exists(fullPath)) return fullPath;
            }
            catch
            {
                // 忽略无效路径
            }
        }

        return null;
    }

    [GeneratedRegex(@"^(\d+(\.\d+)*)")]
    private static partial Regex VersionRegex();
}