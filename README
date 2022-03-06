# Crusader Kings III Save File Watcher
Several people online have complained that Crusader Kings III does not backup save files when in Ironman mode. This is an issue when system instabilities or other factors cause the game to crash, which can result in save file corruption. This application is intended to automatically backup the 4 most recent copies of your save files to prevent a full data loss in the event of save file corruption. These backups are stored in `C:\Users\<USER>\Documents\Paradox Interactive\Crusader Kings III\backups`.

NOTE: This program is intended for Windows only. Migrating the codebase for Linux or MacOS shouldn't be impossible in the future if there is need for it.

## Installation
1. Download the latest release bundle.
1. Extract the bundle somewhere where it won't get deleted.
1. Open the `appsettings.json` file and set the `User` property to your Windows user name.
1. Run `sc.exe create "CK3 Ironman Watcher" binpath="C:\Path\To\Exe\Ck3IronmanWatcherService.exe"` in a command prompt with administrator permissions.
1. Open up the Windows service manager with administrator permissions.
1. Find the Ck3 Ironman Watcher service in the list. Right click it and go to properties.
1. Change the startup type to Automatic.
1. Click apply and okay.
1. Right click the Ck3 Ironman Watcher service and click start.

## Building from source
1. Clone this repository
1. Cd into the `Ck3IronmanWatcherService` directory
1. Run `dotnet publish --sc`
1. Cd into `bin\Debug\net6.0\win-x64\publish`. There should be an executable in that directory.
