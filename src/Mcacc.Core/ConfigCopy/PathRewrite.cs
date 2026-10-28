namespace Mcacc.Core.ConfigCopy;

/// <summary>Whether a copied file holds paths that point back at the source profile, and whether they must go.</summary>
public enum PathRewrite
{
    /// <summary>The file holds no such paths.</summary>
    None,

    /// <summary>The user decides; a stale path there costs a hook or a status line, not the isolation.</summary>
    Optional,

    /// <summary>
    /// Always rewritten. The file names the provider's config-home variable itself, so an unrewritten copy would
    /// point the new profile back at the one it came from.
    /// </summary>
    Required,
}
