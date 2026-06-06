using System.Text;

namespace Tm2020Mcp.Manialinks;

public sealed class ManialinkVideoProbeBuilder
{
    public string Build(string data, bool music = true, bool play = true, bool hidden = false)
    {
        if (string.IsNullOrWhiteSpace(data))
            throw new ArgumentException("Video data cannot be empty.", nameof(data));

        return $"""
            <manialink version="3">
              <frame id="video-probe.root" pos="-60 35" z-index="1">
                <quad id="video-probe.bg" pos="0 0" z-index="0" size="120 18" bgcolor="0d1014d" />
                <label id="video-probe.title" pos="3 -3" z-index="1" size="114 5" text="Video probe" textfont="RobotoCondensed" textsize="1.6" />
                <label id="video-probe.data" pos="3 -9" z-index="1" size="114 4" text="{Escape(data)}" textfont="RobotoCondensed" textsize="1" maxline="1" />
              </frame>
              <video data="{Escape(data)}" music="{Bool(music)}" play="{Bool(play)}" hidden="{Bool(hidden)}" />
            </manialink>
            """;
    }

    private static string Bool(bool value) => value ? "1" : "0";

    private static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(ch switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&apos;",
                _ => ch
            });
        }

        return builder.ToString();
    }
}
