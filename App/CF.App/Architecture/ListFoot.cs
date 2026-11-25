using CF.Core.Ports;

namespace CF.App.Architecture;

/// <summary>The sentence under "Every direction": how many directions there are, how the list reads and what breaks one.</summary>
internal static class ListFoot
{
    public static string Text(ModuleLayers layers, int directionCount, IReadOnlyList<StructureBreach> breaches)
    {
        var rule = layers.Cycles.Count == 0
            ? "a module may use only modules in the layers after its own."
            : Cycles(layers.Cycles);
        return $"Every direction: {directionCount}. Each row is a module and the modules it may use. Layer 1 is the top" +
            (layers.Cycles.Count == 0 ? "; " : ". ") + rule + string.Concat(Breaks(layers, breaches).Select(sentence => " " + sentence));
    }

    // Modules that may use each other cannot stand one above the other: they share a layer, and the foot says which.
    private static string Cycles(IReadOnlyList<IReadOnlyList<string>> cycles)
    {
        var named = cycles.Take(3).Select(cycle => string.Join(", ", cycle.SkipLast(1)) + " and " + cycle[^1] + " may use each other: they share a layer.").ToList();
        if(cycles.Count > 3)
            named.Add($"And {cycles.Count - 3} more groups do the same.");

        return string.Join(" ", named);
    }

    private static List<string> Breaks(ModuleLayers layers, IReadOnlyList<StructureBreach> breaches)
    {
        var rows = layers.Rows.SelectMany((row, index) => row.Select(module => (module, layer: index + 1))).ToDictionary(pair => pair.module, pair => pair.layer, StringComparer.Ordinal);
        var pairs = breaches.Select(breach => (breach.FromModule, breach.ToModule)).Distinct().ToList();
        var sentences = pairs.Take(3).Select(pair =>
            $"{pair.FromModule}, in layer {rows.GetValueOrDefault(pair.FromModule)}, uses {pair.ToModule}, in layer {rows.GetValueOrDefault(pair.ToModule)}: that use breaks a direction.").ToList();
        if(pairs.Count > 3)
            sentences.Add($"And {pairs.Count - 3} more.");

        return sentences;
    }
}
