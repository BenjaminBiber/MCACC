namespace Mcacc.Core.Abstractions;

public interface IExecutableLocator
{
    /// <returns>The full path, or null when <paramref name="name"/> is not on the search path.</returns>
    string? Find(string name);
}
