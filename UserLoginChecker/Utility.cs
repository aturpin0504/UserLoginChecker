using System;
using System.Collections.Generic;
using System.IO;

namespace UserLoginChecker
{
    public static class Utility
    {
        public static string FindQuserPath()
        {
            string[] pathsToCheck = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "quser.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "sysnative", "quser.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "quser.exe")
            };

            foreach (var path in pathsToCheck)
            {
                if (File.Exists(path)) return path;
            }

            foreach (var path in Environment.GetEnvironmentVariable("PATH").Split(';'))
            {
                var fullPath = Path.Combine(path, "quser.exe");
                if (File.Exists(fullPath)) return fullPath;
            }

            throw new FileNotFoundException("quser.exe not found. Please ensure it is available in your PATH or System32 directory.");
        }

        public static void LogError(string message)
        {
            string logFilePath = "UserLoginCheckerErrorLog.txt";
            try
            {
                using (var writer = new StreamWriter(logFilePath, true))
                {
                    writer.WriteLine($"{DateTime.Now}: {message}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to write to log file: {ex.Message}", ex);
            }
        }

        public static void LogErrors(IEnumerable<string> messages)
        {
            string logFilePath = "UserLoginCheckerErrorLog.txt";
            try
            {
                using (var writer = new StreamWriter(logFilePath, true))
                {
                    foreach (var message in messages)
                    {
                        writer.WriteLine($"{DateTime.Now}: {message}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to write to log file: {ex.Message}", ex);
            }
        }
    }
}
