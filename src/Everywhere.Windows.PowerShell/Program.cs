using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using Microsoft.PowerShell;

var path = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
var modulesPath = Path.Combine(path ?? ".", "runtimes", "win", "lib", "net9.0", "Modules");
Environment.SetEnvironmentVariable(
    "PSModulePath",
    $"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\Modules")};" +
    $"{modulesPath};" + // Import application auto-contained modules
    Environment.GetEnvironmentVariable("PSModulePath"));

var iss = InitialSessionState.CreateDefault2();
iss.ExecutionPolicy = ExecutionPolicy.Bypass;

// Create a new runspace and PowerShell instance for each execution
// using the cached InitialSessionState. This ensures context isolation.
using var runspace = RunspaceFactory.CreateRunspace(iss);
// Disable ReSharper warning as OpenAsync() cannot be awaited
// ReSharper disable once MethodHasAsyncOverload
runspace.Open();

var scriptBuilder = new System.Text.StringBuilder();
while (Console.ReadLine() is { } line)
{
    scriptBuilder.AppendLine(line);
}

using var powerShell = PowerShell.Create(runspace);
powerShell.AddScript($"& {{ {scriptBuilder} }} | Out-String"); // Ensure results are returned as string

var results = await powerShell.InvokeAsync();
if (powerShell.HadErrors)
{
    var errorMessage = string.Join(Environment.NewLine, powerShell.Streams.Error.Select(e => e.ToString()));
    Console.Error.Write(errorMessage);
    Environment.Exit(1);
}

var result = results.FirstOrDefault()?.ToString() ?? string.Empty;
if (result.EndsWith(Environment.NewLine)) result = result[..^Environment.NewLine.Length]; // Trim trailing new line
Console.Write(result);