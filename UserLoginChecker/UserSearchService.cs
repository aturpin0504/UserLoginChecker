using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace UserLoginChecker
{
    public class UserSearchService
    {
        private readonly string dhcpServer;
        private readonly string quserPath;

        public UserSearchService(string dhcpServer, string quserPath)
        {
            this.dhcpServer = dhcpServer;
            this.quserPath = quserPath;
        }

        public List<string> GetDhcpScopes()
        {
            var scopes = new List<string>();

            try
            {
                var dhcpServerIp = IPAddress.Parse(dhcpServer);
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    var properties = adapter.GetIPProperties();
                    foreach (var scope in properties.DhcpServerAddresses)
                    {
                        if (scope.Equals(dhcpServerIp))
                        {
                            scopes.Add(adapter.Description);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving DHCP scopes: {ex.Message}", ex);
            }

            return scopes;
        }

        public List<string> GetAllComputers()
        {
            var computers = new List<string>();

            try
            {
                using (var context = new PrincipalContext(ContextType.Domain))
                using (var searcher = new DirectorySearcher(new DirectoryEntry("LDAP://" + context.ConnectedServer)))
                {
                    searcher.Filter = "(objectClass=computer)";
                    searcher.PropertiesToLoad.Add("name");
                    searcher.PageSize = 1000;

                    foreach (SearchResult result in searcher.FindAll())
                    {
                        if (result.Properties.Contains("name"))
                        {
                            computers.Add(result.Properties["name"][0].ToString());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving computers from Active Directory: {ex.Message}", ex);
            }

            return computers;
        }

        public List<string> GetComputersInScope(string scope)
        {
            var computers = new List<string>();

            try
            {
                var script = $@"
                    $dhcpServer = '{dhcpServer}'
                    $scopeId = '{scope}'
                    Get-DhcpServerv4Lease -ComputerName $dhcpServer -ScopeId $scopeId | Select-Object -ExpandProperty HostName
                ";

                computers = ExecutePowerShellScript(script);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error retrieving computers from DHCP scope: {ex.Message}", ex);
            }

            return computers;
        }

        private List<string> ExecutePowerShellScript(string script)
        {
            var results = new List<string>();

            using (PowerShell ps = PowerShell.Create())
            {
                ps.AddScript(script);

                foreach (var result in ps.Invoke())
                {
                    results.Add(result.ToString());
                }

                if (ps.Streams.Error.Count > 0)
                {
                    throw new Exception(string.Join(Environment.NewLine, ps.Streams.Error.Select(e => e.ToString())));
                }
            }

            return results;
        }

        public List<UserSession> GetLoggedInUsers(string computerName)
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
                        if (errorOutput.Contains("Access is denied"))
                        {
                            throw new UnauthorizedAccessException($"Access denied to {computerName}. You must have administrative privileges.");
                        }
                        else
                        {
                            throw new Exception(errorOutput);
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException(ex.Message);  // Re-throw to handle it in the UI layer
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error querying {computerName}: {ex.Message}";
                Utility.LogError(errorMessage);
                throw new Exception(errorMessage);
            }

            return sessions;
        }


        private static readonly Regex QUserLineRegex = new Regex(@"\s*(?<USERNAME>\S+)\s+(?<SESSIONNAME>\S+)\s+(?<ID>\d+)\s+(?<STATE>\S+)\s+(?<IDLE>\S+)\s+(?<LOGONTIME>.+)", RegexOptions.Compiled);

        private UserSession? ParseQUserLine(string line)
        {
            var match = QUserLineRegex.Match(line);

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
    }
}
