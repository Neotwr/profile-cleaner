# ProfileCleaner 🧹

A C# console application designed to clean up unused Windows User Profiles safely and efficiently. It uses WMI to query system profiles, automatically filters out essential/system profiles, allows excluding active sessions, and offers both manual and automatic cleanup modes.

Styled with a modern, responsive console interface using **Spectre.Console**.

---

## ✨ Features

- **🛡️ Auto-Elevation**: Automatically requests Administrator privileges on startup if not already running as Admin.
- **🔍 Smart Filtering**: Automatically detects and ignores:
  - System SIDs (`S-1-5-18`, `S-1-5-19`, `S-1-5-20`).
  - Special and system profiles.
  - Active/loaded sessions (preventing deletion of profiles currently in use).
  - Pre-defined corporate or custom admin profiles (e.g., `Administrador`, `Support`).
- **💡 Operator Protection**: Interactively prompts the operator to select profiles to explicitly protect/ignore before presenting the deletion list.
- **⚡ Execution Modes**:
  - **Manual**: Choose exactly which qualified profiles to delete using a checkbox list.
  - **Automatic**: Safely wipe all eligible profiles at once after double-confirmation.
- **📊 Visual Feedback**: Beautiful tables, styled warning banners, and real-time progress bars for profile deletion.

---

## 🛠️ Prerequisites

- **OS**: Windows (requires WMI and Windows API support).
- **Runtime/SDK**: [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or higher.

---

## 🚀 How to Build and Run

1. Clone or download the repository.
2. Open a terminal in the project directory.
3. Run the following command to build the release binary:
   ```bash
   dotnet build -c Release
   ```
4. Run the executable as Administrator (or let it elevate itself):
   ```bash
   dotnet run
   ```

---

## 📦 Dependencies

- [Spectre.Console](https://github.com/spectreconsole/spectre.console) - For rich console UI elements (tables, prompts, progress bars).
- `System.Management` - For querying `Win32_UserProfile` via WMI.
- `System.DirectoryServices.AccountManagement` - For domain/local user validation.

---

## ⚠️ Safety Disclosures

- Profiles currently loaded or in-use cannot be deleted and are automatically skipped to prevent data corruption.
- System accounts are hard-excluded by SID check.
- Always review the list of selected profiles carefully before confirming deletion.

---

## 📄 License

This project is licensed under the terms of the GNU General Public License v3.0 (GPL-3.0). See the [LICENSE](LICENSE) file for details.
