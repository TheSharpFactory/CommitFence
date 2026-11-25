using CF.Core.Ports;
using CF.Core.Services;

namespace CF.App.Tests.Support;

/// <summary>Sample reports for the built-in graph and a larger graph with longer labels.</summary>
internal static class StructureSamples
{
    public static StructureReport Own(StructureJudgment? judgment = null)
    {
        var modules = new List<StructureModuleReport>
        {
            Module("app", "CF.App", "commitfenceapp"), Module("cli", "CF.Cli", "commitfence"),
            Module("composition", "CF.Composition", "CF.Composition"), Module("core-services", "CF.Core.Services"),
            Module("core-review", "CF.Core.Review"), Module("core-rules", "CF.Core.Rules"), Module("core-structure", "CF.Core.Structure"),
            Module("csharp", "CF.Languages.CSharp", "CF.Languages.CSharp"), Module("adapters", "CF.Adapters", "CF.Adapters"),
            Module("core", "CF.Core", "CF.Core"), Module("meaning", "CF.Meaning", "CF.Meaning")
        };
        return FakeStructureService.Build(modules, Parse("""
            core-structure>core core-rules>core core-rules>core-structure core-review>core core-review>core-rules core-review>core-structure
            core-services>core core-services>core-rules core-services>core-review core-services>core-structure csharp>core adapters>core composition>core
            composition>core-review composition>core-rules composition>core-services composition>csharp composition>adapters cli>composition cli>core
            cli>core-review cli>core-rules cli>core-services app>composition app>core app>core-review app>core-rules app>core-services
            """), judgment);
    }

    public static StructureReport Large(StructureJudgment? judgment = null)
    {
        var hosts = Names("billing-api admin-portal export-worker migrations");
        var apps = Names("billing-app invoicing-app payments-app customers-app reporting-app notify-app");
        var infra = Names("billing-sql invoicing-sql payments-stripe customers-sql mail-smtp pdf-render");
        var domains = Names("billing invoicing payments customers reporting notify tax");
        var shared = Names("kernel money clock validation logging ids");
        var names = hosts.Append("composition").Concat(apps).Concat(infra).Concat(domains).Concat(shared);
        var edges = new List<StructureDirection>();
        for(var i = 0; i < hosts.Length; i++)
            Add(edges, hosts[i], "composition " + apps[i] + " logging");

        edges.AddRange(Parse("""
            billing-api>invoicing-app admin-portal>customers-app export-worker>reporting-app
            invoicing-app>billing invoicing-app>customers invoicing-app>tax payments-app>invoicing reporting-app>invoicing notify-app>customers
            billing-sql>billing-app billing-sql>billing invoicing-sql>invoicing-app invoicing-sql>invoicing
            payments-stripe>payments-app payments-stripe>payments customers-sql>customers-app customers-sql>customers
            mail-smtp>notify-app pdf-render>invoicing-app validation>kernel money>kernel ids>kernel
            """));
        foreach(var name in apps.Concat(infra))
            edges.Add(new StructureDirection("composition", name));

        for(var i = 0; i < apps.Length; i++)
            Add(edges, apps[i], domains[i] + " kernel clock");

        foreach(var name in infra)
            edges.Add(new StructureDirection(name, "logging"));

        foreach(var name in domains)
            Add(edges, name, "kernel money validation ids");

        return FakeStructureService.Build(names.Select(name => Module(name, "Invoicing." + name)), edges, judgment);
    }

    public static StructureJudgment Clean()
    {
        return new StructureJudgment(Samples.Now, null, []);
    }

    public static StructureJudgment Broken()
    {
        return new StructureJudgment(Samples.Now, null,
            [new StructureBreach("core-rules", "core-review", "SettingsScanner.Scan", "ReadingRules", "Dependencies/CF.Core/Rules/Settings/SettingsScanner.cs", 58)]);
    }

    public static StructureModuleReport Module(string name, string @namespace, string? project = null)
    {
        var parts = new List<StructurePart> { new StructurePart("namespace", @namespace) };
        if(project != null)
            parts.Add(new StructurePart("project", project));

        return new StructureModuleReport(name, parts, [], []);
    }

    public static List<StructureDirection> Parse(string directions)
    {
        return directions.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(pair => pair.Split('>'))
            .Select(pair => new StructureDirection(pair[0], pair[1])).ToList();
    }

    private static string[] Names(string names)
    {
        return names.Split(' ');
    }

    private static void Add(List<StructureDirection> edges, string source, string targets)
    {
        edges.AddRange(Names(targets).Select(target => new StructureDirection(source, target)));
    }
}
