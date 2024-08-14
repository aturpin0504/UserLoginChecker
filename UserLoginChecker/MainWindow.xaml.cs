using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace UserLoginChecker
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<UserSession> userSessions;
        private readonly UserSearchService userSearchService;
        private readonly int timeoutMinutes;
        private CancellationTokenSource cancellationTokenSource;
        private readonly List<string> errorMessages;

        public MainWindow()
        {
            InitializeComponent();
            userSessions = new ObservableCollection<UserSession>();
            UsersDataGrid.ItemsSource = userSessions;

            string quserPath = Utility.FindQuserPath();
            string dhcpServer = ConfigurationManager.AppSettings["DhcpServer"];
            timeoutMinutes = int.TryParse(ConfigurationManager.AppSettings["TimeoutMinutes"], out int timeout) ? timeout : 5; // Default to 5 minutes

            errorMessages = new List<string>();

            try
            {
                ValidateDhcpServer(dhcpServer);  // Validate the DHCP server IP address
                userSearchService = new UserSearchService(dhcpServer, quserPath);
            }
            catch (Exception ex)
            {
                ShowErrorMessage(ex.Message);
                DisableSearchControls(); // Disable controls since DHCP server is invalid
                return;  // Exit constructor early since there was an error
            }

            Loaded += (s, e) =>
            {
                ComputerNameTextBox.Focus();
                LoadDhcpScopes();  // Load DHCP scopes when the window is loaded
            };
        }

        private void DhcpScopeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ValidateForm();
        }

        // This method handles text changes in the Username TextBox
        private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateForm();
        }

        private void ValidateDhcpServer(string dhcpServer)
        {
            if (!IPAddress.TryParse(dhcpServer, out _))
            {
                throw new Exception($"Invalid DHCP server IP address: {dhcpServer}. Please correct the IP address in the application configuration file. Disabling ability to search computers for a user session until this issue has been corrected.");
            }
        }

        private void DisableSearchControls()
        {
            DhcpScopeComboBox.IsEnabled = false;
            SearchButton.IsEnabled = false;
            UsernameTextBox.IsEnabled = false;
        }

        private void LoadDhcpScopes()
        {
            var scopes = userSearchService.GetDhcpScopes();
            scopes.Add("All Computers");
            DhcpScopeComboBox.ItemsSource = scopes;
            DhcpScopeComboBox.SelectedIndex = scopes.Count > 0 ? 0 : -1;
            ValidateForm();  // Validate the form to enable/disable the Search button
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

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (SearchButton.Content.ToString() == "Cancel")
            {
                cancellationTokenSource?.Cancel();
                return;
            }

            cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(timeoutMinutes));
            try
            {
                SearchButton.Content = "Cancel";
                await SearchButton_ClickAsync(cancellationTokenSource.Token);
            }
            finally
            {
                SearchButton.Content = "Search Computers";
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }
        }

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
                if (SearchButton.Content.ToString() == "Cancel")
                {
                    cancellationTokenSource?.Cancel();
                    return;
                }

                cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(timeoutMinutes));
                try
                {
                    SearchButton.Content = "Cancel";
                    await SearchButton_ClickAsync(cancellationTokenSource.Token);
                }
                finally
                {
                    SearchButton.Content = "Search Computers";
                    cancellationTokenSource.Dispose();
                    cancellationTokenSource = null;
                }
            }
        }

        private async Task CheckComputerButton_ClickAsync()
        {
            string computerName = ComputerNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(computerName))
            {
                ShowWarningMessage("Please enter a computer name.");
                return;
            }

            SetProgressVisibility("Searching...");

            var loggedUsers = await Task.Run(() => userSearchService.GetLoggedInUsers(computerName));

            UpdateComputerCheckResultTextBlock(loggedUsers, computerName);

            HideProgress();
            DisplayErrorSummary();
        }

        private async Task SearchButton_ClickAsync(CancellationToken cancellationToken)
        {
            string usernameFilter = UsernameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(usernameFilter))
            {
                ShowWarningMessage("Please enter a username.");
                return;
            }

            string selectedScope = DhcpScopeComboBox.SelectedItem.ToString();

            SetProgressVisibility("Starting search...");

            userSessions.Clear();
            errorMessages.Clear();

            bool messageBoxShown = false;

            try
            {
                await Task.Run(async () =>
                {
                    try
                    {
                        var computers = selectedScope == "All Computers"
                            ? userSearchService.GetAllComputers()
                            : userSearchService.GetComputersInScope(selectedScope);

                        int totalComputers = computers.Count;
                        int currentCount = 0;

                        foreach (var computerName in computers)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var sessions = userSearchService.GetLoggedInUsers(computerName);

                            var filteredSessions = sessions
                                .Where(s => s.Username.ToLowerInvariant().Contains(usernameFilter.ToLowerInvariant()))
                                .Select(s => new UserSession
                                {
                                    Username = s.Username,
                                    SessionName = s.SessionName,
                                    Id = s.Id,
                                    State = s.State,
                                    IdleTime = s.IdleTime,
                                    LogonTime = s.LogonTime,
                                    ComputerName = computerName
                                })
                                .ToList();

                            if (filteredSessions.Any())
                            {
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    foreach (var session in filteredSessions)
                                    {
                                        userSessions.Add(session);
                                    }
                                });
                            }

                            currentCount++;
                            await Dispatcher.InvokeAsync(() => UpdateProgressText($"{currentCount} of {totalComputers}"));
                        }
                    }
                    catch (Exception ex) when (HandleException(ref messageBoxShown, ex)) { }
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ShowWarningMessage("Search was canceled.");
            }
            finally
            {
                await Dispatcher.InvokeAsync(HideProgress);
                DisplayErrorSummary();
            }
        }

        private void ValidateForm()
        {
            SearchButton.IsEnabled = !string.IsNullOrEmpty(UsernameTextBox.Text.Trim())
                                     && DhcpScopeComboBox.SelectedItem != null;
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
                    case OperationCanceledException _:
                        ShowWarningMessage("The operation was canceled.");
                        break;
                    default:
                        ShowErrorMessage($"An unexpected error occurred: {ex.Message}");
                        break;
                }

                ClearTextboxesAndFocus();
            });

            Utility.LogError(ex.Message);
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

        private void UpdateProgressText(string text)
        {
            if (ProgressTextBlock.Text != text)
            {
                Dispatcher.Invoke(() => ProgressTextBlock.Text = text);
            }
        }

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
                var sb = new StringBuilder($"Users are logged into {computerName}: ");
                for (int i = 0; i < loggedUsers.Count; i++)
                {
                    var user = loggedUsers[i];
                    sb.Append(user.Username);
                    if (i < loggedUsers.Count - 1)
                    {
                        sb.Append(", ");
                    }
                }

                ComputerCheckResultTextBlock.Inlines.Add(new Run(sb.ToString()) { FontWeight = FontWeights.Bold });
            }
            else
            {
                var message = $"No users are currently logged into {computerName}.";
                if (ComputerCheckResultTextBlock.Text != message)
                {
                    ComputerCheckResultTextBlock.Text = message;
                }
            }
        }

        private void ClearTextboxesAndFocus()
        {
            ComputerNameTextBox.Clear();
            UsernameTextBox.Clear();
            ComputerNameTextBox.Focus();
        }

        private void ShowWarningMessage(string message) => MessageBox.Show(message, "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);

        private void ShowErrorMessage(string message) => MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

        private void DisplayErrorSummary()
        {
            if (errorMessages.Any())
            {
                Utility.LogErrors(errorMessages);
                ShowWarningMessage(string.Join(Environment.NewLine, errorMessages));
            }
        }
    }
}
