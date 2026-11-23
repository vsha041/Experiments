#Requires -Version 7.0
<#
.SYNOPSIS
    Installs or removes GitTools: Git for Windows' GNU tools (grep, sed, awk, gfind, ...) as
    PowerShell commands, for your terminals and for Copilot CLI.

.DESCRIPTION
    Per-user only; no admin rights needed. Requires PowerShell 7, Git for Windows, and the
    .NET 10 SDK or newer (to build the launcher). Safe to re-run: it updates what is there.

    The module and your profile go under %LOCALAPPDATA%\PowerShell, which OneDrive doesn't
    sync. Only two small files stay in Documents\PowerShell: powershell.config.json (tells
    PowerShell where the modules are) and your all-hosts profile (loads the one in AppData).

    Install:
      1. Checks that the launcher can find Git. Only if Git isn't registered or in Program
         Files does it set the GITTOOLS_GIT_ROOT user environment variable.
      2. Sets PSModulePath in your powershell.config.json to %LOCALAPPDATA%\PowerShell\Modules,
         keeping the file's other settings, so every pwsh (Copilot's pwsh -NoProfile included)
         finds the module. Stops if PSModulePath already points somewhere else. Note that
         Documents\PowerShell\Modules is then no longer searched, and Install-Module still
         installs there.
      3. Builds gitlaunch.exe from src\ with `dotnet publish`.
      4. Copies module\GitTools.psd1/.psm1 and gitlaunch.exe to
         %LOCALAPPDATA%\PowerShell\Modules\GitTools, and creates bin\<name>.exe for every name
         in GitTools.psd1 (hard links, or plain copies if that folder is in OneDrive).
      5. Tests the installed tools, including that a new pwsh -NoProfile finds them.
      6. Adds an Import-Module block to %LOCALAPPDATA%\PowerShell\profile.ps1, and a block that
         loads that file to your Documents profile (all hosts), unless it already does.
      7. Adds the Shell tools section (copilot-shell-tools.md) to
         ~\.copilot\copilot-instructions.md, and to ~\.copilot\instructions\copilot-instructions.md
         if that file exists.
    It reports execution-policy problems but never changes the policy.

    -Uninstall removes the module folder, the Import-Module block (from both profiles), the
    PSModulePath entry if this script set it, the Copilot section, and GITTOOLS_GIT_ROOT if this
    script set it. The block in your Documents profile that loads the AppData profile stays,
    since your own profile content depends on it. The setup folder itself is left as it is.

    Copied this folder from another computer by download, email or chat? Unblock it first,
    or PowerShell refuses to run it:
        Get-ChildItem <setup folder> -Recurse | Unblock-File

.PARAMETER Uninstall
    Remove everything the install added.
.PARAMETER ModuleDir
    Where the module goes; the folder above it becomes your user module folder. Leave the
    default (%LOCALAPPDATA%\PowerShell\Modules\GitTools); it can be overridden for testing.
.PARAMETER ProfilePath
    The profile that gets the Import-Module block. Leave the default
    (%LOCALAPPDATA%\PowerShell\profile.ps1).
.PARAMETER LoaderPath
    The profile PowerShell itself runs, which gets the block that loads ProfilePath. Leave the
    default (all hosts profile in Documents\PowerShell).
.PARAMETER ConfigPath
    Your user powershell.config.json, which gets the PSModulePath entry. Leave the default.
.PARAMETER CopilotDir
    Copilot CLI's settings folder. Leave the default (~\.copilot).

.EXAMPLE
    pwsh -File .\install.ps1
.EXAMPLE
    pwsh -File .\install.ps1 -Uninstall
#>
[CmdletBinding()]
param(
    [switch]$Uninstall,
    [string]$ModuleDir   = (Join-Path $env:LOCALAPPDATA 'PowerShell\Modules\GitTools'),
    [string]$ProfilePath = (Join-Path $env:LOCALAPPDATA 'PowerShell\profile.ps1'),
    [string]$LoaderPath  = $PROFILE.CurrentUserAllHosts,
    [string]$ConfigPath  = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'PowerShell\powershell.config.json'),
    [string]$CopilotDir  = (Join-Path $HOME '.copilot')
)

$ErrorActionPreference = 'Stop'

$ProfileBegin    = '# >>> GitTools (managed by gitlaunch\install.ps1) >>>'
$ProfileEnd      = '# <<< GitTools <<<'
$LoaderBegin     = '# >>> Load the profile in AppData (added by gitlaunch\install.ps1) >>>'
$LoaderEnd       = '# <<< Load the profile in AppData <<<'
$LoaderPattern   = '(LOCALAPPDATA|AppData\\Local)[^\r\n]*profile\.ps1'
$CopilotBegin    = '<!-- GitTools: begin (managed by gitlaunch\install.ps1; edit copilot-shell-tools.md there) -->'
$CopilotEnd      = '<!-- GitTools: end -->'
$CopilotHeading  = '## Shell tools (Windows, PowerShell 7)'
$ImportPattern   = '^\s*Import-Module\s+GitTools\b'
$ModulesRoot     = Split-Path -Parent $ModuleDir
$LegacyModuleDir = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'PowerShell\Modules\GitTools'
$StatePath       = Join-Path $ModuleDir 'install-state.json'
$CopilotFiles    = @(
    @{ Path = (Join-Path $CopilotDir 'copilot-instructions.md');              Create = $true }
    @{ Path = (Join-Path $CopilotDir 'instructions\copilot-instructions.md'); Create = $false }
)

# ---------------------------------------------------------------------------------------
# Helpers

function Write-Step([string]$Text) { Write-Host "==> $Text" -ForegroundColor Cyan }
function Write-Info([string]$Text) { Write-Host "    $Text" }

# Text files are edited line by line, keeping their BOM and newline style.
function Read-TextFile([string]$Path) {
    $file = [pscustomobject]@{ Exists = $false; Lines = @(); Bom = $false; NewLine = "`r`n" }
    if (-not (Test-Path -LiteralPath $Path)) { return $file }
    $bytes = [IO.File]::ReadAllBytes($Path)
    $file.Exists = $true
    $file.Bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $skip = 0
    if ($file.Bom) { $skip = 3 }
    $text = [Text.Encoding]::UTF8.GetString($bytes, $skip, $bytes.Length - $skip)
    if ($text.Contains("`n") -and -not $text.Contains("`r`n")) { $file.NewLine = "`n" }
    # Drop the empty string after the final newline.
    $file.Lines = @($text -split '\r?\n' | Select-Object -SkipLast 1)
    if (-not $text.EndsWith("`n")) { $file.Lines = @($text -split '\r?\n') }
    $file
}

function Write-TextFile($File, [string]$Path, [string[]]$Lines) {
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
    $text = (@($Lines) -join $File.NewLine) + $File.NewLine
    [IO.File]::WriteAllText($Path, $text, [Text.UTF8Encoding]::new($File.Bom))
}

# Drops the lines from $Begin to $End, both included.
function Remove-MarkedBlock([string[]]$Lines, [string]$Begin, [string]$End) {
    $inside = $false
    foreach ($line in @($Lines)) {
        if (-not $inside -and $line.Trim() -eq $Begin) { $inside = $true; continue }
        if ($inside) {
            if ($line.Trim() -eq $End) { $inside = $false }
            continue
        }
        $line
    }
}

# Drops a section that starts at $Heading and runs to the next # or ## heading, or the end.
# (Handles a Shell tools section that was pasted in by hand, without the markers.)
function Remove-Section([string[]]$Lines, [string]$Heading) {
    $skipping = $false
    foreach ($line in @($Lines)) {
        if ($line.Trim() -eq $Heading) { $skipping = $true; continue }
        if ($skipping -and $line -match '^#{1,2} ') { $skipping = $false }
        if (-not $skipping) { $line }
    }
}

function Remove-TrailingBlankLines([string[]]$Lines) {
    $list = [Collections.Generic.List[string]]::new()
    foreach ($line in @($Lines)) { $list.Add($line) }
    while ($list.Count -gt 0 -and $list[$list.Count - 1].Trim() -eq '') { $list.RemoveAt($list.Count - 1) }
    $list.ToArray()
}

function Test-SameLines([string[]]$A, [string[]]$B) {
    (@(Remove-TrailingBlankLines $A) -join "`n") -ceq (@(Remove-TrailingBlankLines $B) -join "`n")
}

# Compares two folder paths, ignoring case, a trailing \ and %VARIABLES%.
function Test-SamePath([string]$A, [string]$B) {
    [Environment]::ExpandEnvironmentVariables($A).TrimEnd('\') -eq [Environment]::ExpandEnvironmentVariables($B).TrimEnd('\')
}

# What the install recorded, so -Uninstall only undoes what it did.
function Read-State {
    $state = [pscustomobject]@{
        GitRootVariableSet = $false; ProfileCreated = $false; ProfilePath = ''
        ConfigFileCreated = $false; PSModulePathSet = $false
        CopilotFilesCreated = @(); BinFiles = ''; InstalledFrom = ''; InstalledAt = ''
    }
    if (Test-Path -LiteralPath $StatePath) {
        $saved = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
        if ($saved.GitRootVariableSet) { $state.GitRootVariableSet = $true }
        if ($saved.ProfileCreated) { $state.ProfileCreated = $true }
        if ($saved.ProfilePath) { $state.ProfilePath = "$($saved.ProfilePath)" }
        if ($saved.ConfigFileCreated) { $state.ConfigFileCreated = $true }
        if ($saved.PSModulePathSet) { $state.PSModulePathSet = $true }
        if ($saved.CopilotFilesCreated) { $state.CopilotFilesCreated = @($saved.CopilotFilesCreated) }
    }
    $state
}

function Save-State($State) {
    if (-not (Test-Path -LiteralPath $ModuleDir)) { New-Item -ItemType Directory -Force $ModuleDir | Out-Null }
    $State | ConvertTo-Json | Set-Content -LiteralPath $StatePath
}

# Reads powershell.config.json as an ordered dictionary (empty if the file is missing or empty).
function Read-Config {
    $file = Read-TextFile $ConfigPath
    $raw = $file.Lines -join "`n"
    $config = [ordered]@{}
    if ($raw.Trim()) {
        try { $config = $raw | ConvertFrom-Json -AsHashtable }
        catch { throw "Couldn't read $ConfigPath ($($_.Exception.Message)). Fix the JSON by hand and run install.ps1 again." }
        if ($config -isnot [Collections.IDictionary]) { throw "$ConfigPath doesn't contain a JSON object. Fix it by hand and run install.ps1 again." }
    }
    [pscustomobject]@{ File = $file; Config = $config }
}

function Write-Config($Read) {
    Write-TextFile $Read.File $ConfigPath @(($Read.Config | ConvertTo-Json -Depth 10) -split '\r?\n')
}

function Test-GitRoot([string]$Root) {
    [bool]($Root -and (Test-Path -LiteralPath (Join-Path $Root 'usr\bin\grep.exe')))
}

# The same order gitlaunch uses: GITTOOLS_GIT_ROOT, the installer's registry entry, Program Files\Git.
function Get-LauncherGitRoot {
    if ($env:GITTOOLS_GIT_ROOT) { return $env:GITTOOLS_GIT_ROOT }
    foreach ($hive in 'HKLM:', 'HKCU:') {
        $key = Get-ItemProperty -Path "$hive\SOFTWARE\GitForWindows" -ErrorAction SilentlyContinue
        if ($key -and $key.InstallPath -and (Test-Path -LiteralPath $key.InstallPath)) { return $key.InstallPath }
    }
    Join-Path $env:ProgramFiles 'Git'
}

# Looks for the Git for Windows folder above git.exe on PATH (e.g. <Git>\cmd\git.exe).
function Find-GitRootFromPath {
    foreach ($git in @(Get-Command git -All -CommandType Application -ErrorAction SilentlyContinue)) {
        $dir = Split-Path -Parent $git.Source
        while ($dir) {
            if (Test-GitRoot $dir) { return $dir }
            $dir = Split-Path -Parent $dir
        }
    }
}

# ---------------------------------------------------------------------------------------
# Install steps

function Test-ExecutionPolicy {
    # What a new PowerShell process (such as Copilot's) gets; this window's own
    # -ExecutionPolicy setting doesn't count.
    $policy = $null
    $scope = $null
    foreach ($s in 'MachinePolicy', 'UserPolicy', 'CurrentUser', 'LocalMachine') {
        $p = Get-ExecutionPolicy -Scope $s
        if ($p -ne 'Undefined') { $policy = "$p"; $scope = $s; break }
    }
    if (-not $policy) { $policy = 'RemoteSigned'; $scope = 'PowerShell 7 default' }

    if ($policy -in 'Restricted', 'AllSigned') {
        $why = "Execution policy is $policy (${scope}), so PowerShell won't load the GitTools module (it isn't signed)."
        if ($scope -in 'MachinePolicy', 'UserPolicy') {
            Write-Warning "$why It is set by your IT department (Group Policy), so only they can change it."
        }
        else {
            Write-Warning "$why To allow your own scripts, run:  Set-ExecutionPolicy -Scope CurrentUser RemoteSigned"
        }
    }
    else {
        Write-Info "Execution policy: $policy (${scope}) - OK"
    }
}

function Confirm-GitRoot($State) {
    Write-Step 'Checking that the launcher can find Git'
    # A user variable set since this window opened isn't in $env: yet.
    $userValue = [Environment]::GetEnvironmentVariable('GITTOOLS_GIT_ROOT', 'User')
    if ($userValue -and -not $env:GITTOOLS_GIT_ROOT) { $env:GITTOOLS_GIT_ROOT = $userValue }

    $root = Get-LauncherGitRoot
    if (Test-GitRoot $root) {
        Write-Info "Git for Windows: $root"
        return
    }

    $found = Find-GitRootFromPath
    if (-not $found) {
        throw "Git for Windows not found (looked in '$root' and above git.exe on PATH). Install Git for Windows, or set the GITTOOLS_GIT_ROOT user environment variable to the folder that contains usr\bin."
    }
    [Environment]::SetEnvironmentVariable('GITTOOLS_GIT_ROOT', $found, 'User')
    $env:GITTOOLS_GIT_ROOT = $found
    $State.GitRootVariableSet = $true
    Save-State $State
    Write-Info "Git for Windows: $found"
    Write-Info "The launcher wouldn't find it on its own (looked in '$root'), so the user variable GITTOOLS_GIT_ROOT now points to it."
    Write-Info 'Restart VS Code and open new terminals so they see it.'
}

# PowerShell 7 looks for your own modules only in Documents\PowerShell\Modules unless the user
# powershell.config.json says otherwise. Done before anything is built or copied, so a
# conflicting entry stops the install early.
function Set-ModulePathConfig($State) {
    Write-Step "Pointing PowerShell at $ModulesRoot"
    $read = Read-Config
    $current = $read.Config['PSModulePath']
    if ($current -and -not (Test-SamePath $current $ModulesRoot)) {
        throw "$ConfigPath already sets PSModulePath to '$current', so PowerShell wouldn't find the module in $ModulesRoot. Change that entry to $ModulesRoot by hand (or remove it) and run install.ps1 again."
    }
    if ($current) {
        Write-Info "Already set in $ConfigPath - nothing to change."
        return
    }
    $read.Config['PSModulePath'] = $ModulesRoot
    Write-Config $read
    $State.PSModulePathSet = $true
    if (-not $read.File.Exists) { $State.ConfigFileCreated = $true; Write-Info "Created $ConfigPath" }
    else { Write-Info "Added PSModulePath to $ConfigPath; its other settings are kept." }
    Save-State $State
    Write-Info 'Documents\PowerShell\Modules is no longer searched, and Install-Module still installs there.'
}

function Build-Launcher {
    Write-Step 'Building gitlaunch.exe'
    $sdks = @()
    if (Get-Command dotnet -ErrorAction SilentlyContinue) { $sdks = @(dotnet --list-sdks) }
    $usable = @($sdks | Where-Object { $_ -match '^(\d+)\.' -and [int]$Matches[1] -ge 10 })
    if (-not $usable) {
        $found = 'no .NET SDK'
        if ($sdks) { $found = $sdks -join '; ' }
        throw "Building gitlaunch.exe needs the .NET 10 SDK or newer. Found: $found."
    }

    $rid = 'win-x64'
    if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { $rid = 'win-arm64' }
    $publishDir = Join-Path $PSScriptRoot "src\publish\$rid"
    $log = & dotnet publish (Join-Path $PSScriptRoot 'src\gitlaunch.csproj') -c Release -r $rid -o $publishDir --nologo 2>&1
    if ($LASTEXITCODE -ne 0) {
        $log | ForEach-Object { Write-Host "    $_" }
        throw "dotnet publish failed (exit code $LASTEXITCODE)."
    }
    $exe = Join-Path $publishDir 'gitlaunch.exe'
    Write-Info "Built $exe (SDK $($usable[-1]))"
    $exe
}

# Returns 'hardlink' or 'copy'.
function Install-GitToolsModule([string]$LauncherExe) {
    Write-Step "Installing the module in $ModuleDir"
    $bin = Join-Path $ModuleDir 'bin'
    New-Item -ItemType Directory -Force $bin | Out-Null
    try {
        Get-ChildItem -LiteralPath $bin -Filter '*.exe' | Remove-Item -Force
    }
    catch {
        throw "Couldn't replace the files in ${bin}: one of the tools is probably still running (in a terminal or a Copilot session). Close it and run install.ps1 again. ($($_.Exception.Message))"
    }

    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'module\GitTools.psd1'), (Join-Path $PSScriptRoot 'module\GitTools.psm1') -Destination $ModuleDir -Force
    $launcher = Join-Path $bin 'gitlaunch.exe'
    Copy-Item -LiteralPath $LauncherExe -Destination $launcher

    # OneDrive syncs plain files; hard links are left out to be safe.
    $inOneDrive = @($env:OneDrive, $env:OneDriveCommercial, $env:OneDriveConsumer |
        Where-Object { $_ -and $ModuleDir.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0

    $names = (Import-PowerShellDataFile (Join-Path $ModuleDir 'GitTools.psd1')).AliasesToExport
    foreach ($name in $names) {
        $path = Join-Path $bin "$name.exe"
        if ($inOneDrive) { Copy-Item -LiteralPath $launcher -Destination $path }
        else { New-Item -ItemType HardLink -Path $path -Target $launcher | Out-Null }
    }

    # Files that came by download or email carry a "from the internet" mark that makes
    # PowerShell refuse the module. These are your own files, so clear it.
    Get-ChildItem -LiteralPath $ModuleDir -Recurse -File | Unblock-File

    if ($inOneDrive) {
        Write-Info "The module folder is in OneDrive, so bin\ holds $($names.Count) plain copies of gitlaunch.exe (no hard links)."
        'copy'
    }
    else {
        Write-Info "Installed bin\gitlaunch.exe and $($names.Count) tool links."
        'hardlink'
    }
}

function Test-Installed {
    Write-Step 'Testing the installed tools'
    $printf = Join-Path $ModuleDir 'bin\printf.exe'
    $expected = '[a "b" c|don''t|*.cs]'
    try {
        $out = (& $printf '[%s|%s|%s]' 'a "b" c' "don't" '*.cs' 2>&1) -join "`n"
    }
    catch {
        throw "Couldn't start $printf - $($_.Exception.Message) If Windows says it is blocked by group policy, your company doesn't allow programs from your user folder; that needs your IT department."
    }
    if ($LASTEXITCODE -ne 0 -or $out -cne $expected) {
        throw "Test failed (exit code $LASTEXITCODE). Expected $expected but got: $out"
    }
    Write-Info "printf passed  a ""b"" c  /  don't  /  *.cs  through unchanged - OK"

    # A new PowerShell without a profile, the way Copilot runs commands, must find the module itself.
    $source = pwsh -NoProfile -NonInteractive -Command { (Get-Command grep -ErrorAction SilentlyContinue).Source }
    if ($source -ne 'GitTools') {
        throw "A new pwsh -NoProfile doesn't find GitTools (grep came from '$source'). Check that PSModulePath in $ConfigPath is $ModulesRoot."
    }
    Write-Info 'A new pwsh -NoProfile finds grep in GitTools - OK'
}

function Set-ProfileBlock($State) {
    Write-Step "Adding the Import-Module block to $ProfilePath"
    $file = Read-TextFile $ProfilePath
    $hadBlock = @($file.Lines | Where-Object { $_.Trim() -eq $ProfileBegin }).Count -gt 0

    $lines = @(Remove-MarkedBlock $file.Lines $ProfileBegin $ProfileEnd)
    $lines = @($lines | Where-Object { $_ -notmatch $ImportPattern })
    $lines = @(Remove-TrailingBlankLines $lines)
    if ($lines.Count) { $lines += '' }
    $lines += @(
        $ProfileBegin
        '# Git for Windows GNU tools (grep, sed, awk, gfind, ...). The module also auto-loads without'
        "# a profile (e.g. Copilot CLI's pwsh -NoProfile); this loads it up front in interactive windows."
        'Import-Module GitTools -ErrorAction SilentlyContinue'
        $ProfileEnd
    )
    Write-TextFile $file $ProfilePath $lines
    if (-not $file.Exists) { $State.ProfileCreated = $true; Write-Info 'Created the profile.' }
    elseif ($hadBlock) { Write-Info 'Updated the existing block.' }
    else { Write-Info 'Added.' }
}

# PowerShell only runs the profile in Documents; it needs a block that loads $ProfilePath.
function Set-ProfileLoader {
    if (Test-SamePath $LoaderPath $ProfilePath) { return }
    Write-Step "Checking that $LoaderPath loads $ProfilePath"
    $file = Read-TextFile $LoaderPath
    $loads = @($file.Lines | Where-Object {
            $_ -match $LoaderPattern -or $_.IndexOf($ProfilePath, [StringComparison]::OrdinalIgnoreCase) -ge 0 }).Count -gt 0
    if ($loads) { Write-Info 'It does - nothing to add.' }
    else {
        $portable = ($ProfilePath -ireplace ('^' + [regex]::Escape($env:LOCALAPPDATA)), '%LOCALAPPDATA%').Replace("'", "''")
        $lines = @(Remove-TrailingBlankLines $file.Lines)
        if ($lines.Count) { $lines += '' }
        $lines += @(
            $LoaderBegin
            '# Your profile lives outside OneDrive; this loads it. install.ps1 -Uninstall leaves this in place.'
            "`$localProfile = [Environment]::ExpandEnvironmentVariables('$portable')"
            'if (Test-Path $localProfile) { . $localProfile }'
            $LoaderEnd
        )
        Write-TextFile $file $LoaderPath $lines
        Write-Info 'Added the block that loads it.'
    }
    if (@($file.Lines | Where-Object { $_ -match $ImportPattern }).Count) {
        Write-Warning "$LoaderPath still has an Import-Module GitTools line from an earlier install. It's harmless; install.ps1 -Uninstall removes it."
    }
}

function Set-CopilotSection($State) {
    $section = @(Remove-TrailingBlankLines @(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'copilot-shell-tools.md')))
    foreach ($target in $CopilotFiles) {
        $path = $target.Path
        $file = Read-TextFile $path
        if (-not $file.Exists -and -not $target.Create) { continue }

        Write-Step "Adding the Shell tools section to $path"
        $lines = @(Remove-MarkedBlock $file.Lines $CopilotBegin $CopilotEnd)
        $lines = @(Remove-Section $lines $CopilotHeading)
        $lines = @(Remove-TrailingBlankLines $lines)
        if ($lines.Count) { $lines += '' }
        $lines += @($CopilotBegin) + $section + @($CopilotEnd)
        Write-TextFile $file $path $lines

        if (-not $file.Exists) {
            if ($State.CopilotFilesCreated -notcontains $path) { $State.CopilotFilesCreated += $path }
            Write-Info 'Created the file.'
        }
        else { Write-Info 'Done.' }
    }
}

# ---------------------------------------------------------------------------------------
# Uninstall

function Remove-ProfileBlock([string]$Path, [bool]$MayDelete) {
    Write-Step "Removing the Import-Module block from $Path"
    $file = Read-TextFile $Path
    $lines = @(Remove-MarkedBlock $file.Lines $ProfileBegin $ProfileEnd)
    $lines = @($lines | Where-Object { $_ -notmatch $ImportPattern })
    $lines = @(Remove-TrailingBlankLines $lines)
    if (-not $file.Exists -or (Test-SameLines $file.Lines $lines)) { Write-Info 'Nothing to remove.' }
    elseif ($lines.Count -eq 0 -and $MayDelete) {
        Remove-Item -LiteralPath $Path
        Write-Info 'Removed the profile (the install had created it).'
    }
    else {
        Write-TextFile $file $Path $lines
        Write-Info 'Removed.'
    }
}

function Remove-ModulePathConfig($State) {
    Write-Step "Checking PSModulePath in $ConfigPath"
    if (-not $State.PSModulePathSet) { Write-Info "Left as it is: the install didn't set it."; return }
    try { $read = Read-Config }
    catch { Write-Warning $_.Exception.Message; return }
    $current = $read.Config['PSModulePath']
    if (-not $current) { Write-Info 'Not set.'; return }
    if (-not (Test-SamePath $current $ModulesRoot)) { Write-Info "Left it as it is ($current): it no longer points to $ModulesRoot."; return }
    $read.Config.Remove('PSModulePath')
    if ($read.Config.Count -eq 0 -and $State.ConfigFileCreated) {
        Remove-Item -LiteralPath $ConfigPath
        Write-Info 'Removed the file (the install had created it).'
    }
    else {
        Write-Config $read
        Write-Info 'Removed PSModulePath; the other settings are kept.'
    }
}

function Uninstall-GitTools {
    $state = Read-State

    Write-Step "Removing $ModuleDir"
    if (Test-Path -LiteralPath $ModuleDir) {
        try { Remove-Item -LiteralPath $ModuleDir -Recurse -Force }
        catch {
            throw "Couldn't remove ${ModuleDir}: one of the tools is probably still running (in a terminal or a Copilot session). Close it and run install.ps1 -Uninstall again. ($($_.Exception.Message))"
        }
        Write-Info 'Removed.'
    }
    else { Write-Info 'Not installed.' }

    # The AppData profile, the one recorded at install time if different, and the Documents
    # profile (where an earlier install put the block). The loader block stays.
    Remove-ProfileBlock $ProfilePath $state.ProfileCreated
    if ($state.ProfilePath -and -not (Test-SamePath $state.ProfilePath $ProfilePath)) { Remove-ProfileBlock $state.ProfilePath $false }
    if (-not (Test-SamePath $LoaderPath $ProfilePath)) {
        Remove-ProfileBlock $LoaderPath $false
        Write-Info "The block that loads $ProfilePath stays: your own profile content depends on it."
    }

    Remove-ModulePathConfig $state

    foreach ($target in $CopilotFiles) {
        $path = $target.Path
        $file = Read-TextFile $path
        if (-not $file.Exists) { continue }
        Write-Step "Removing the Shell tools section from $path"
        $lines = @(Remove-MarkedBlock $file.Lines $CopilotBegin $CopilotEnd)
        $lines = @(Remove-Section $lines $CopilotHeading)
        $lines = @(Remove-TrailingBlankLines $lines)
        if (Test-SameLines $file.Lines $lines) { Write-Info 'Nothing to remove.'; continue }
        if ($lines.Count -eq 0 -and $state.CopilotFilesCreated -contains $path) {
            Remove-Item -LiteralPath $path
            Write-Info 'Removed the file (the install had created it).'
        }
        else {
            Write-TextFile $file $path $lines
            Write-Info 'Removed.'
        }
    }

    Write-Step 'Checking GITTOOLS_GIT_ROOT'
    $userValue = [Environment]::GetEnvironmentVariable('GITTOOLS_GIT_ROOT', 'User')
    if ($state.GitRootVariableSet -and $userValue) {
        [Environment]::SetEnvironmentVariable('GITTOOLS_GIT_ROOT', $null, 'User')
        Remove-Item Env:GITTOOLS_GIT_ROOT -ErrorAction SilentlyContinue
        Write-Info 'Removed the user variable (the install had set it).'
    }
    elseif ($userValue) { Write-Info "Left it as it is ($userValue): the install didn't set it." }
    else { Write-Info 'Not set.' }

    if ((Test-Path -LiteralPath $LegacyModuleDir) -and -not (Test-SamePath $LegacyModuleDir $ModuleDir)) {
        Write-Warning "An older GitTools copy is still in $LegacyModuleDir. Delete that folder, or PowerShell may load it from there."
    }
}

# ---------------------------------------------------------------------------------------
# Main

if (-not $IsWindows) { throw 'GitTools is for Windows only.' }

if ($Uninstall) {
    Uninstall-GitTools
    Write-Host "`nGitTools removed. Open new terminals and restart VS Code." -ForegroundColor Green
    return
}

Write-Step "Installing GitTools from $PSScriptRoot"
Write-Info "PowerShell $($PSVersionTable.PSVersion)"
Test-ExecutionPolicy

$state = Read-State
Confirm-GitRoot $state
Set-ModulePathConfig $state
$launcherExe = Build-Launcher
$state.BinFiles = Install-GitToolsModule $launcherExe
Save-State $state
Test-Installed
Set-ProfileBlock $state
Set-ProfileLoader
Set-CopilotSection $state
$state.ProfilePath = $ProfilePath
$state.InstalledFrom = $PSScriptRoot
$state.InstalledAt = (Get-Date).ToString('s')
Save-State $state

if ((Test-Path -LiteralPath $LegacyModuleDir) -and -not (Test-SamePath $LegacyModuleDir $ModuleDir)) {
    Write-Warning "An older GitTools copy is still in $LegacyModuleDir. PowerShell no longer looks there; you can delete that folder."
}

Write-Host ''
Write-Host 'GitTools installed. Open a new PowerShell 7 window (and restart VS Code for Copilot), then try:' -ForegroundColor Green
Write-Host '    awk ''BEGIN { printf "launcher ok: %d\n", 42 }'''