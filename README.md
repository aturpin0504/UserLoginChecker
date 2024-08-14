# User Login Checker

## Overview

The **User Login Checker** is a WPF application designed to help IT administrators and support staff check which users are logged into computers within a specific DHCP scope or across all computers in a domain. The application allows you to search for active user sessions on remote computers based on either a specific username or a computer name.

## Features

- **Search by Computer Name**: Check if any users are logged into a specific computer.
- **Search by Username**: Find all computers where a specific user is logged in.
- **Filter by DHCP Scope**: Narrow your search to computers within a specific DHCP scope or search across all computers in the domain.
- **Progress Feedback**: Provides real-time feedback on the progress of searches.
- **Cancel Search**: Ability to cancel long-running searches.

## Requirements

- **.NET Framework**: Version 4.6.1 or later.
- **Windows OS**: The application is designed to run on Windows.
- **Admin Privileges**: Administrator privileges are required to execute remote commands and retrieve session information from other computers.

## Installation

1. **Download**: Obtain the latest version of the application from the repository or distribution source.
2. **Extract**: Unzip the contents into a directory of your choice.
3. **Configuration**: Modify the `app.config` file to set the correct DHCP server and timeout settings.

## Configuration

Before using the application, you'll need to configure the `app.config` file:

1. **Open `app.config`**: Located in the application directory.
2. **Set DHCP Server**: Replace the `DhcpServer` value with the server name or IP address of your DHCP server.

   ```xml
   <appSettings>
     <add key="DhcpServer" value="dhcp-server.local" />
     <add key="TimeoutMinutes" value="60" />
   </appSettings>
   ```

   - `DhcpServer`: This can be either the server name (e.g., `dhcp-server.local`) or an IP address (e.g., `192.168.1.1`) of the DHCP server that manages the scopes you want to query.
   - `TimeoutMinutes`: The number of minutes before a search times out.

3. **Save**: Save your changes to the `app.config` file.

## Usage

### Launching the Application

1. **Run the Application**: Double-click the `UserLoginChecker.exe` file to launch the application.

### Checking a Computer for Logged-In Users

1. **Enter Computer Name**: In the "Enter computer name" field, type the name of the computer you want to check.
2. **Click "Check Computer"**: The application will retrieve and display the users currently logged into that computer.

### Searching for a User Across Computers

1. **Select DHCP Scope**: Use the dropdown to select the DHCP scope you want to search within. Choose "All Computers" to search across the entire domain.
2. **Enter Username**: Type the username you want to search for in the "Enter username" field.
3. **Click "Search Computers"**: The application will search through the computers in the selected DHCP scope and display the results in the grid.
4. **Cancel Search**: If a search is taking too long, you can click "Cancel" to stop the operation.

### Viewing Results

- **Data Grid**: Results are displayed in the grid, showing the computer name, username, session name, and other session details.
- **Progress Bar**: The progress bar and text will update to show the current progress of the search.

### Error Handling

- **Invalid DHCP Server**: If the DHCP server name or IP address in the configuration is invalid or cannot be resolved, the application will display an error message and close. You'll need to correct the server information in the `app.config` file before restarting the application.
- **Access Denied Errors**: If you do not have administrative privileges on a target computer, the application will inform you of which computers were inaccessible due to lack of permissions after the search completes.
- **Missing Inputs**: If required fields are left empty (e.g., username or DHCP scope), the application will prompt you to complete these fields before proceeding.

## Troubleshooting

- **No Results Found**: Ensure that the computer names and usernames entered are correct and that the computers are within the selected DHCP scope.
- **Cannot Connect to DHCP Server**: Verify that the DHCP server name or IP address is correct and that the application has network access to the server.
- **Timeouts**: If searches are frequently timing out, consider increasing the `TimeoutMinutes` value in the `app.config` file.
- **Access Denied on Computers**: Ensure that you have administrative privileges on the computers you are querying.

## Contributing

Contributions to the project are welcome. Please fork the repository, make your changes, and submit a pull request.

## License

This project is licensed under the MIT License. See the `LICENSE` file for more information.
