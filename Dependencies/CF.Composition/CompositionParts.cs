using CF.Core.Ports;
using CF.Core.Services;

namespace CF.Composition;

/// <summary>
/// The adapters and services that more than one part of the review is built from, handed from one creation method to
/// the next.
/// </summary>
internal sealed record CompositionParts(IFileSystem FileSystem, IClock Clock, IProcessRunner ProcessRunner, IDataFolder DataFolder, IGit Git, IProductInfoService ProductInfo);