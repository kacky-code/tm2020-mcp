using System.Text;
using System.Text.RegularExpressions;

namespace Tm2020Mcp.Manialinks;

public sealed partial class ManialinkInspector
{
    public string InspectInteractiveControls(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return "No ManiaLink XML provided.";

        var controls = new List<string>();
        foreach (Match match in ControlTagRegex().Matches(xml))
        {
            var tag = match.Groups["tag"].Value;
            var attrs = match.Groups["attrs"].Value;
            var id = GetAttribute(attrs, "id");
            var action = GetAttribute(attrs, "action");
            var scriptAction = GetAttribute(attrs, "scriptaction");
            var scriptEvents = GetAttribute(attrs, "scriptevents");

            if (action is null && scriptAction is null && scriptEvents is null)
                continue;

            controls.Add(
                $"{tag} id={id ?? "(none)"}, action={action ?? "-"}, scriptaction={scriptAction ?? "-"}, scriptevents={scriptEvents ?? "-"}");
        }

        if (controls.Count == 0)
            return "No interactive label/quad controls found.";

        var builder = new StringBuilder();
        builder.AppendLine($"Interactive controls: {controls.Count}");
        foreach (var control in controls.Take(100))
            builder.AppendLine($"- {control}");
        if (controls.Count > 100)
            builder.AppendLine($"- ... {controls.Count - 100} more");

        return builder.ToString().TrimEnd();
    }

    private static string? GetAttribute(string attrs, string name)
    {
        var pattern = $@"\b{Regex.Escape(name)}\s*=\s*""(?<value>[^""]*)""";
        var match = Regex.Match(attrs, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value : null;
    }

    [GeneratedRegex(@"<(?<tag>label|quad)\b(?<attrs>[^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ControlTagRegex();
}
