---
name: dotnet-decompile
description: Inspect compiled .NET assemblies and NuGet package implementations using ILSpy. Use when source code is unavailable, when investigating external or internal NuGet packages, private or internal classes, dependency injection registrations, debugging dependencies, or designing tests involving compiled dependencies.
---

# .NET Assembly Decompilation

## Prerequisites

Use the locally installed `ilspycmd` command-line tool.

Verify availability using:

`ilspycmd --version`

If unavailable, notify the user before installing anything.

## Workflow

1. Identify the assembly and fully qualified type name.
2. Determine the package version and target framework used by the project.
3. Locate the implementation DLL in:
   - Project build output.
   - Local NuGet package cache.
   - Paths resolved from `obj/project.assets.json`.
4. If build output folders don't exist then build the whole project or solution to generated the DLL files.
5. If it is not possible to get the build working then get that specific version DLL from here - %USERPROFILE%\.nuget\packages 
6. Avoid reference-only assemblies. Use build output assemblies instead.
7. List available types if required:

   `ilspycmd -l c "path-to-assembly.dll"`

8. Decompile the relevant type:

   `ilspycmd -t "Namespace.TypeName" "path-to-assembly.dll"`

9. Inspect related types if additional implementation details are required.
10. If necessary, decompile the assembly into a temporary project outside the repository.
11. Use the decompiled code to answer the user's question or inform implementation decisions.

## Rules

- Do not guess the implementation of compiled dependencies.
- Prefer targeted decompilation to reduce context consumption.
- Do not modify the original assembly.
- Do not commit generated source files.
- Treat decompiled code as reconstructed code, not original source code.
- Distinguish verified implementation details from assumptions.
- Report any decompilation failures or missing dependencies.
