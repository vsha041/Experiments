# Git for Windows GNU tools as PowerShell commands.
# Master copy: module\GitTools.psm1 in the gitlaunch setup folder. install.ps1 copies it to
# <Documents>\PowerShell\Modules\GitTools; edit the master and re-run install.ps1.
#
# Auto-loads in any PowerShell 7 process (including `pwsh -NoProfile`, which Copilot CLI
# uses) the first time one of the names in GitTools.psd1 is used. install.ps1 also adds an
# Import-Module line to the profile so interactive windows have them up front.
#
# Each name is an alias for bin\<name>.exe, a hard link to (or copy of) gitlaunch.exe,
# built by install.ps1 from src\ in the setup folder. gitlaunch runs the real Git tool
# with every argument re-quoted, because Git's MSYS tools parse command lines differently
# from how PowerShell builds them (" inside arguments, don't, *.cs, {a,b} and ~ would
# otherwise be mangled). It also handles the tools that start other programs by name
# (xargs, gfind, env, gtimeout) by putting usr\bin on their PATH.
#
# Why aliases (not .cmd wrappers or functions):
#   - .cmd wrappers are re-parsed by cmd.exe, so | & > < ^ inside arguments break.
#   - PowerShell functions drop a bare `--` and buffer piped input.
#   - An alias to an exe passes arguments and pipes straight through.
#
# PowerShell quoting notes (these apply to any native exe):
#   - write '{}' and ';' quoted:  gfind . -name '*.cs' -exec grep -l foo '{}' ';'
#     (a bare {} is a PowerShell script block and a bare ; ends the command)
#
# Names already used by Windows or PowerShell (find, sort, timeout, diff, bash) keep
# their usual meaning; the GNU / Git Bash versions are the g-prefixed names.
# Deliberately not included: date (PowerShell runs Get-Date for it), and cat, ls, cp, mv,
# rm, tee, ps, kill, echo, pwd, sleep, mkdir (built-in PowerShell aliases/functions).

$bin = Join-Path $PSScriptRoot 'bin'
$manifest = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'GitTools.psd1')

if (-not (Test-Path (Join-Path $bin 'gitlaunch.exe'))) {
    Write-Warning "GitTools: $bin\gitlaunch.exe is missing. Run install.ps1 in the gitlaunch setup folder."
}

foreach ($name in $manifest.AliasesToExport) {
    $exe = Join-Path $bin "$name.exe"
    if (Test-Path $exe) {
        Set-Alias -Name $name -Value $exe   # find them later with: Get-Alias | Where-Object Source -eq GitTools
    }
}

Export-ModuleMember -Alias $manifest.AliasesToExport
