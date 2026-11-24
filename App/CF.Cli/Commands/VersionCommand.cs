using CF.Core.Review;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>
/// Prints the version, and nothing else.
/// </summary>
internal sealed class VersionCommand
{
    private readonly IProductInfoService _productInfo;
    private readonly TextWriter _output;

    public VersionCommand(IProductInfoService productInfo, TextWriter output)
    {
        _productInfo = productInfo;
        _output = output;
    }

    public ExitCode Run()
    {
        _output.WriteLine(_productInfo.Version);
        return ExitCode.Passed;
    }
}