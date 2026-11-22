---
name: dotnet-decompile
description: Inspect .NET assembly implementations with ilspycmd only when the implementation is needed for the task and accessible source code is unavailable. Use for understanding NuGet package internals, internal classes, method behavior, or dependency registrations hidden inside compiled DLLs; do not activate for routine builds, dependency injection questions, or test design when available source is sufficient.
---

# .NET Assembly Decompilation

## Prerequisites

Use the locally installed `ilspycmd` command-line tool.

Verify availability with `ilspycmd --version`. If unavailable, notify the user before installing anything.

## Workflow

1. **Decide whether decompilation is needed.**
   - Prefer implementation source in the current repository or referenced projects.
   - Check for accessible original source through Source Link or a known source repository, when feasible. Do not assume online or private repository access.
   - If source is unavailable or insufficient and implementation details matter, use `ilspycmd`. Do not decompile a DLL merely because it is referenced.

2. **Identify the exact assembly and target.**
   - Determine the assembly name and, if known, the fully qualified type or member.
   - Identify the project's target framework (TFM), runtime identifier (RID, if relevant), and resolved NuGet package version.
   - Prefer existing `obj/project.assets.json` to identify resolved dependencies. Search it selectively; never dump the entire file into context. For example:

     `rg -n -C 4 'Package.Id/' obj/project.assets.json`

   - If `rg` is unavailable, use `Select-String` in PowerShell or `grep` in bash. Package-listing commands may restore implicitly on newer SDKs; for .NET 10+, use `dotnet package list --no-restore --include-transitive` when appropriate.

3. **Locate the implementation DLL without an unnecessary build.**

   **For a NuGet dependency:**
   - Find the global package cache using `dotnet nuget locals global-packages --list`; do not assume a fixed path. `NUGET_PACKAGES` can relocate it.
   - The usual managed implementation is under `<global-packages>/<package-id-lowercase>/<version>/lib/<tfm>/`.
   - Do not assume `lib/<tfm>/` is always correct. RID-specific managed implementations may live under `runtimes/<rid>/lib/<tfm>/`.
   - Inspect the applicable target's `runtime` and `runtimeTargets` assets in `project.assets.json`, the project's RID, and existing build/publish output to find the matching implementation. Do not choose a RID-specific candidate just because its file exists.

   **For an assembly built by this solution:**
   - Prefer an existing implementation DLL in the relevant project's `bin/<Configuration>/<tfm>/` (sometimes with an additional `<rid>/` segment).
   - If missing and a build is necessary, build **only the relevant project** using its explicit `.csproj` path. Prefer `dotnet build "path/to/Project.csproj" --no-restore --verbosity quiet --nologo` when assets have already been restored.
   - Avoid an entire solution build or implicit restore merely to locate a DLL. If required prerequisites are missing or the build fails, explain the limitation; use a cached package only if it matches the intended version and implementation.

4. **Exclude reference assemblies.**
   - Prefer runtime implementation assets, not `ref/<tfm>/` or build intermediates such as `obj/**/ref/` or `obj/**/refint/`.
   - `compile` assets may be reference assemblies; `runtime` assets generally contain implementations. Consider RID-specific assets as described above.
   - Placeholder-looking decompiled bodies (such as `throw null;`) are a reason to **verify** whether the assembly is a reference assembly, not definitive proof by themselves.

5. **Find the relevant type with filtered output.**

   `ilspycmd -l c "path-to-assembly.dll" | rg -i "keyword"`

   - If `rg` is unavailable, use `Select-String` (PowerShell) or `grep -i` (bash).
   - Type filters include `c` (class), `i` (interface), `s` (struct), `e` (enum), and `d` (delegate). Use only the types relevant to the question.
   - Do not print an unfiltered listing for a large assembly.

6. **Decompile only the type needed and keep output out of context by default.**
   - Write the output to a temporary file outside the repository, then search and read only relevant sections:

     `ilspycmd -t "Namespace.TypeName" "path-to-assembly.dll" > "<temp-dir>/TypeName.cs"`

     `rg -n -C 8 "MemberName" "<temp-dir>/TypeName.cs"`

   - Use `Select-String -Context 8,8` in PowerShell or `grep -n -C 8` in bash if `rg` is unavailable.
   - Nested types may use `Namespace.Outer+Inner`. Generic types require a backtick and arity; protect the name from shell interpretation with single quotes, e.g.:

     ``ilspycmd -t 'Namespace.Repository`1' "path-to-assembly.dll" > "<temp-dir>/Repository.cs"``

   - Read surrounding code only as needed to understand control flow and related calls. Do not paste a whole large type into the conversation.

7. **Resolve dependencies only when necessary.**
   - If a required dependency cannot be resolved, point ILSpy at its directory:

     `ilspycmd -r "<dir-with-dependencies>" -t "Namespace.TypeName" "path-to-assembly.dll" > "<temp-dir>/TypeName.cs"`

   - Inspect related types one at a time. Avoid recursively decompiling every dependency.

8. **Use whole-assembly decompilation only as a last resort.**
   - When targeted inspection is insufficient, decompile into a temporary directory **outside** the repository:

     `ilspycmd -p -o "<temp-dir>/decompiled" "path-to-assembly.dll"`

   - Search that directory first, and open only the files needed. Never read or paste the entire generated tree.

9. **Report findings concisely.**
   - Summarize relevant behavior, identify the assembly/package version inspected, and distinguish decompiled observations from assumptions.
   - Mention unresolved dependencies, missing bodies, uncertain runtime selection, or decompilation errors that limit confidence.

## Rules

- Do not invent the behavior of compiled dependencies or treat reconstructed code as original source.
- Prefer available source and targeted inspection; never decompile by default for every DLL reference.
- Keep potentially large `ilspycmd` output in temporary files; search before reading.
- Never print an entire assembly to the conversation; avoid dumping `project.assets.json` or unfiltered type listings.
- Do not modify original assemblies or commit generated code.
- Do not send proprietary assemblies, decompiled code, or diagnostics to external services without authorization.
- Avoid repeated decompilation of the same unchanged assembly when existing temporary output is sufficient.
- Report failures and evidence limitations rather than guessing.
