# User Login Checker

User Login Checker is a WPF (Windows Presentation Foundation) application that allows users to check if someone is currently logged into a computer on a network and search for all computers where a specific user is logged in. This tool is particularly useful for system administrators managing large networks.

## Features

- **Check Computer Login Status**: Enter a computer name to see if any users are currently logged in.
- **Search by Username**: Enter a username to find all computers on the network where that user is logged in.
- **Progress Indicator**: Displays a progress bar and status text while searching through the network.
- **Error Handling**: Provides user-friendly error messages and logs errors for diagnostic purposes.

## Prerequisites

- .NET Framework 4.6.2 or later.
- Access to the `quser.exe` tool, which should be located in the `System32` directory or available in the system's PATH.
- Sufficient permissions to query Active Directory and remote computers.

## Installation

1. **Clone the Repository**: 
    ```bash
    git clone https://github.com/yourusername/UserLoginChecker.git
    ```

2. **Build the Project**:
   - Open the solution in Visual Studio.
   - Restore NuGet packages if necessary.
   - Build the solution.

3. **Run the Application**:
   - Press `F5` in Visual Studio or run the compiled `.exe` file from the output directory.

## Usage

### Check Computer Login Status

1. Enter the name of the computer in the "Enter computer name" textbox.
2. Click the "Check Computer" button or press `Enter`.
3. The application will display any users currently logged into the specified computer.

### Search for User Logins

1. Enter the username in the "Enter username" textbox.
2. Click the "Search Computers" button or press `Enter`.
3. The application will search through the network and list all computers where the specified user is logged in.

### Error Handling

- If the application cannot access the Active Directory server, an error message will be displayed, and the textboxes will be cleared.
- If the application cannot find `quser.exe`, it will notify the user with an appropriate message.

## Code Overview

### MainWindow.xaml

This file defines the UI layout for the application using WPF. It includes:

- **Title Bar**: Custom title bar with minimize and close buttons.
- **Input Fields**: Textboxes for entering the computer name and username.
- **Buttons**: "Check Computer" and "Search Computers" buttons to initiate actions.
- **Progress Indicator**: Progress bar and text to show the search status.
- **DataGrid**: Displays the list of computers where the user is logged in.

### MainWindow.xaml.cs

This file contains the application logic, including:

- **Event Handlers**: For buttons and textboxes to handle user input and initiate actions.
- **Async Methods**: To perform background operations like querying Active Directory and checking login status.
- **Helper Methods**: For finding `quser.exe`, creating directory searchers, and managing error messages.
- **Error Handling**: Catches exceptions, displays error messages, and logs errors to a file.

## Troubleshooting

- **`quser.exe` Not Found**: Ensure `quser.exe` is located in `System32` or is available in the system's PATH.
- **Active Directory Errors**: Ensure the application has sufficient permissions to query the network and that the network is accessible.
- **Application Crashes**: Check the `UserLoginCheckerErrorLog.txt` file for detailed error logs.

## License

This project is licensed under the MIT License. See the `LICENSE` file for details.
