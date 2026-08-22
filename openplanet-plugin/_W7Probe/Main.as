// THROWAWAY probe for wayfinder ticket W7. Not part of the bridge, delete after answering.
//
// Question it exists to answer: do the members the docs promise actually carry usable values on
// a real client? Specifically whether server-delivered UI layers (kontrol widgets, ManiaScript
// gamemode HUDs) show up in UILayers with a walkable LocalPage, and whether the
// DownloadInProgress + Image tri-state really identifies a failed image.
//
// Install: copy this folder into OpenplanetNext\Plugins, then use Plugins > W7 Readback Probe.
// Output goes to the Openplanet log.

void Main() { }

void DumpLayers()
{
    auto app = GetApp();
    if (app is null) { print("W7: no app"); return; }

    auto network = cast<CTrackManiaNetwork>(app.Network);
    if (network is null) { print("W7: no network"); return; }

    auto maniaApp = network.ClientManiaAppPlayground;
    if (maniaApp is null) {
        print("W7: ClientManiaAppPlayground is null. Join a server first.");
        return;
    }

    print("W7: UILayers = " + maniaApp.UILayers.Length);

    for (uint i = 0; i < maniaApp.UILayers.Length; i++) {
        auto layer = maniaApp.UILayers[i];
        if (layer is null) continue;

        string xml = layer.ManialinkPageUtf8;

        print("W7: --- layer " + i
            + " attachId='" + layer.AttachId + "'"
            + " type=" + int(layer.Type)
            + " visible=" + (layer.IsVisible ? "1" : "0")
            + " anim=" + (layer.AnimInProgress ? "1" : "0")
            + " scriptRunning=" + (layer.IsLocalPageScriptRunning ? "1" : "0")
            + " xmlLen=" + xml.Length);

        auto page = layer.LocalPage;
        if (page is null) { print("W7:     LocalPage is null"); continue; }

        print("W7:     controls=" + page.ControlsCache.Length
            + " mainFrame=" + (page.MainFrame is null ? "null" : "ok"));

        uint quads = 0;
        uint loaded = 0;
        uint pending = 0;
        uint failed = 0;

        for (uint c = 0; c < page.ControlsCache.Length; c++) {
            auto control = page.ControlsCache[c];
            if (control is null) continue;

            auto quad = cast<CGameManialinkQuad>(control);
            if (quad is null) continue;
            if (quad.ImageUrl == "") continue;

            quads++;
            if (quad.DownloadInProgress) {
                pending++;
            } else if (quad.Image is null) {
                failed++;
                // The finding the whole feature hangs on.
                print("W7:     FAILED id='" + control.ControlId + "' url=" + quad.ImageUrl
                    + " visible=" + (control.Visible ? "1" : "0")
                    + " pos=" + control.AbsolutePosition_V3.x + "," + control.AbsolutePosition_V3.y
                    + " size=" + control.Size.x + "x" + control.Size.y);
            } else {
                loaded++;
            }
        }

        print("W7:     imageQuads=" + quads + " loaded=" + loaded
            + " pending=" + pending + " failed=" + failed);
    }

    print("W7: done");
}

void RenderMenu()
{
    if (UI::MenuItem("W7 Readback Probe: dump UI layers")) {
        DumpLayers();
    }
}
