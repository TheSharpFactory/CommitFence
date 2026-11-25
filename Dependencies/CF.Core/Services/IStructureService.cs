namespace CF.Core.Services;

/// <summary>Reads and edits the structure rules of a settings file.</summary>
public interface IStructureService
{
    /// <summary>Reads the modules, their directions and the last review that judged them.</summary>
    StructureReport Show(string folder);

    /// <summary>Adds a module with no parts.</summary>
    StructureChange AddModule(string folder, string name);

    /// <summary>Renames a module and updates its directions.</summary>
    StructureChange RenameModule(string folder, string name, string newName);

    /// <summary>Removes a module and all its directions.</summary>
    StructureChange RemoveModule(string folder, string name);

    /// <summary>Adds a part, written as kind:pattern.</summary>
    StructureChange AddPart(string folder, string name, string part);

    /// <summary>Removes a part, written as kind:pattern.</summary>
    StructureChange RemovePart(string folder, string name, string part);

    /// <summary>Changes whether the first module may use the second.</summary>
    StructureChange SetDirection(string folder, string from, string to, bool allowed);

    /// <summary>Restores the settings from before a change if the file still contains that change.</summary>
    void Undo(string folder, StructureChange change);
}
