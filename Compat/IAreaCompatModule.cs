using DrakeModsLibs.Compat;
using UnityEngine;

namespace DrakeRenameit.Compat;

/// <summary>
/// Ward stack used for paper take/edit and item-stand No access.
/// Scaffolding is <see cref="ICompatModule"/>; queries stay on RenameIt's host.
/// </summary>
internal interface IAreaCompatModule : ICompatModule
{
    /// <summary>True when this stack has an enabled ward covering <paramref name="position"/>.</summary>
    bool IsInsideEnabledWard(Vector3 position);

    /// <summary>Local-player access at <paramref name="position"/>.</summary>
    AreaCoverageKind QueryLocalAccess(Vector3 position, bool flash);
}
