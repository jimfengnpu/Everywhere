using System.Text.Json.Nodes;
using GnomeStack.Os.Secrets.Win32;
using ZLinq;

namespace Everywhere.Configuration.Migrations;

public class _20260106195452_0_6_0 : SettingsMigration
{
    public override Version Version => new(0, 6, 0);

    protected internal override bool Migrate(JsonObject root)
    {
        if (!OperatingSystem.IsWindows()) return false;

        var secretsToMigrate = new Dictionary<string, string>();
        foreach (var credential in WinCredManager.EnumerateCredentials())
        {
            if (credential.Service != "com.sylinko.everywhere.apikeys") continue;
            if (credential is not { Account: { } account, Password: { } password }) continue;

            secretsToMigrate[account] = credential.Password;
            WinCredManager.DeleteSecret("com.sylinko.everywhere.apikeys", account);
        }

        foreach (var (account, secret) in secretsToMigrate.AsValueEnumerable())
        {
            WinCredManager.SetSecret("com.sylinko.everywhere", account, secret);
        }

        return secretsToMigrate.Count > 0;
    }
}