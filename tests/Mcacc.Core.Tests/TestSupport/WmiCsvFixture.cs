using System.Globalization;
using System.Text;
using Mcacc.Core.Processes;

namespace Mcacc.Core.Tests.TestSupport;

/// <summary>
/// Reads a snapshot saved by <c>Get-CimInstance Win32_Process | Select-Object ProcessId,ParentProcessId,ExecutablePath,
/// CommandLine,CreationDate | Export-Csv -NoTypeInformation</c>: quoted fields with doubled inner quotes.
/// </summary>
internal static class WmiCsvFixture
{
    public static IReadOnlyList<ProcessInfo> Load(string name)
    {
        var records = ReadRecords(FixtureFiles.ReadAllText(name));
        var header = records[0];
        var pid = header.IndexOf("ProcessId");
        var parentPid = header.IndexOf("ParentProcessId");
        var exePath = header.IndexOf("ExecutablePath");
        var commandLine = header.IndexOf("CommandLine");
        var creationDate = header.IndexOf("CreationDate");

        return records.Skip(1)
            .Select(r => new ProcessInfo(
                int.Parse(r[pid], CultureInfo.InvariantCulture),
                int.Parse(r[parentPid], CultureInfo.InvariantCulture),
                NullIfEmpty(r[exePath]),
                NullIfEmpty(r[commandLine]),
                r[creationDate].Length == 0 ? null : DateTimeOffset.Parse(r[creationDate], CultureInfo.InvariantCulture)))
            .ToList();
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static List<List<string>> ReadRecords(string csv)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];
            if (inQuotes)
            {
                if (c != '"')
                {
                    field.Append(c);
                }
                else if (i + 1 < csv.Length && csv[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = false;
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    record.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    EndRecord();
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        EndRecord();
        return records;

        void EndRecord()
        {
            record.Add(field.ToString());
            field.Clear();
            if (record is not [""])
            {
                records.Add(record);
            }

            record = [];
        }
    }
}
