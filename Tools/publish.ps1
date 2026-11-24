# Publishes the command line (commitfence) as one file for one system:
# self-contained, one file, compiled ahead of time (ReadyToRun) and not compressed inside the file,
# invariant globalization, without Roslyn's translated-message folders.
#
#   Tools\publish.ps1 [-Runtime win-x64] [-Output <folder>]
#
# Runtime: win-x64 (default), win-arm64, linux-x64, linux-arm64, linux-musl-x64, osx-x64, osx-arm64.
# Output:  default artifacts\publish\<runtime> in the repository (ignored by git).
#
# What the published folder holds, and why:
#   commitfence(.exe)         the whole program in one file (the libraries and Roslyn are inside it)
#   analyzers\CF.Meaning.dll  our analyzer, beside the program and NOT inside it: the team's compiler loads it
#                             from disk, so it is handed to the build by path. It is found at
#                             AppContext.BaseDirectory\analyzers, which for a single-file program is the folder
#                             of the file. The plug-in project copies it beside the build; CF.Cli.csproj marks it
#                             ExcludeFromSingleFile, which keeps it out of the bundle and beside the file.
# The MSBuild file of ours that the build needs is written by the program itself into its data folder
# from text compiled into the program, so nothing else travels with it.
param(
    [string]$Runtime = 'win-x64',
    [string]$Output = ''
)

$ErrorActionPreference = 'Stop'

$repository = Split-Path -Parent $PSScriptRoot
if ($Output -eq '') {
    $Output = Join-Path $repository (Join-Path 'artifacts\publish' $Runtime)
}

$project = Join-Path $repository 'App\CF.Cli\CF.Cli.csproj'

$arguments = @(
    'publish', $project,
    '-c', 'Release',
    '-r', $Runtime,
    '--self-contained', 'true',
    '-o', $Output,
    '-p:PublishSingleFile=true',
    '-p:PublishReadyToRun=true',
    '-p:EnableCompressionInSingleFile=false',
    '-p:InvariantGlobalization=true',
    '-p:SatelliteResourceLanguages=en',
    '-p:PublishDocumentationFile=false',
    '-p:DebugType=none',
    '-p:DebugSymbols=false',
    '-p:AllowedReferenceRelatedFileExtensions=.none'
)

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Get-ChildItem -Path $Output -Recurse -File |
    Sort-Object FullName |
    ForEach-Object { '{0,12:N0}  {1}' -f $_.Length, $_.FullName.Substring($Output.Length + 1) }
