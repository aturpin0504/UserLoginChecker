# User Login Checker

## Overview

**User Login Checker** is a WPF-based application that allows network administrators to easily check which users are logged into computers within a specified DHCP scope or across the entire network. This tool provides a simple and efficient way to monitor user sessions on remote machines, utilizing PowerShell and the `quser.exe` command to gather session information.

## Features

- **Check Specific Computers**: Enter a computer name to check if any users are logged into that machine.
- **Search by DHCP Scope**: Select a DHCP scope to search for user sessions across all computers within that scope.
- **Real-Time Feedback**: Progress is displayed in real-time as the application searches for user sessions, with a detailed status bar and progress indicator.
- **Cancelable Operations**: Ongoing operations can be canceled at any time by clicking the "Cancel" button.
- **Detailed Results**: Results are displayed in a sortable and resizable `DataGrid`, allowing you to see session details such as the computer name, username, session ID, and more.

## Requirements

- **.NET Framework 4.6.1** or higher
- **Windows OS** with PowerShell installed
- **Network Access** to the target computers and DHCP servers
- **Administrator Privileges** may be required to execute certain commands

## Installation

1. **Clone the Repository**:
   ```bash
   git clone https://github.com/your-username/user-login-checker.git
   cd user-login-checker
   ```

2. **Build the Application**:
   - Open the solution file in Visual Studio.
   - Build the solution to generate the executable.

3. **Configure Application Settings**:
   - Modify the `App.config` file to include your DHCP server name:
     ```xml
     <appSettings>
         <add key="DhcpServerName" value="your-dhcp-server-name" />
         <add key="TaskTimeoutMinutes" value="60" />
     </appSettings>
     ```

4. **Run the Application**:
   - Launch the application from Visual Studio or by running the generated executable.

## Usage

### 1. **Check a Specific Computer**

- **Enter the Computer Name**: In the "Check Computer" section, enter the name of the computer you want to check.
- **Click "Check Computer"**: The application will search for active user sessions on the specified computer. Results will be displayed below.

### 2. **Search by DHCP Scope**

- **Select a DHCP Scope**: From the "Select DHCP Scope" dropdown, choose the DHCP scope to search within. You can also select "All Computers" to search across the entire network.
- **Enter a Username**: In the "Search Computers" section, enter the username you want to search for.
- **Click "Search Computers"**: The application will search all computers within the selected DHCP scope for the specified username. Progress is shown in real-time, and results are displayed in the `DataGrid`.

### 3. **Canceling an Operation**

- **Click "Cancel"**: If an operation is taking too long or you wish to stop it, simply click the "Cancel" button. The operation will be terminated, and the application will return to the ready state.

### 4. **Viewing Results**

- **DataGrid Interaction**: The results are displayed in a `DataGrid`, where you can sort by columns, resize columns, and review detailed session information such as the computer name, username, session name, session ID, state, idle time, and logon time.

### 5. **Status Bar**

- **Monitor Application State**: The status bar at the bottom of the window provides real-time feedback, such as "Ready", "Processing...", or "Operation Canceled".

## Logging

- **Log Files**: The application generates log files in the user's application data folder. Logs are named using the format `UserLoginChecker_{username}_{yyyyMMdd}.log`.
- **Error Tracking**: All errors encountered during operations are logged with timestamps for easy troubleshooting.

## Troubleshooting

- **No Results Found**: Ensure that the computer name or DHCP scope is correct, and that the target computers are reachable.
- **Access Denied**: If the application fails to retrieve information, ensure you have sufficient permissions and that PowerShell can run with the necessary privileges.

## Contributing

- **Pull Requests**: Contributions are welcome! Please fork the repository and create a pull request with your changes.
- **Issues**: Report issues via the GitHub Issues tab.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
