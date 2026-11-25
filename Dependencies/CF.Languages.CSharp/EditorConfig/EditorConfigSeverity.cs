using CF.Core.Ports;
using CF.Languages.CSharp.Toolchain;
using Microsoft.CodeAnalysis;

namespace CF.Languages.CSharp.EditorConfig;

/// <summary>Looks up the severity a diagnostic's source file gets from the repository's editor configuration.</summary>
internal sealed class EditorConfigSeverity(string treeFolder, IFileSystem files)
{
    private readonly Dictionary<string, AnalyzerConfig?> _files = new Dictionary<string, AnalyzerConfig?>(ToolchainPaths.Comparer);
    private readonly Dictionary<string, AnalyzerConfigSet> _sets = new Dictionary<string, AnalyzerConfigSet>(ToolchainPaths.Comparer);

    public string? Read(string? path, string ruleId)
    {
        if(path == null || Path.IsPathRooted(path))
            return null;

        var fullPath = ToolchainPaths.Combine(treeFolder, path);
        var folder = ToolchainPaths.FolderOf(fullPath);
        if(!_sets.TryGetValue(folder, out var configs))
        {
            configs = ReadConfigs(folder);
            _sets.Add(folder, configs);
        }

        var options = configs.GetOptionsForSourcePath(fullPath).TreeOptions;
        if(!options.TryGetValue(ruleId, out var severity))
            return null;

        return severity switch
        {
            ReportDiagnostic.Error => "error",
            ReportDiagnostic.Warn => "warning",
            ReportDiagnostic.Info => "suggestion",
            ReportDiagnostic.Hidden => "silent",
            ReportDiagnostic.Suppress => "none",
            ReportDiagnostic.Default or _ => null
        };
    }

    private AnalyzerConfigSet ReadConfigs(string folder)
    {
        var root = ToolchainPaths.Normalize(treeFolder);
        var configs = new List<AnalyzerConfig>();
        for(var current = folder; ToolchainPaths.IsUnder(root, current) || current.Equals(root, ToolchainPaths.Comparison); current = ToolchainPaths.FolderOf(current))
        {
            var path = Path.Combine(current, ".editorconfig");
            if(!_files.TryGetValue(path, out var config))
            {
                config = files.FileExists(path) ? AnalyzerConfig.Parse(files.ReadAllText(path), path) : null;
                _files.Add(path, config);
            }

            if(config != null)
                configs.Add(config);

            if(current.Equals(root, ToolchainPaths.Comparison))
                break;
        }

        return AnalyzerConfigSet.Create(configs);
    }
}