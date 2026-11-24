using CF.Core.Code;
using CF.Core.Ports;
using CF.Core.Services;

namespace CF.Composition;

/// <summary>
/// The services the programs use, all typed by Core. The programs never see which class implements which.
/// </summary>
public sealed class CommitFenceServices
{
    /// <summary>
    /// Creates the set of services.
    /// </summary>
    public CommitFenceServices(IProductInfoService productInfo, IClock clock, IReadOnlyList<ILanguage> languages, IRuleService rules, IReviewService review, ISetupService setup, IFolderService folders, IUpdater updater, IUpdateCheckRecord updateChecks)
    {
        ProductInfo = productInfo;
        Clock = clock;
        Languages = languages;
        Rules = rules;
        Review = review;
        Setup = setup;
        Folders = folders;
        Updater = updater;
        UpdateChecks = updateChecks;
    }

    /// <summary>
    /// The name and the version of the product.
    /// </summary>
    public IProductInfoService ProductInfo { get; }

    /// <summary>
    /// The clock.
    /// </summary>
    public IClock Clock { get; }

    /// <summary>
    /// Every language the product can review.
    /// </summary>
    public IReadOnlyList<ILanguage> Languages { get; }

    /// <summary>
    /// The rules of a settings file.
    /// </summary>
    public IRuleService Rules { get; }

    /// <summary>
    /// Reviews code against the rules.
    /// </summary>
    public IReviewService Review { get; }

    /// <summary>
    /// Installs the program and sets a repository up.
    /// </summary>
    public ISetupService Setup { get; }

    /// <summary>
    /// Says where a folder's settings file is and which repository the folder is in.
    /// </summary>
    public IFolderService Folders { get; }

    /// <summary>
    /// Updates the program in place from the latest release.
    /// </summary>
    public IUpdater Updater { get; }

    /// <summary>
    /// When the program last asked for a newer release.
    /// </summary>
    public IUpdateCheckRecord UpdateChecks { get; }
}