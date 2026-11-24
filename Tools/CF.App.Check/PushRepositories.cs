using System.Diagnostics;

namespace CF.App.Check;

/// <summary>
/// The four versions of the second commit of a scratch repository that is ahead of its remote.
/// </summary>
internal enum PushKind
{
    Refused,

    Warned,

    Clean,

    Incomplete
}

/// <summary>
/// Makes the scratch repositories of the Review checks: a folder named invoicing on the branch main with a bare repository as
/// its origin, one commit already pushed, and a second commit, not pushed, that adds five files. Nothing outside the folder it is
/// given is read or written.
/// </summary>
internal static class PushRepositories
{
    private const string _project = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Library</OutputType>
            <TargetFramework>net11.0</TargetFramework>
            <ImplicitUsings>disable</ImplicitUsings>
            <GenerateDocumentationFile>true</GenerateDocumentationFile>
            <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
            <NoWarn>CS1591</NoWarn>
          </PropertyGroup>
        </Project>
        """;

    private const string _brokenProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net11.0
        </Project>
        """;

    private const string _editorConfig = "root = true\n\n[*.cs]\ndotnet_diagnostic.IDE0005.severity = warning\ndotnet_diagnostic.CA1822.severity = none\n";

    private const string _serviceWithBreach = """
        using System.Text;

        namespace Billing;

        public class InvoiceService
        {
            public int Total(int count)
            {
                if (count < 0) { return 0; }
                return count * 2;
            }
        }
        """;

    private const string _serviceWithUsing = """
        using System.Text;

        namespace Billing;

        public class InvoiceService
        {
            public int Total(int count)
            {
                if (count < 0)
                    return 0;
                return count * 2;
            }
        }
        """;

    private const string _serviceClean = """
        namespace Billing;

        public class InvoiceService
        {
            public int Total(int count)
            {
                if (count < 0)
                    return 0;
                return count * 2;
            }
        }
        """;

    private const string _mapperWithBreach = """
        namespace Billing;

        internal class InvoiceMapper
        {
            internal int Map(int value)
            {
                return value + 1;
            }
        }
        """;

    private const string _mapperClean = """
        namespace Billing;

        internal class InvoiceMapper
        {
            public int Map(int value)
            {
                return value + 1;
            }
        }
        """;

    private const string _line = """
        namespace Billing;

        public class InvoiceLine
        {
            public int Quantity { get; set; }
        }
        """;

    private const string _totals = """
        namespace Billing;

        public class InvoiceTotals
        {
            public int Sum { get; set; }
        }
        """;

    private const string _orderMapper = """
        namespace Orders;

        public class OrderMapper
        {
            public int Map(int value)
            {
                return value;
            }
        }
        """;

    /// <summary>
    /// Makes the repository, with its remote beside it, and returns the folder of the work.
    /// </summary>
    public static string Make(string parent, string name, PushKind kind, string sampleSettings)
    {
        var folder = Path.Combine(parent, name, "invoicing");
        Directory.CreateDirectory(Path.Combine(folder, "Billing"));
        Directory.CreateDirectory(Path.Combine(folder, "Orders"));
        var remote = Path.Combine(parent, name, "origin.git");
        Git(parent, "init", "-q", "--bare", "-b", "main", remote);
        Git(folder, "init", "-q", "-b", "main");
        File.Copy(sampleSettings, Path.Combine(folder, SampleFolders.SettingsName));
        File.WriteAllText(Path.Combine(folder, "Lib.csproj"), _project + "\n");
        File.WriteAllText(Path.Combine(folder, ".editorconfig"), _editorConfig);
        File.WriteAllText(Path.Combine(folder, "Base.cs"), "namespace Billing;\n\npublic class Base\n{\n}\n");
        Commit(folder, "base");
        Git(folder, "remote", "add", "origin", remote);
        Git(folder, "push", "-q", "-u", "origin", "main");
        AddTheFiles(folder, kind);
        Commit(folder, "invoices");

        return folder;
    }

    /// <summary>
    /// A repository with a commit and no remote.
    /// </summary>
    public static string MakeWithoutRemote(string parent, string name, string sampleSettings)
    {
        var folder = Path.Combine(parent, name, "invoicing");
        Directory.CreateDirectory(folder);
        Git(folder, "init", "-q", "-b", "main");
        File.Copy(sampleSettings, Path.Combine(folder, SampleFolders.SettingsName));
        File.WriteAllText(Path.Combine(folder, "Base.cs"), "namespace Billing;\n\npublic class Base\n{\n}\n");
        Commit(folder, "base");

        return folder;
    }

    /// <summary>
    /// A repository that is ahead of its remote and whose pushed commit holds a settings file that is not valid JSON.
    /// </summary>
    public static string MakeWithBrokenSettings(string parent, string name, string sampleSettings)
    {
        var folder = Make(parent, name, PushKind.Clean, sampleSettings);
        File.WriteAllText(Path.Combine(folder, SampleFolders.SettingsName), "{ \"rules\": [ ");
        Commit(folder, "settings");

        return folder;
    }

    /// <summary>
    /// A repository whose pushed commit holds breaches that were committed long ago (the folder review's repository): sixteen
    /// blocking and four warning findings in four services, four blocking in two mappers, two warnings in two orders, and clean
    /// files; and a second commit, not pushed, that adds one clean file.
    /// </summary>
    public static string MakeWithOldBreaches(string parent, string name, string sampleSettings)
    {
        var folder = Path.Combine(parent, name, "invoicing");
        foreach(var part in new[] { "Billing", "Orders", "Tidy", "Extra1", "Extra2", "Extra3", "Extra4" })
            Directory.CreateDirectory(Path.Combine(folder, part));

        var remote = Path.Combine(parent, name, "origin.git");
        Git(parent, "init", "-q", "--bare", "-b", "main", remote);
        Git(folder, "init", "-q", "-b", "main");
        File.Copy(sampleSettings, Path.Combine(folder, SampleFolders.SettingsName));
        File.WriteAllText(Path.Combine(folder, "Lib.csproj"), _project + "\n");
        File.WriteAllText(Path.Combine(folder, ".editorconfig"), _editorConfig);
        File.WriteAllText(Path.Combine(folder, "Base.cs"), "namespace Billing;\n\npublic class Base\n{\n}\n");
        File.WriteAllText(Path.Combine(folder, "Billing", "Clean.cs"), _orderMapper.Replace("Orders", "Billing", StringComparison.Ordinal).Replace("OrderMapper", "Clean", StringComparison.Ordinal) + "\n");
        File.WriteAllText(Path.Combine(folder, "Tidy", "Tidy.cs"), _orderMapper.Replace("Orders", "Tidy", StringComparison.Ordinal).Replace("OrderMapper", "Tidy", StringComparison.Ordinal) + "\n");
        for(var number = 1; number <= 4; number++)
        {
            File.WriteAllText(Path.Combine(folder, "Billing", $"Service{number}.cs"), OldService(number) + "\n");
            File.WriteAllText(Path.Combine(folder, $"Extra{number}", "Extra.cs"), _orderMapper.Replace("Orders", $"Extra{number}", StringComparison.Ordinal).Replace("OrderMapper", "Extra", StringComparison.Ordinal) + "\n");
        }

        for(var number = 1; number <= 2; number++)
        {
            File.WriteAllText(Path.Combine(folder, "Billing", $"Mapper{number}.cs"), OldMapper(number) + "\n");
            File.WriteAllText(Path.Combine(folder, "Orders", $"Order{number}.cs"), OldOrder(number) + "\n");
        }

        Commit(folder, "old breaches");
        Git(folder, "remote", "add", "origin", remote);
        Git(folder, "push", "-q", "-u", "origin", "main");
        File.WriteAllText(Path.Combine(folder, "Billing", "InvoiceLine.cs"), _line + "\n");
        Commit(folder, "a clean file");

        return folder;
    }

    /// <summary>
    /// Changes a tracked file in the working folder, so that the folder holds uncommitted work.
    /// </summary>
    public static void MakeUncommittedChange(string folder)
    {
        File.AppendAllText(Path.Combine(folder, "Billing", "Clean.cs"), "// changed after the commit\n");
    }

    private static string OldService(int number)
    {
        return $$"""
            using System.Text;

            namespace Billing;

            public class Service{{number}}
            {
                public int First(int count)
                {
                    if (count < 0) { return 0; }
                    return count;
                }

                public int Second(int count)
                {
                    if (count < 1) { return 1; }
                    return count;
                }

                public int Third(int count)
                {
                    if (count < 2) { return 2; }
                    return count;
                }

                public int Fourth(int count)
                {
                    if (count < 3) { return 3; }
                    return count;
                }
            }
            """;
    }

    private static string OldMapper(int number)
    {
        return $$"""
            namespace Billing;

            internal class Mapper{{number}}
            {
                internal int One(int value)
                {
                    return value + 1;
                }

                internal int Two(int value)
                {
                    return value + 2;
                }
            }
            """;
    }

    private static string OldOrder(int number)
    {
        return $$"""
            using System.Text;

            namespace Orders;

            public class Order{{number}}
            {
                public int Map(int value)
                {
                    return value;
                }
            }
            """;
    }

    public static void DetachHead(string folder)
    {
        Git(folder, "checkout", "-q", "--detach", "HEAD");
    }

    public static void NewBranch(string folder, string name)
    {
        Git(folder, "checkout", "-q", "-b", name);
    }

    public static string Head(string folder)
    {
        return Git(folder, "rev-parse", "HEAD").Trim();
    }

    public static string Show(string folder, string revision, string path)
    {
        return Git(folder, "show", revision + ":" + path);
    }

    public static string RemoteTip(string folder)
    {
        return Git(folder, "rev-parse", "origin/main").Trim();
    }

    public static string RemoteUrl(string folder)
    {
        return Git(folder, "remote", "get-url", "--push", "origin").Trim();
    }

    private static void AddTheFiles(string folder, PushKind kind)
    {
        if(kind == PushKind.Incomplete)
            File.WriteAllText(Path.Combine(folder, "Lib.csproj"), _brokenProject + "\n");

        var service = kind == PushKind.Refused ? _serviceWithBreach : kind == PushKind.Warned ? _serviceWithUsing : _serviceClean;
        var mapper = kind == PushKind.Refused ? _mapperWithBreach : _mapperClean;
        File.WriteAllText(Path.Combine(folder, "Billing", "InvoiceService.cs"), service + "\n");
        File.WriteAllText(Path.Combine(folder, "Billing", "InvoiceMapper.cs"), mapper + "\n");
        File.WriteAllText(Path.Combine(folder, "Billing", "InvoiceLine.cs"), _line + "\n");
        File.WriteAllText(Path.Combine(folder, "Billing", "InvoiceTotals.cs"), _totals + "\n");
        File.WriteAllText(Path.Combine(folder, "Orders", "OrderMapper.cs"), _orderMapper + "\n");
    }

    private static void Commit(string folder, string message)
    {
        Git(folder, "add", "-A");
        Git(folder, "-c", "user.name=Check", "-c", "user.email=check@example.invalid", "-c", "commit.gpgsign=false", "commit", "-q", "-m", message);
    }

    private static string Git(string folder, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = folder,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach(var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("git did not start");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if(!process.WaitForExit(60000) || process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error.Result}");

        return output.Result;
    }
}
