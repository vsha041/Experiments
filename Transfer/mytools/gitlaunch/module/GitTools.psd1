# Master copy: module\GitTools.psd1 in the gitlaunch setup folder. install.ps1 copies it to
# <Documents>\PowerShell\Modules\GitTools; edit the master and re-run install.ps1.
@{
    RootModule        = 'GitTools.psm1'
    ModuleVersion     = '1.0.0'
    GUID              = '3f6c2a91-8d4e-4b7a-b0e5-71c9d2f4a863'
    Description       = 'Git for Windows GNU tools (grep, sed, awk, gfind, ...) as PowerShell commands.'
    PowerShellVersion = '7.0'

    # This list is what lets PowerShell auto-load the module, even in `pwsh -NoProfile`
    # (e.g. Copilot CLI): the first time one of these names is used, the module is imported.
    # It is the one list to edit: GitTools.psm1 makes an alias for each name, and
    # install.ps1 creates bin\<name>.exe for each name (re-run it after editing). Which Git
    # exe a name runs is decided in src\Program.cs: usr\bin\<name>.exe unless listed there
    # (sh, gbash, gsort, gdiff, gfind, ...).
    FunctionsToExport = @()
    AliasesToExport   = @(
        # search / read
        'grep', 'sed', 'awk', 'gawk', 'head', 'tail', 'nl', 'tac', 'wc', 'xargs'
        # reshape output
        'uniq', 'cut', 'tr', 'paste', 'join', 'comm', 'column'
        # compare / patch
        'diff3', 'sdiff', 'cmp', 'patch'
        # files / paths
        'touch', 'ln', 'stat', 'du', 'df', 'realpath', 'readlink', 'basename', 'dirname',
        'mktemp', 'which', 'cygpath', 'file'
        # encoding / line endings / binary / checksums
        'dos2unix', 'unix2dos', 'iconv', 'xxd', 'od', 'base64', 'base32', 'basenc',
        'md5sum', 'sha1sum', 'sha256sum', 'sha512sum', 'b2sum', 'cksum'
        # archives (bzip2/bunzip2 are already on PATH via Git\ucrt64\bin)
        'gzip', 'unzip', 'zipinfo'
        # scripting / environment
        'perl', 'env', 'printenv', 'nproc', 'uname', 'seq', 'printf', 'expr',
        'split', 'csplit', 'fold', 'truncate', 'shuf', 'sh'
        # signatures
        'gpg'
        # GNU / Git Bash versions of names Windows or PowerShell already use
        'gfind', 'gsort', 'gtimeout', 'gdiff', 'gbash'
    )
    CmdletsToExport   = @()
    VariablesToExport = @()
}
