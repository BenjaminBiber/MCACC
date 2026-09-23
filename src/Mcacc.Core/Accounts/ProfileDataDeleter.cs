using Mcacc.Core.FileSystem;
using Mcacc.Core.Model;
using Mcacc.Core.Paths;

namespace Mcacc.Core.Accounts;

public sealed class ProfileDataDeleter
{
    public void Delete(Account account, McaccPaths paths)
    {
        if (account.IsDefault)
        {
            throw new InvalidOperationException("The default account's data is never deleted.");
        }

        // Win32 trims trailing dots and spaces, so "privat." would reach "privat" and "..." the profiles root.
        if (!AccountId.IsValid(account.Id))
        {
            throw new InvalidOperationException($"Refusing to delete the data of the invalid account id '{account.Id}'.");
        }

        var profileDir = paths.ProfileDir(account.Id);
        if (!PathComparer.IsStrictlyUnder(profileDir, paths.ProfilesDir, paths.Style)
            || !PathComparer.IsStrictlyUnder(Path.GetFullPath(profileDir), Path.GetFullPath(paths.ProfilesDir), paths.Style))
        {
            throw new InvalidOperationException($"Refusing to delete '{profileDir}', which is not inside '{paths.ProfilesDir}'.");
        }

        var profile = new DirectoryInfo(profileDir);
        if (profile.IsReparsePoint())
        {
            profile.Delete();
            return;
        }

        if (!profile.Exists)
        {
            return;
        }

        PrepareForDeletion(profile);
        profile.Delete(recursive: true);
    }

    // Links are removed up front instead of followed: their targets stay untouched, and Windows cannot
    // remove a nested junction inside Directory.Delete(recursive) without admin rights.
    private static void PrepareForDeletion(DirectoryInfo directory)
    {
        ClearReadOnly(directory);
        foreach (var entry in directory.EnumerateAllChildren().ToList())
        {
            if (entry.IsReparsePoint())
            {
                entry.Delete();
            }
            else if (entry is DirectoryInfo subdirectory)
            {
                PrepareForDeletion(subdirectory);
            }
            else
            {
                ClearReadOnly(entry);
            }
        }
    }

    private static void ClearReadOnly(FileSystemInfo entry)
    {
        if (entry.Attributes.HasFlag(FileAttributes.ReadOnly))
        {
            entry.Attributes &= ~FileAttributes.ReadOnly;
        }
    }
}
