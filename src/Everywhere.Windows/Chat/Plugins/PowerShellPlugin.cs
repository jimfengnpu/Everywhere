using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using DynamicData;
using Everywhere.Chat.Permissions;
using Everywhere.Chat.Plugins;
using Everywhere.Common;
using Everywhere.Extensions;
using Everywhere.I18N;
using Lucide.Avalonia;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Everywhere.Windows.Chat.Plugins;

public class PowerShellPlugin : BuiltInChatPlugin
{
    public override DynamicResourceKeyBase HeaderKey { get; } = new DynamicResourceKey(LocaleKey.NativeChatPlugin_Shell_Header);

    public override DynamicResourceKeyBase DescriptionKey { get; } = new DynamicResourceKey(LocaleKey.NativeChatPlugin_Shell_Description);

    public override LucideIconKind? Icon => LucideIconKind.SquareTerminal;

    public override string BeautifulIcon => "avares://Everywhere.Windows/Assets/Icons/PowerShell.svg";

    private readonly ILogger<PowerShellPlugin> _logger;

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

        var path = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? ".";
        var psi = new ProcessStartInfo
        {
            FileName = Path.GetFullPath(Path.Combine(path, "Everywhere.Windows.PowerShell.exe")),
            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        string result;
        using (var process = Process.Start(psi))
        {
            if (process is null)
            {
                throw new SystemException("Failed to start PowerShell script execution process.");
            }

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

            await process.StandardInput.WriteAsync(script);
            process.StandardInput.Close();

            result = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorOutput = await process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                throw new HandledException(
                    new SystemException($"PowerShell script execution failed: {errorOutput}"),
                    new FormattedDynamicResourceKey(
                        LocaleKey.NativeChatPlugin_PowerShell_ExecuteScript_ErrorMessage,
                        new DirectResourceKey(errorOutput)),
                    showDetails: false);
            }
        }

        userInterface.DisplaySink.AppendCodeBlock(result, "log");
        return result;
    }
}