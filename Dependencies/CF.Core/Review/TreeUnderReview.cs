using CF.Core.Changes;
using CF.Core.Ports;

namespace CF.Core.Review;

/// <summary>
/// What the stages of a change set's review share: the repository, the change set, the folder that holds the private
/// copy of its tree, and the settings file the review was asked to use.
/// </summary>
internal sealed class TreeUnderReview
{
    /// <summary>
    /// The name of the settings file at the root of a tree.
    /// </summary>
    public const string DefaultSettingsFile = "commitfence.json";

    public TreeUnderReview(RepositoryLocation repository, ChangeSet changeSet, string folder, string? givenSettingsPath)
    {
        Repository = repository;
        ChangeSet = changeSet;
        Folder = folder;
        GivenSettingsPath = givenSettingsPath;
    }

    public RepositoryLocation Repository { get; }

    public ChangeSet ChangeSet { get; }

    /// <summary>
    /// The folder that holds the private copy of the tree under review.
    /// </summary>
    public string Folder { get; }

    /// <summary>
    /// The settings file the review was told to use, or nothing when the tree's own is used.
    /// </summary>
    public string? GivenSettingsPath { get; }

    /// <summary>
    /// The settings file of this review: the one given, or the file of the default name in the private copy.
    /// </summary>
    public string SettingsPath()
    {
        return GivenSettingsPath ?? Path.Combine(Folder, DefaultSettingsFile);
    }

    /// <summary>
    /// Whether the change set adds or changes the settings file this review reads from the tree: then that file must be
    /// read strictly even when nothing else is to review, because a broken file would refuse every later review.
    /// </summary>
    public bool ChangesTheSettingsOfTheTree()
    {
        if(GivenSettingsPath != null)
            return false;

        return ChangeSet.Files.Any(file => file.Status != ChangeStatus.Deleted && string.Equals(file.Path, DefaultSettingsFile, StringComparison.OrdinalIgnoreCase));
    }
}