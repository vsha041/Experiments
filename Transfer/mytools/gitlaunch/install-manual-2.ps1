# install-manual.ps1
#
# The same install as install.ps1, split into steps you paste ONE AT A TIME into ONE
# PowerShell 7 window, so you can see what each step does. No admin rights needed.
#
# How to use:
#   - Open PowerShell 7 (pwsh), not Windows PowerShell 5.1.
#   - Paste Step 0 first, after editing $setup. Every later step uses the variables it sets.
#     If you close the window halfway, paste Step 0 again and carry on.
#   - Paste each step on its own, in order, and read the output before the next one.
#   - Steps that only apply in some cases check that themselves and print what they did.
#   - To undo everything later:  pwsh -File .\install.ps1 -Uninstall
#
# Running this file as a whole does nothing (the line below stops it); use install.ps1 for that.
if ($PSCommandPath) { Write-Warning 'install-manual.ps1 is for pasting one step at a time. To install in one go, run install.ps1.'; return }


# ===== Step 0: set the paths every step uses ============================================
# EDIT the first line: the folder you copied (the one that contains install.ps1).
# The module and your real profile go under AppData\Local, which OneDrive doesn't sync. Only two
# small files stay in Documents\PowerShell: powershell.config.json, which tells PowerShell where
# the modules are (Step 13), and a profile that loads the one in AppData (Step 16).
$setup       = 'C:\Users\Valkyrie\mytools\gitlaunch'
$modulesRoot = Join-Path $env:LOCALAPPDATA 'PowerShell\Modules'
$moduleDir   = Join-Path $modulesRoot 'GitTools'
$bin         = Join-Path $moduleDir 'bin'
$profilePath = Join-Path $env:LOCALAPPDATA 'PowerShell\profile.ps1'
$stubPath    = $PROFILE.CurrentUserAllHosts
$configPath  = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'PowerShell\powershell.config.json'
$copilotDir  = Join-Path $HOME '.copilot'
$rid         = if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'win-arm64' } else { 'win-x64' }
$publishDir  = Join-Path $setup "src\publish\$rid"
# Remembered for Step 18, so install.ps1 -Uninstall knows what to undo later:
$gitRootVarSet = $false; $profileCreated = $false; $copilotFilesCreated = @(); $binFiles = ''
$configFileCreated = $false; $psModulePathSet = $false
[pscustomobject]@{
    SetupFolder   = $setup
    SetupFolderOK = Test-Path (Join-Path $setup 'install.ps1')   # must be True
    ModuleDir     = $moduleDir
    Profile       = $profilePath
    ProfileLoader = $stubPath
    Config        = $configPath
    CopilotDir    = $copilotDir
    Platform      = $rid
} | Format-List


# ===== Step 1: PowerShell version =========================================================
# Must be 7.x. GitTools doesn't load in Windows PowerShell 5.1.
$PSVersionTable.PSVersion


# ===== Step 2: execution policy ===========================================================
# A new PowerShell window (and every Copilot command) uses the first scope from the top that
# isn't "Undefined". If they're all Undefined, PowerShell 7 uses RemoteSigned.
#   OK:      RemoteSigned, Unrestricted, Bypass
#   Not OK:  AllSigned or Restricted. In MachinePolicy/UserPolicy that's set by IT: stop here.
#            Only in CurrentUser/LocalMachine? Allow your own scripts with:
#              Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
Get-ExecutionPolicy -List


# ===== Step 3: .NET SDK ===================================================================
# Needs a line starting with 10. or higher (used in Step 6 to build gitlaunch.exe).
dotnet --list-sdks


# ===== Step 4: where is Git? ==============================================================
# The launcher looks in this order: GITTOOLS_GIT_ROOT, the registry entry Git's installer
# writes, then C:\Program Files\Git. It needs the folder that contains usr\bin.
"GITTOOLS_GIT_ROOT : $env:GITTOOLS_GIT_ROOT"
"Registry (machine): $((Get-ItemProperty HKLM:\SOFTWARE\GitForWindows -ErrorAction SilentlyContinue).InstallPath)"
"Registry (user)   : $((Get-ItemProperty HKCU:\SOFTWARE\GitForWindows -ErrorAction SilentlyContinue).InstallPath)"
"git.exe on PATH   : $((Get-Command git -ErrorAction SilentlyContinue).Source)"
$gitRoot = $env:GITTOOLS_GIT_ROOT
if (-not $gitRoot) { $gitRoot = (Get-ItemProperty HKLM:\SOFTWARE\GitForWindows -ErrorAction SilentlyContinue).InstallPath }
if (-not $gitRoot) { $gitRoot = (Get-ItemProperty HKCU:\SOFTWARE\GitForWindows -ErrorAction SilentlyContinue).InstallPath }
if (-not $gitRoot) { $gitRoot = Join-Path $env:ProgramFiles 'Git' }
"The launcher will use: $gitRoot"
"Has usr\bin\grep.exe : $(Test-Path (Join-Path $gitRoot 'usr\bin\grep.exe'))"   # True = fine


# ===== Step 5: (only if Step 4 printed False) point GITTOOLS_GIT_ROOT at Git =============
# Walks up from git.exe (e.g. ...\Git\cmd\git.exe) to the folder that has usr\bin\grep.exe
# and saves it as a user environment variable. Does nothing if Step 4 printed True.
if (Test-Path (Join-Path $gitRoot 'usr\bin\grep.exe')) {
    "Not needed: the launcher finds Git at $gitRoot"
}
else {
    $dir = Split-Path (Get-Command git -ErrorAction SilentlyContinue).Source
    while ($dir -and -not (Test-Path (Join-Path $dir 'usr\bin\grep.exe'))) { $dir = Split-Path $dir }
    if (-not $dir) {
        Write-Warning 'Git for Windows not found. Install it, or set GITTOOLS_GIT_ROOT by hand to the folder that contains usr\bin.'
    }
    else {
        $gitRoot = $dir
        [Environment]::SetEnvironmentVariable('GITTOOLS_GIT_ROOT', $gitRoot, 'User')   # for new windows
        $env:GITTOOLS_GIT_ROOT = $gitRoot                                              # for this window
        $gitRootVarSet = $true
        "Set the user variable GITTOOLS_GIT_ROOT = $gitRoot"
    }
}


# ===== Step 6: build gitlaunch.exe ========================================================
# Compiles src\Program.cs into one exe (10-30 seconds the first time). The last lines should
# say "gitlaunch -> ...\src\publish\win-x64\".
dotnet publish (Join-Path $setup 'src\gitlaunch.csproj') -c Release -r $rid -o $publishDir --nologo
Get-Item (Join-Path $publishDir 'gitlaunch.exe') | Select-Object FullName, Length, LastWriteTime


# ===== Step 7: create the module folder and clear out old exes ============================
# An error here means one of the tools is still running (a terminal or Copilot session):
# close it and paste this step again.
New-Item -ItemType Directory -Force $bin | Out-Null
Get-ChildItem $bin -Filter '*.exe' | Remove-Item -Force
Get-ChildItem $moduleDir -Recurse | Select-Object FullName


# ===== Step 8: copy the module files and the launcher =====================================
Copy-Item (Join-Path $setup 'module\GitTools.psd1'), (Join-Path $setup 'module\GitTools.psm1') $moduleDir -Force
Copy-Item (Join-Path $publishDir 'gitlaunch.exe') $bin
Get-ChildItem $moduleDir -Recurse -File | Select-Object FullName, Length


# ===== Step 9: hard links or copies? ======================================================
# One exe per tool name is needed (bin\grep.exe, bin\sed.exe, ...). Normally these are hard
# links to gitlaunch.exe (no extra disk space). If the module folder is synced by OneDrive,
# plain copies are used instead (about 13 MB), since OneDrive and hard links don't mix well.
# With the module in AppData this should say hard links.
$names = (Import-PowerShellDataFile (Join-Path $moduleDir 'GitTools.psd1')).AliasesToExport
$inOneDrive = @($env:OneDrive, $env:OneDriveCommercial, $env:OneDriveConsumer |
    Where-Object { $_ -and $moduleDir.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0
"$($names.Count) tool names: $($names -join ', ')"
"Module folder in OneDrive: $inOneDrive  ->  will use $(if ($inOneDrive) { 'plain copies' } else { 'hard links' })"


# ===== Step 10: create bin\<name>.exe for every tool ======================================
foreach ($name in $names) {
    $path = Join-Path $bin "$name.exe"
    #if ($inOneDrive) { Copy-Item (Join-Path $bin 'gitlaunch.exe') $path }
    #else { New-Item -ItemType HardLink -Path $path -Target (Join-Path $bin 'gitlaunch.exe') | Out-Null }
	Copy-Item (Join-Path $bin 'gitlaunch.exe') $path
}
#$binFiles = if ($inOneDrive) { 'copy' } else { 'hardlink' }
$binFiles = 'copy'
"exe files in bin: $((Get-ChildItem $bin -Filter '*.exe').Count)"   # 72 = gitlaunch.exe + 71 tools


# ===== Step 11: clear the "downloaded from the internet" mark =============================
# Files that came by download, email or chat carry a mark that makes PowerShell refuse to
# load the module. These are your own files. (Harmless if there is no mark.)
Get-ChildItem $moduleDir -Recurse -File | Unblock-File
'Unblocked.'


# ===== Step 12: run the launcher directly =================================================
# a) gitlaunch.exe on its own should refuse politely and return exit code 2:
& (Join-Path $bin 'gitlaunch.exe'); "exit code: $LASTEXITCODE"
# b) Through a tool name: the three arguments must come back unchanged:
#      [a "b" c|don't|*.cs]
#    "blocked by group policy"  -> your company blocks programs in your user folder (needs IT)
#    a message about .NET       -> the .NET 10 runtime (or newer) is missing
#    "...not found (Git root:"  -> go back to Steps 4-5
& (Join-Path $bin 'printf.exe') '[%s|%s|%s]\n' 'a "b" c' "don't" '*.cs'; "exit code: $LASTEXITCODE"


# ===== Step 13: tell PowerShell where the module folder is ================================
# PowerShell 7 normally looks for your own modules only in Documents\PowerShell\Modules. A
# PSModulePath entry in your user powershell.config.json (which stays in Documents\PowerShell)
# makes it use $modulesRoot instead, in every pwsh start, Copilot's pwsh -NoProfile included.
# Other settings in that file are kept. If PSModulePath already points somewhere else, the file
# is left alone and you get a warning. Note: Documents\PowerShell\Modules is then no longer
# searched, and Install-Module still installs there.
$configOK = $false
$configExists = Test-Path $configPath
try {
    $raw = if ($configExists) { Get-Content $configPath -Raw } else { $null }
    $config = if ($raw -and $raw.Trim()) { $raw | ConvertFrom-Json -AsHashtable -ErrorAction Stop } else { [ordered]@{} }
    $current = $config['PSModulePath']
    if ($current -and [Environment]::ExpandEnvironmentVariables($current).TrimEnd('\') -ne $modulesRoot) {
        Write-Warning "$configPath already sets PSModulePath to '$current'. Nothing changed: edit that entry by hand to $modulesRoot."
    }
    elseif ($current) {
        $configOK = $true
        "Already set in $configPath - nothing to change."
    }
    else {
        $config['PSModulePath'] = $modulesRoot
        New-Item -ItemType Directory -Force (Split-Path $configPath) | Out-Null
        $config | ConvertTo-Json -Depth 10 | Set-Content $configPath
        $configFileCreated = -not $configExists
        $psModulePathSet = $true
        $configOK = $true
        "Set PSModulePath in $configPath"
    }
}
catch {
    Write-Warning "Couldn't read $configPath ($($_.Exception.Message)). Nothing changed: fix the JSON by hand first."
}
# New windows read the file when they start; this window gets the folder added directly:
if ($configOK -and ($env:PSModulePath -split ';') -notcontains $modulesRoot) { $env:PSModulePath = "$modulesRoot;$env:PSModulePath" }
if (Test-Path $configPath) { Get-Content $configPath }


# ===== Step 14: load the module in this window and try it =================================
Import-Module GitTools -Force
"aliases: $((Get-Alias | Where-Object Source -eq GitTools).Count)"   # 71
Get-Command grep, gfind, xargs | Format-Table Name, CommandType, Definition -AutoSize
awk 'BEGIN { printf "launcher ok: %d\n", 42 }'
'apple', 'banana', 'mango' | grep -c an                                # 2 (banana, mango)


# ===== Step 15: try it the way Copilot runs commands ======================================
# A new PowerShell process WITHOUT a profile: the module has to load by itself.
pwsh -NoProfile -NonInteractive -Command {
    "grep comes from: $((Get-Command grep).Source)"                     # GitTools
    awk 'BEGIN { printf "copilot-style ok: %d\n", 7 }'
}


# ===== Step 16: add the Import-Module block to your profile ===============================
# Your profile lives in AppData ($profilePath). The profile in Documents\PowerShell ($stubPath)
# only holds a short block that loads it.
# a) Adds the GitTools block to the AppData profile; skipped if it's already there.
# b) Adds the loader block to the Documents profile; skipped if that profile already loads a
#    profile.ps1 from AppData. install.ps1 -Uninstall leaves the loader in place, since your
#    own profile content depends on it.
# Shows the end of both files afterwards.
if ((Test-Path $profilePath) -and (Select-String -Path $profilePath -Pattern 'Import-Module\s+GitTools' -Quiet)) {
    "Already in $profilePath - nothing to add."
}
else {
    $profileCreated = -not (Test-Path $profilePath)
    New-Item -ItemType Directory -Force (Split-Path $profilePath) | Out-Null
    Add-Content -Path $profilePath -Value @'

# >>> GitTools (managed by gitlaunch\install.ps1) >>>
# Git for Windows GNU tools (grep, sed, awk, gfind, ...). The module also auto-loads without
# a profile (e.g. Copilot CLI's pwsh -NoProfile); this loads it up front in interactive windows.
Import-Module GitTools -ErrorAction SilentlyContinue
# <<< GitTools <<<
'@
    "Added to $profilePath"
}
if ((Test-Path $stubPath) -and (Select-String -Path $stubPath -Pattern '(LOCALAPPDATA|AppData\\Local)[^\r\n]*profile\.ps1' -Quiet)) {
    "$stubPath already loads the AppData profile - nothing to add."
}
else {
    New-Item -ItemType Directory -Force (Split-Path $stubPath) | Out-Null
    Add-Content -Path $stubPath -Value @'

# >>> Load the profile in AppData (added by gitlaunch\install-manual.ps1) >>>
# The real profile is %LOCALAPPDATA%\PowerShell\profile.ps1, outside OneDrive.
$localProfile = Join-Path $env:LOCALAPPDATA 'PowerShell\profile.ps1'
if (Test-Path $localProfile) { . $localProfile }
# <<< Load the profile in AppData <<<
'@
    "Added the loader to $stubPath"
}
if ((Test-Path $stubPath) -and (Select-String -Path $stubPath -Pattern 'Import-Module\s+GitTools' -Quiet)) {
    Write-Warning "$stubPath still has a GitTools block from an earlier install. It's harmless, but you can delete it."
}
"--- $profilePath"; Get-Content $profilePath | Select-Object -Last 6
"--- $stubPath";    Get-Content $stubPath | Select-Object -Last 6


# ===== Step 17: add the Shell tools section to the Copilot instructions ===================
# Tells Copilot about gfind/gsort/gtimeout/gdiff/gbash and PowerShell's quoting catches.
# copilot-instructions.md is created if missing; the copy in instructions\ is only updated
# if it already exists. Skipped for a file that already has a Shell tools section.
$section = (Get-Content (Join-Path $setup 'copilot-shell-tools.md') -Raw).TrimEnd()
$block = "`n<!-- GitTools: begin (managed by gitlaunch\install.ps1; edit copilot-shell-tools.md there) -->`n$section`n<!-- GitTools: end -->"
foreach ($file in (Join-Path $copilotDir 'copilot-instructions.md'), (Join-Path $copilotDir 'instructions\copilot-instructions.md')) {
    $exists = Test-Path $file
    if (-not $exists -and $file -like '*\instructions\*') { "Skipped (doesn't exist): $file"; continue }
    if ($exists -and (Select-String -Path $file -Pattern '## Shell tools' -Quiet)) { "Already has the section: $file"; continue }
    New-Item -ItemType Directory -Force (Split-Path $file) | Out-Null
    Add-Content -Path $file -Value $block
    if (-not $exists) { $copilotFilesCreated += $file }
    "Added to $file"
}


# ===== Step 18: record what was done ======================================================
# Saves install-state.json in the module folder, the same file install.ps1 writes, so that
# "install.ps1 -Uninstall" knows what to undo (e.g. GITTOOLS_GIT_ROOT only if Step 5 set it,
# the PSModulePath entry only if Step 13 set it). If you ran these steps before, what that
# earlier run did is kept as well.
$statePath = Join-Path $moduleDir 'install-state.json'
if (Test-Path $statePath) {
    $old = Get-Content $statePath -Raw | ConvertFrom-Json
    $gitRootVarSet     = $gitRootVarSet -or [bool]$old.GitRootVariableSet
    $profileCreated    = $profileCreated -or [bool]$old.ProfileCreated
    $configFileCreated = $configFileCreated -or [bool]$old.ConfigFileCreated
    $psModulePathSet   = $psModulePathSet -or [bool]$old.PSModulePathSet
    $copilotFilesCreated = @(@($copilotFilesCreated) + @($old.CopilotFilesCreated | Where-Object { $_ }) | Select-Object -Unique)
}
[ordered]@{
    GitRootVariableSet  = $gitRootVarSet
    ProfileCreated      = $profileCreated
    ProfilePath         = $profilePath
    ConfigFileCreated   = $configFileCreated
    PSModulePathSet     = $psModulePathSet
    CopilotFilesCreated = @($copilotFilesCreated)
    BinFiles            = $binFiles
    InstalledFrom       = $setup
    InstalledAt         = (Get-Date).ToString('s')
} | ConvertTo-Json | Set-Content $statePath
Get-Content $statePath


# ===== Done ===============================================================================
# Restart VS Code and open new terminals. In a new window, this should print "launcher ok: 42":
#   awk 'BEGIN { printf "launcher ok: %d\n", 42 }'
# In Copilot, ask it to run:
#   (Get-Command grep, gfind, xargs | ForEach-Object { "$($_.Name)=$($_.Source)" }) -join ' '
# and expect: grep=GitTools gfind=GitTools xargs=GitTools