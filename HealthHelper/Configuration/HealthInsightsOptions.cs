using System;
using System.IO;

namespace HealthHelper.Configuration;

public sealed class HealthInsightsOptions
{
    private const string DefaultFileName = "healthhelper.db";

    public string DatabasePath { get; set; } = GetDefaultPath();

    private static string GetDefaultPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directory = Path.Combine(appData, "HealthHelper");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, DefaultFileName);
    }
}


