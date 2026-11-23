
# Global Agent Instructions

## Clarification-First Workflow for software / programming / coding questions and tasks

- For implementation, architecture, debugging, testing, refactoring,
  and other complex tasks, always begin with a clarification phase
  before proposing solutions, generating code, or modifying files.

- For simple factual or informational questions, answer directly
  unless the question is ambiguous.

- Identify ambiguities, assumptions, constraints, and possible
  interpretations of my request.

- Ask focused and relevant questions about:
  - Requirements and expected behaviour
  - Existing implementation and architecture
  - Dependencies and technical constraints
  - Edge cases and error handling
  - Preferred approaches when multiple options exist

- Do not assume missing requirements or invent implementation details.

- When multiple approaches are possible, briefly explain the
  important trade-offs and ask which approach I prefer.

- Do not ask questions already answered in the conversation or
  available from the codebase or provided context.

- If a complex task seems completely clear, briefly confirm your
  understanding and ask whether I have additional constraints
  before implementing.

- Wait for my answers before proceeding with implementation.

- After clarification, proceed without unnecessarily repeating
  questions or requesting confirmation for already agreed decisions.

- If I explicitly tell you to proceed without questions, do so.

## Clarification Format

- When asking clarification questions, use the interactive
  multiple-choice question UI (e.g. the Ask User Question tool) whenever
  it is available, instead of listing questions in plain text.
- Put the recommended option first and mark it "(Recommended)".
- Ask the most important ones first and follow up with the rest after I answer.
- If the interactive UI isn't available, fall back to a short numbered list.

## Software / Programming / Coding Guidelines

- Understand the existing codebase before proposing changes.
- Prefer minimal changes consistent with existing architecture.
- Do not modify files until the clarification phase is complete.
- Distinguish confirmed facts from assumptions.
- Prefer correctness and maintainability over unnecessary complexity.

## Inspecting .NET NuGet Package Implementations

Whenever a task requires understanding the implementation of a class, method, interface, or service provided by a compiled .NET assembly or NuGet package:

- Do not assume the internal implementation of external dependencies.
- Check whether the source code is available locally.
- If source code is unavailable, use the locally installed `ilspycmd` command-line tool to inspect the assembly.
- Locate the correct DLL using the project's build output, NuGet package cache, or `obj/project.assets.json`.
- If build output folders don't exist then build the whole project or solution.
- If it is not possible to get the build working then get that specific version DLL from here - %USERPROFILE%\.nuget\packages 
- Verify that the assembly version and target framework match those used by the project.
- Prefer implementation assemblies rather than reference assemblies.
- Use `ilspycmd -l c` to discover classes when necessary.
- Use `ilspycmd -t "Fully.Qualified.TypeName" "PathToDll"` to decompile specific types.
- Prefer targeted decompilation instead of decompiling an entire assembly.
- If full assembly decompilation is necessary, write the generated files to a temporary directory just outside the repository.
- Never modify the original DLL or commit decompiled source code.
- Base implementation decisions, debugging recommendations and test designs on the inspected code rather than assumptions.
- If decompilation fails, explicitly report the failure and any remaining uncertainty.

<!-- GitTools: begin (managed by gitlaunch\install.ps1; edit copilot-shell-tools.md there) -->
## Shell tools (Windows, PowerShell 7)

- GNU tools from Git for Windows work as PowerShell commands (GitTools module, auto-loads): grep, sed, awk, head, tail, wc, xargs, cut, tr, uniq, column, diff3, patch, file, xxd, od, dos2unix, iconv, sha256sum, which, cygpath, env, perl, sh and more.
- GNU versions of names that Windows/PowerShell already use: `gfind`, `gsort`, `gtimeout`, `gdiff`. `gbash` is Git Bash (plain `bash` starts WSL). Plain `find`, `sort`, `timeout`, `diff`, `cat`, `ls`, `rm` are the Windows/PowerShell versions.
- Wildcards are not expanded for these tools: `grep foo *.cs` fails. Use `grep -rn foo --include='*.cs' .` or `gfind . -name '*.cs'`.
- In `gfind -exec`, quote `'{}'` and `';'` (a bare `{}` is a PowerShell script block, a bare `;` ends the command).
- Text piped from PowerShell (Get-Content, string literals) reaches GNU tools with CRLF line endings, except into xargs. Prefer passing file names, or piping one GNU tool into another.
<!-- GitTools: end -->

