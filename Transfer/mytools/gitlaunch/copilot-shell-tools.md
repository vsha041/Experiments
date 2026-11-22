## Shell tools (Windows, PowerShell 7)

- GNU tools from Git for Windows work as PowerShell commands (GitTools module, auto-loads): grep, sed, awk, head, tail, wc, xargs, cut, tr, uniq, column, diff3, patch, file, xxd, od, dos2unix, iconv, sha256sum, which, cygpath, env, perl, sh and more.
- GNU versions of names that Windows/PowerShell already use: `gfind`, `gsort`, `gtimeout`, `gdiff`. `gbash` is Git Bash (plain `bash` starts WSL). Plain `find`, `sort`, `timeout`, `diff`, `cat`, `ls`, `rm` are the Windows/PowerShell versions.
- Wildcards are not expanded for these tools: `grep foo *.cs` fails. Use `grep -rn foo --include='*.cs' .` or `gfind . -name '*.cs'`.
- In `gfind -exec`, quote `'{}'` and `';'` (a bare `{}` is a PowerShell script block, a bare `;` ends the command).
- Text piped from PowerShell (Get-Content, string literals) reaches GNU tools with CRLF line endings, except into xargs. Prefer passing file names, or piping one GNU tool into another.
