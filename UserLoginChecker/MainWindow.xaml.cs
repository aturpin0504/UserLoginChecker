using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UserLoginChecker
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _cancellationTokenSource;
        private readonly int _timeoutMinutes;
        private readonly string _logFilePath;

        public MainWindow()
        {
            InitializeComponent();
            _timeoutMinutes = GetTimeoutFromConfig();
            _logFilePath = GetLogFilePath();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Disable UI elements until initialization is complete
            ToggleUIElements(false);

            try
            {
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                LogError($"Error during initialization: {ex.Message}");
                MessageBox.Show("Initialization failed. Please restart the application.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
            finally
            {
                ToggleUIElements(true); // Re-enable UI elements after initialization
            }
        }

        private int GetTimeoutFromConfig()
        {
            if (int.TryParse(ConfigurationManager.AppSettings["TaskTimeoutMinutes"], out int timeout))
            {
                return timeout;
            }
            return 60; // Default to 60 minutes if the config value is not valid
        }

        private string GetLogFilePath()
        {
            string username = Environment.UserName;
            string date = DateTime.Now.ToString("yyyyMMdd");
            string logFileName = $"UserLoginChecker_{username}_{date}.log";

            string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UserLoginCheckerLogs");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            return Path.Combine(logDirectory, logFileName);
        }

        private async Task InitializeAsync()
        {
            await LoadDhcpScopesAsync();
            // Add other asynchronous initialization tasks here
        }

        private async Task LoadDhcpScopesAsync()
        {
            string dhcpServerName = ConfigurationManager.AppSettings["DhcpServerName"];
            List<string> dhcpScopes = await GetDhcpScopesAsync(dhcpServerName).ConfigureAwait(false);

            Dispatcher.Invoke(() =>
            {
                DhcpScopeComboBox.Items.Clear();
                DhcpScopeComboBox.Items.Add("All Computers");

                foreach (var scope in dhcpScopes)
                {
                    DhcpScopeComboBox.Items.Add(scope);
                }

                DhcpScopeComboBox.SelectedIndex = 0; // Default to "All Computers"
            });
        }


        private Task<List<string>> GetDhcpScopesAsync(string dhcpServerName)
        {
            return Task.Run(() =>
            {
                List<string> scopes = new List<string>();

                try
                {
                    using (var runspace = RunspaceFactory.CreateRunspace())
                    {
                        runspace.Open();
                        using (var powershell = PowerShell.Create())
                        {
                            powershell.Runspace = runspace;
                            powershell.AddScript($@"
                                Get-DhcpServerv4Scope -ComputerName {dhcpServerName} |
                                Where-Object {{ $_.State -eq 'Active' }} |
                                Select-Object -ExpandProperty Name");

                            foreach (PSObject result in powershell.Invoke())
                            {
                                scopes.Add(result.ToString());
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Failed to load DHCP scopes: {ex.Message}");
                }

                return scopes;
            });
        }

        private async Task<List<string>> GetComputersAsync(string selectedScope, CancellationToken cancellationToken, TimeSpan timeout)
        {
            const int maxRetryAttempts = 3;
            const int delayBetweenRetriesMs = 2000;
            int attempt = 0;

            List<string> computers = new List<string>();

            while (attempt < maxRetryAttempts)
            {
                attempt++;
                try
                {
                    if (selectedScope == "All Computers")
                    {
                        using (var context = new PrincipalContext(ContextType.Domain))
                        using (var searcher = new PrincipalSearcher(new ComputerPrincipal(context)))
                        {
                            foreach (var result in searcher.FindAll())
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                DirectoryEntry de = result.GetUnderlyingObject() as DirectoryEntry;
                                string computerName = de.Properties["name"].Value.ToString();
                                computers.Add(computerName);
                            }
                        }
                    }
                    else
                    {
                        string dhcpServerName = ConfigurationManager.AppSettings["DhcpServerName"];

                        using (var runspace = RunspaceFactory.CreateRunspace())
                        {
                            runspace.Open();
                            using (var powershell = PowerShell.Create())
                            {
                                powershell.Runspace = runspace;

                                // Identify the ScopeId for the selected scope
                                powershell.AddScript($@"
                                    Get-DhcpServerv4Scope -ComputerName {dhcpServerName} |
                                    Where-Object {{ $_.Name -eq '{selectedScope}' }} |
                                    Select-Object -ExpandProperty ScopeId");

                                string scopeId = powershell.Invoke().FirstOrDefault()?.ToString();

                                if (!string.IsNullOrEmpty(scopeId))
                                {
                                    // Get the list of computer names from the selected scope
                                    powershell.Commands.Clear();
                                    powershell.AddScript($@"
                                        Get-DhcpServerv4Lease -ScopeId {scopeId} -ComputerName {dhcpServerName} |
                                        Where-Object {{ $_.HostName -like 'WKS*' -or $_.HostName -like 'VDI*' }} |
                                        Select-Object -ExpandProperty HostName");

                                    foreach (PSObject result in powershell.Invoke())
                                    {
                                        cancellationToken.ThrowIfCancellationRequested();
                                        computers.Add(result.ToString());
                                    }
                                }
                            }
                        }
                    }

                    break; // Exit retry loop if successful
                }
                catch (UnauthorizedAccessException ex)
                {
                    LogError($"Unauthorized access while retrieving computers: {ex.Message}");
                    break; // Do not retry on UnauthorizedAccessException
                }
                catch (IOException ex)
                {
                    LogError($"IO error while retrieving computers: {ex.Message}");
                    if (attempt >= maxRetryAttempts)
                    {
                        throw; // Rethrow if all retry attempts fail
                    }
                    await Task.Delay(delayBetweenRetriesMs, cancellationToken);
                }
                catch (Exception ex)
                {
                    LogError($"An unexpected error occurred while retrieving computers: {ex.Message}");
                    if (attempt >= maxRetryAttempts)
                    {
                        throw; // Rethrow if all retry attempts fail
                    }
                    await Task.Delay(delayBetweenRetriesMs, cancellationToken);
                }
            }

            return computers;
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellationTokenSource != null)
            {
                // Cancel the ongoing operation
                _cancellationTokenSource.Cancel();
                return;
            }

            string selectedScope = Dispatcher.Invoke(() => DhcpScopeComboBox.SelectedItem?.ToString());
            string username = Dispatcher.Invoke(() => UsernameTextBox.Text);

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(selectedScope))
            {
                MessageBox.Show("Please enter a username and select a DHCP scope.");
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            TimeSpan timeout = TimeSpan.FromMinutes(_timeoutMinutes);

            // Switch button to "Cancel" mode
            SearchButton.Content = "Cancel";
            CheckComputerButton.IsEnabled = false; // Disable CheckComputerButton while searching

            try
            {
                List<string> computers = await GetComputersAsync(selectedScope, _cancellationTokenSource.Token, timeout).ConfigureAwait(false);
                int totalComputers = computers.Count;
                int processedComputers = 0;

                Dispatcher.Invoke(() =>
                {
                    ProgressTextBlock.Visibility = Visibility.Visible;
                    SearchProgressBar.Visibility = Visibility.Visible;
                    SearchProgressBar.Maximum = totalComputers;
                    SearchProgressBar.Value = 0;
                });

                var tasks = computers.Select(async computer =>
                {
                    bool found = await SearchForUserOnComputerAsync(username, computer, _cancellationTokenSource.Token, timeout).ConfigureAwait(false);

                    Interlocked.Increment(ref processedComputers);

                    if (found)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            UsersDataGrid.Items.Add(new SessionInfo
                            {
                                ComputerName = computer,
                                Username = username
                            });
                        });
                    }

                    Dispatcher.Invoke(() =>
                    {
                        SearchProgressBar.Value = processedComputers;
                        ProgressTextBlock.Text = $"Processing {computer} ({processedComputers} of {totalComputers})";
                    });
                });

                await Task.WhenAll(tasks).ConfigureAwait(false);

                Dispatcher.Invoke(() => MessageBox.Show("Search completed."));
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() => MessageBox.Show("Search cancelled."));
            }
            catch (Exception ex)
            {
                LogError($"Error during search: {ex.Message}");
                Dispatcher.Invoke(() => MessageBox.Show("An error occurred during the search."));
            }
            finally
            {
                _cancellationTokenSource = null;
                Dispatcher.Invoke(() =>
                {
                    SearchButton.Content = "Search Computers"; // Switch button back to "Search" mode
                    CheckComputerButton.IsEnabled = true; // Re-enable CheckComputerButton
                    ProgressTextBlock.Visibility = Visibility.Collapsed;
                    SearchProgressBar.Visibility = Visibility.Collapsed;
                });
            }
        }

        private async void CheckComputerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellationTokenSource != null)
            {
                // Cancel the ongoing operation
                _cancellationTokenSource.Cancel();
                return;
            }

            string computerName = Dispatcher.Invoke(() => ComputerNameTextBox.Text);

            if (string.IsNullOrEmpty(computerName))
            {
                MessageBox.Show("Please enter a computer name.");
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            TimeSpan timeout = TimeSpan.FromMinutes(_timeoutMinutes);

            // Switch button to "Cancel" mode
            CheckComputerButton.Content = "Cancel";
            SearchButton.IsEnabled = false; // Disable SearchButton while checking

            try
            {
                bool found = await SearchForUserOnComputerAsync("", computerName, _cancellationTokenSource.Token, timeout).ConfigureAwait(false);

                Dispatcher.Invoke(() =>
                {
                    if (found)
                    {
                        ComputerCheckResultTextBlock.Text = $"Users are logged into {computerName}.";
                    }
                    else
                    {
                        ComputerCheckResultTextBlock.Text = $"No users found on {computerName}.";
                    }
                });
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() => MessageBox.Show("Check operation cancelled."));
            }
            catch (Exception ex)
            {
                LogError($"Error during check: {ex.Message}");
                Dispatcher.Invoke(() => MessageBox.Show("An error occurred during the check operation."));
            }
            finally
            {
                _cancellationTokenSource = null;
                Dispatcher.Invoke(() =>
                {
                    CheckComputerButton.Content = "Check Computer"; // Switch button back to "Check" mode
                    SearchButton.IsEnabled = true; // Re-enable SearchButton
                });
            }
        }

        private async Task<bool> SearchForUserOnComputerAsync(string username, string computer, CancellationToken cancellationToken, TimeSpan timeout)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "quser.exe",
                    Arguments = $"/server:{computer}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = processStartInfo })
                {
                    process.Start();

                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();
                    var exitTask = process.WaitForExitAsync(cancellationToken);

                    var completedTask = await Task.WhenAny(Task.WhenAll(outputTask, errorTask, exitTask), Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);

                    if (completedTask == Task.WhenAll(outputTask, errorTask, exitTask))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (process.ExitCode != 0)
                        {
                            LogError($"quser.exe failed on {computer} with exit code {process.ExitCode}");
                            return false;
                        }

                        var sessions = ParseQuserOutput(await outputTask.ConfigureAwait(false));
                        foreach (var session in sessions)
                        {
                            if (string.Equals(session.Username, username, StringComparison.OrdinalIgnoreCase))
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    UsersDataGrid.Items.Add(session);
                                });
                                return true;
                            }
                        }
                    }
                    else
                    {
                        LogError($"Timeout occurred while searching for user {username} on computer {computer}");
                        process.Kill();
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error while searching for user {username} on computer {computer}: {ex.Message}");
                return false;
            }

            return false;
        }

        private List<SessionInfo> ParseQuserOutput(string output)
        {
            var sessions = new List<SessionInfo>();
            var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            // Skip the first line (header)
            foreach (var line in lines.Skip(1))
            {
                var columns = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length >= 6)
                {
                    var session = new SessionInfo
                    {
                        Username = columns[0],
                        SessionName = columns[1],
                        Id = columns[2],
                        State = columns[3],
                        IdleTime = columns[4],
                        LogonTime = string.Join(" ", columns.Skip(5)) // LogonTime might contain spaces, so join the remaining columns
                    };

                    sessions.Add(session);
                }
            }

            return sessions;
        }

        private void ToggleUIElements(bool isEnabled)
        {
            SearchButton.IsEnabled = isEnabled;
            DhcpScopeComboBox.IsEnabled = isEnabled;
            CheckComputerButton.IsEnabled = isEnabled;
            // Disable/enable other UI elements as needed
        }

        private void LogError(string message)
        {
            try
            {
                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}{Environment.NewLine}";
                File.AppendAllText(_logFilePath, logMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }
    }

    public class SessionInfo
    {
        public string ComputerName { get; set; }
        public string Username { get; set; }
        public string SessionName { get; set; }
        public string Id { get; set; }
        public string State { get; set; }
        public string IdleTime { get; set; }
        public string LogonTime { get; set; }
    }

    public static class ProcessExtensions
    {
        public static Task WaitForExitAsync(this Process process, CancellationToken cancellationToken = default)
        {
            if (process.HasExited) return Task.CompletedTask;

            var tcs = new TaskCompletionSource<object>();
            process.EnableRaisingEvents = true;

            void ProcessExited(object sender, EventArgs e)
            {
                tcs.TrySetResult(null);
                process.Exited -= ProcessExited;
            }

            process.Exited += ProcessExited;

            if (cancellationToken != default)
            {
                cancellationToken.Register(() =>
                {
                    tcs.TrySetCanceled();
                    process.Exited -= ProcessExited;
                });
            }

            return tcs.Task;
        }
    }
}
