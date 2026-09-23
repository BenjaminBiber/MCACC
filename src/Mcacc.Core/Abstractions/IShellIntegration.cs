namespace Mcacc.Core.Abstractions;

public interface IShellIntegration
{
    /// <summary>Shows the folder in the file manager.</summary>
    void RevealFolder(string path);

    void OpenUrl(Uri url);
}
