namespace Mcacc.Core.Model;

/// <param name="Command">The executable as a launch script writes it; looked up on the <c>PATH</c>.</param>
/// <param name="ConfigDirVariable">The variable that points the CLI at a profile; unset means the default profile.</param>
/// <param name="DefaultHomeFolder">
/// Below the user's home directory: where the CLI keeps the profile that is not isolated by MCACC.
/// </param>
public sealed record ProviderCli(string Command, string ConfigDirVariable, string DefaultHomeFolder);
