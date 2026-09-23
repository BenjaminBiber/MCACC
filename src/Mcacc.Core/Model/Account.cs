namespace Mcacc.Core.Model;

public sealed record Account(string Id, string Name, string Color, bool IsDefault)
{
    public const string DefaultId = "standard";
    public const string DefaultName = "Standard";
    public const string DefaultColor = "#D97757";

    public static Account CreateDefault() => new(DefaultId, DefaultName, DefaultColor, IsDefault: true);
}
