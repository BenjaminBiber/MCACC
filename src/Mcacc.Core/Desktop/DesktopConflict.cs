using Mcacc.Core.Model;

namespace Mcacc.Core.Desktop;

/// <param name="Owner">Null for a profile that belongs to no account.</param>
public sealed record DesktopConflict(DesktopInstance Instance, Account? Owner);
