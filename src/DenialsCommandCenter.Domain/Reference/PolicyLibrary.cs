using System.Globalization;
using System.Text.RegularExpressions;

namespace DenialsCommandCenter.Domain.Reference;

public sealed record PolicySection(string PolicyId, int Number, string Text)
{
    public string Citation => $"{PolicyId} §{Number}";
}

public sealed record PolicyDocument(string PolicyId, string Title, IReadOnlyList<PolicySection> Sections);

public sealed class PolicyLibrary
{
    private static readonly Regex NumberedLine = new(@"^(\d+)\.\s+(.+)$", RegexOptions.CultureInvariant);
    private readonly Dictionary<string, PolicySection> _sectionsByCitation = new(StringComparer.Ordinal);

    public PolicyLibrary(IEnumerable<PolicyDocument> documents)
    {
        Documents = documents.OrderBy(d => d.PolicyId, StringComparer.Ordinal).ToList();
        // A policy file that restarts its numbering keeps the first section under each citation.
        foreach (var section in Documents.SelectMany(d => d.Sections))
            _sectionsByCitation.TryAdd(section.Citation, section);
        Citations = _sectionsByCitation.Keys.ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlyList<PolicyDocument> Documents { get; }

    public IReadOnlySet<string> Citations { get; }

    public PolicySection? Find(string citation) => _sectionsByCitation.GetValueOrDefault(citation);

    public static PolicyLibrary Parse(IEnumerable<(string FileName, string Content)> files) =>
        new(files.Select(file => ParseDocument(Path.GetFileNameWithoutExtension(file.FileName), file.Content)));

    public static PolicyDocument ParseDocument(string policyId, string content)
    {
        var title = "";
        var sections = new List<PolicySection>();
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            if (title.Length == 0 && line.StartsWith("# ", StringComparison.Ordinal))
            {
                title = line[2..].Trim();
                continue;
            }
            var numbered = NumberedLine.Match(line);
            if (numbered.Success)
                sections.Add(new PolicySection(policyId, int.Parse(numbered.Groups[1].Value, CultureInfo.InvariantCulture), numbered.Groups[2].Value.Trim()));
            else if (sections.Count > 0 && !line.StartsWith('#'))
                sections[^1] = sections[^1] with { Text = $"{sections[^1].Text} {line}" };
        }
        return new PolicyDocument(policyId, title, sections);
    }
}
