using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.DirectoryServices.AccountManagement;
using System.Windows.Documents;
using System.DirectoryServices.Protocols;
using System.Windows.Controls;

namespace UserLoginChecker
{
    public partial class MainWindow : Window
    {
        private readonly List<string> errorMessages = new List<string>();
        private readonly ObservableCollection<UserSession> userSessions;
        private readonly string quserPath;

        public MainWindow()
        {
            InitializeComponent();
            userSessions = new ObservableCollection<UserSession>();
            UsersDataGrid.ItemsSource = userSessions;

            quserPath = FindQuserPath();

            Loaded += (s, e) => ComputerNameTextBox.Focus();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private async void CheckComputerButton_Click(object sender, RoutedEventArgs e) => await CheckComputerButton_ClickAsync();

        private async void SearchButton_Click(object sender, RoutedEventArgs e) => await SearchButton_ClickAsync();

        private async void ComputerNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await CheckComputerButton_ClickAsync();
                ResetAndFocusTextBox(ComputerNameTextBox);
            }
        }

        private async void UsernameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await SearchButton_ClickAsync();
                ResetAndFocusTextBox(UsernameTextBox);
            }
        }

        private async Task CheckComputerButton_ClickAsync()
        {
            if (IsQuserPathInvalid())
            {
                return;
            }

            string computerName = ComputerNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(computerName))
            {
                ShowWarningMessage("Please enter a computer name.");
                return;
            }

            SetProgressVisibility("Searching...");

            var loggedUsers = await Task.Run(() => GetLoggedInUsers(computerName));

            UpdateComputerCheckResultTextBlock(loggedUsers, computerName);

            HideProgress();
            DisplayErrorSummary();
        }

        private async Task SearchButton_ClickAsync()
        {
            if (IsQuserPathInvalid())
            {
                return;
            }

            string usernameFilter = UsernameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(usernameFilter))
            {
                ShowWarningMessage("Please enter a username.");
                return;
            }

            SetProgressVisibility("Starting search...");

            errorMessages.Clear();
            userSessions.Clear();

            bool messageBoxShown = false;

            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        int totalComputers = 0;
                        int currentCount = 0;

                        using (var context = CreatePrincipalContext())
                        using (var searcher = CreateDirectorySearcher(context))
                        {
                            totalComputers = searcher.FindAll().Count;

                            foreach (SearchResult searchResult in searcher.FindAll())
                            {
                                string computerName = searchResult.Properties["name"][0].ToString();
                                var sessions = GetLoggedInUsers(computerName);

                                lock (userSessions)
                                {
                                    foreach (var session in sessions)
                                    {
                                        userSessions.Add(session);
                                    }
                                }

                                currentCount++;
                                UpdateProgressText($"{currentCount} of {totalComputers}");
                            }
                        }
                    }
                    catch (Exception ex) when (HandleException(ref messageBoxShown, ex)) { }
                });
            }
            finally
            {
                HideProgress();
                DisplayErrorSummary();
            }
        }

        private bool HandleException(ref bool messageBoxShown, Exception ex)
        {
            if (messageBoxShown) return true;

            messageBoxShown = true;

            Dispatcher.Invoke(() =>
            {
                switch (ex)
                {
                    case PrincipalServerDownException _:
                        ShowErrorMessage("Unable to contact the Active Directory server. Please ensure you are connected to the network and try again.");
                        break;
                    case LdapException _:
                        ShowErrorMessage("The LDAP server is unavailable. Please check your network connection and try again.");
                        break;
                    default:
                        ShowErrorMessage($"An unexpected error occurred: {ex.Message}");
                        break;
                }

                ClearTextboxesAndFocus();
            });

            LogError(ex.Message);
            return true;
        }

        private void ResetAndFocusTextBox(TextBox textBox)
        {
            textBox.Clear();
            textBox.Focus();
        }

        private void SetProgressVisibility(string message)
        {
            SearchProgressBar.Visibility = Visibility.Visible;
            ProgressTextBlock.Visibility = Visibility.Visible;
            ProgressTextBlock.Text = message;
        }

        private void UpdateProgressText(string text) => Dispatcher.Invoke(() => ProgressTextBlock.Text = text);

        private void HideProgress()
        {
            SearchProgressBar.Visibility = Visibility.Collapsed;
            ProgressTextBlock.Text = "Search complete";
        }

        private void UpdateComputerCheckResultTextBlock(List<UserSession> loggedUsers, string computerName)
        {
            ComputerCheckResultTextBlock.Inlines.Clear();

            if (loggedUsers.Any())
            {
                ComputerCheckResultTextBlock.Inlines.Add(new Run($"Users are logged into {computerName}: "));

                for (int i = 0; i < loggedUsers.Count; i++)
                {
                    var user = loggedUsers[i];
                    ComputerCheckResultTextBlock.Inlines.Add(new Run(user.Username) { FontWeight = FontWeights.Bold });

                    if (i < loggedUsers.Count - 1)
                    {
                        ComputerCheckResultTextBlock.Inlines.Add(new Run(", "));
                    }
                }
            }
            else
            {
                ComputerCheckResultTextBlock.Text = $"No users are currently logged into {computerName}.";
            }
        }

        private void ClearTextboxesAndFocus()
        {
            ComputerNameTextBox.Clear();
            UsernameTextBox.Clear();
            ComputerNameTextBox.Focus();
        }

        private PrincipalContext CreatePrincipalContext() => new PrincipalContext(ContextType.Domain);

        private DirectorySearcher CreateDirectorySearcher(PrincipalContext context)
        {
            var searcher = new DirectorySearcher(new DirectoryEntry("LDAP://" + context.ConnectedServer))
            {
                Filter = "(objectClass=computer)"
            };
            searcher.PropertiesToLoad.Add("name");

            return searcher;
        }

        private bool IsQuserPathInvalid()
        {
            if (string.IsNullOrEmpty(quserPath))
            {
                ShowErrorMessage("quser.exe path is not set. Unable to perform the operation.");
                return true;
            }
            return false;
        }

        private void ShowWarningMessage(string message) => MessageBox.Show(message, "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);

        private void ShowErrorMessage(string message) => MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

        private string FindQuserPath()
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

            ShowErrorMessage("quser.exe not found. Please ensure it is available in your PATH or System32 directory.");
            return null;
        }

        private List<UserSession> GetLoggedInUsers(string computerName)
        {
            var sessions = new List<UserSession>();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = quserPath,
                    Arguments = $"/server:{computerName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    var output = process.StandardOutput.ReadToEnd();
                    var errorOutput = process.StandardError.ReadToEnd();

                    if (!string.IsNullOrEmpty(output))
                    {
                        var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Skip(1);
                        foreach (var line in lines)
                        {
                            var session = ParseQUserLine(line);
                            if (session.HasValue)
                            {
                                sessions.Add(session.Value);
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(errorOutput))
                    {
                        throw new Exception(errorOutput);
                    }
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error querying {computerName}: {ex.Message}";
                errorMessages.Add(errorMessage);
                LogError(errorMessage);
            }

            return sessions;
        }

        private void DisplayErrorSummary()
        {
            if (errorMessages.Any())
            {
                ShowWarningMessage(string.Join(Environment.NewLine, errorMessages));
            }
        }

        private UserSession? ParseQUserLine(string line)
        {
            var regex = new Regex(@"\s*(?<USERNAME>\S+)\s+(?<SESSIONNAME>\S+)\s+(?<ID>\d+)\s+(?<STATE>\S+)\s+(?<IDLE>\S+)\s+(?<LOGONTIME>.+)");
            var match = regex.Match(line);

            return match.Success
                ? new UserSession
                {
                    Username = match.Groups["USERNAME"].Value,
                    SessionName = match.Groups["SESSIONNAME"].Value,
                    Id = match.Groups["ID"].Value,
                    State = match.Groups["STATE"].Value,
                    IdleTime = match.Groups["IDLE"].Value,
                    LogonTime = match.Groups["LOGONTIME"].Value
                }
                : (UserSession?)null;
        }

        private void LogError(string message)
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
                ShowErrorMessage($"Failed to write to log file: {ex.Message}");
            }
        }
    }

    public struct UserSession
    {
        public string Username { get; set; }
        public string SessionName { get; set; }
        public string Id { get; set; }
        public string State { get; set; }
        public string IdleTime { get; set; }
        public string LogonTime { get; set; }
    }
}
