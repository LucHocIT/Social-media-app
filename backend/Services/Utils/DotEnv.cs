using System;
using System.IO;

namespace SocialApp.Services.Utils
{
    public static class DotEnv
    {
        public static void Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Warning: Environment file not found: {filePath}");
                return;
            }

            foreach (var line in File.ReadAllLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//") || line.StartsWith("#"))
                    continue;

                var parts = line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                    continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                // Remove quotes if present
                if (value.StartsWith("\"") && value.EndsWith("\""))
                    value = value.Substring(1, value.Length - 2);

                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
