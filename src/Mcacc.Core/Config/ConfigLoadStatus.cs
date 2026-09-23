namespace Mcacc.Core.Config;

public enum ConfigLoadStatus
{
    Loaded,
    CreatedDefault,
    RecoveredFromBackup,
    ResetAfterCorruption,
    NewerVersionReadOnly,
}
