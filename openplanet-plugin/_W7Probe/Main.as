// THROWAWAY probe for wayfinder ticket W7. Not part of the bridge, delete after answering.
//
// Revision 2. The first revision answered W1 (the tree walk is real) and refuted W2 (the
// DownloadInProgress + Image tri-state calls everything failed). Both results reproduced across
// two runs, so this revision stops re-litigating them and goes after what is still open:
//
//   1. WHAT CONTEXT was the dump taken in? Revision 1 printed no context at all, so a menu dump
//      and an on-server dump were indistinguishable -- and they came back identical, which was
//      unreadable without knowing the client state behind each.
//   2. CAN A LAYER BE IDENTIFIED without AttachId? It was 'Unassigned' on all 29 layers both
//      times, so identity has to come from the <manialink> tag in the XML. This prints it.
//   3. IS ControlsCache TRUSTWORTHY? Five layers reported controls=0 with a live MainFrame, one
//      of them off 6.5KB of XML. This walks MainFrame as a fallback and reports both numbers.
//
// Install: copy this folder into OpenplanetNext\Plugins, then use Plugins > W7 Readback Probe.
// Output goes to the Openplanet log.

void Main() { }

// Revision 1 printed Type as a bare int, which read as menu chrome when it was actually the
// playground's own layer set. Never again.
string LayerTypeName(CGameUILayer::EUILayerType type)
{
    switch (type) {
        case CGameUILayer::EUILayerType::Normal:           return "Normal";
        case CGameUILayer::EUILayerType::ScoresTable:      return "ScoresTable";
        case CGameUILayer::EUILayerType::ScreenIn3d:       return "ScreenIn3d";
        case CGameUILayer::EUILayerType::AltMenu:          return "AltMenu";
        case CGameUILayer::EUILayerType::Markers:          return "Markers";
        case CGameUILayer::EUILayerType::CutScene:         return "CutScene";
        case CGameUILayer::EUILayerType::InGameMenu:       return "InGameMenu";
        case CGameUILayer::EUILayerType::EditorPlugin:     return "EditorPlugin";
        case CGameUILayer::EUILayerType::ManiaplanetPlugin: return "ManiaplanetPlugin";
        case CGameUILayer::EUILayerType::ManiaplanetMenu:  return "ManiaplanetMenu";
        case CGameUILayer::EUILayerType::LoadingScreen:    return "LoadingScreen";
    }
    return "Unknown(" + int(type) + ")";
}

// AttachId is dead as an identifier, so fall back to the <manialink> opening tag, which carries
// the id/name attributes a pushed layer would set.
string ManialinkTag(const string &in xml)
{
    if (xml.Length == 0) return "(empty)";

    int at = xml.IndexOf("<manialink");
    if (at < 0) return "(no <manialink> tag)";

    uint remaining = xml.Length - uint(at);
    uint take = remaining;
    if (take > 160) take = 160;
    return xml.SubStr(uint(at), take);
}

// ControlsCache came back empty on layers that clearly have content, so descend MainFrame instead
// and let the caller compare the two counts.
// 'out' is a reserved keyword in AngelScript (the &out parameter modifier), hence 'collected'.
void CollectFrom(CGameManialinkFrame@ frame, array<CGameManialinkControl@>@ collected, uint depth)
{
    if (frame is null || depth > 12) return;

    for (uint i = 0; i < frame.Controls.Length; i++) {
        auto control = frame.Controls[i];
        if (control is null) continue;

        collected.InsertLast(control);

        auto child = cast<CGameManialinkFrame>(control);
        if (child !is null) CollectFrom(child, collected, depth + 1);
    }
}

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

    // Context first. Without this a dump cannot be attributed to a client state after the fact.
    print("W7: === context ===");
    print("W7: maniaAppUrl='" + maniaApp.ManiaAppUrl + "'");
    // MapName is a wstring and Openplanet registers conversions in both directions, so a ternary
    // mixing it with a string literal has no unambiguous common type. An explicitly typed local
    // fixes the target type and leaves only the wstring -> string conversion in play.
    string mapName = "(none)";
    if (maniaApp.Map !is null) mapName = maniaApp.Map.MapName;

    string rootMapName = "(none)";
    if (app.RootMap !is null) rootMapName = app.RootMap.MapName;

    print("W7: playground=" + (maniaApp.Playground is null ? "null" : "ok")
        + " currentPlayground=" + (app.CurrentPlayground is null ? "null" : "ok")
        + " map='" + mapName + "'"
        + " rootMap='" + rootMapName + "'");

    print("W7: === layers ===");
    print("W7: UILayers = " + maniaApp.UILayers.Length);

    uint totalQuads = 0;
    uint totalLoaded = 0;
    uint totalPending = 0;
    uint totalNullImage = 0;
    uint namedLayers = 0;
    uint emptyCacheWithContent = 0;

    for (uint i = 0; i < maniaApp.UILayers.Length; i++) {
        auto layer = maniaApp.UILayers[i];
        if (layer is null) continue;

        string xml = layer.ManialinkPageUtf8;
        if (layer.AttachId != "Unassigned" && layer.AttachId != "") namedLayers++;

        print("W7: --- layer " + i
            + " attachId='" + layer.AttachId + "'"
            + " type=" + LayerTypeName(layer.Type)
            + " visible=" + (layer.IsVisible ? "1" : "0")
            + " anim=" + (layer.AnimInProgress ? "1" : "0")
            + " scriptRunning=" + (layer.IsLocalPageScriptRunning ? "1" : "0")
            + " xmlLen=" + xml.Length);

        // The identity fallback. This is the W3/W10 question now that AttachId is useless.
        print("W7:     tag: " + ManialinkTag(xml));

        auto page = layer.LocalPage;
        if (page is null) { print("W7:     LocalPage is null"); continue; }

        uint cacheLen = page.ControlsCache.Length;
        array<CGameManialinkControl@> controls;

        if (cacheLen > 0) {
            for (uint c = 0; c < cacheLen; c++) controls.InsertLast(page.ControlsCache[c]);
        } else {
            CollectFrom(page.MainFrame, controls, 0);
            if (controls.Length > 0) emptyCacheWithContent++;
        }

        print("W7:     controlsCache=" + cacheLen
            + " walked=" + controls.Length
            + " via=" + (cacheLen > 0 ? "cache" : "descent")
            + " mainFrame=" + (page.MainFrame is null ? "null" : "ok"));

        uint quads = 0;
        uint loaded = 0;
        uint pending = 0;
        uint nullImage = 0;
        uint sampled = 0;

        for (uint c = 0; c < controls.Length; c++) {
            auto control = controls[c];
            if (control is null) continue;

            auto quad = cast<CGameManialinkQuad>(control);
            if (quad is null) continue;
            if (quad.ImageUrl == "") continue;

            quads++;
            if (quad.DownloadInProgress) {
                pending++;
            } else if (quad.Image is null) {
                nullImage++;
                // Revision 1 printed one line per quad and buried the log in 2338 of them. The
                // tri-state is already refuted; a few samples are enough to keep it falsifiable.
                if (sampled < 3) {
                    sampled++;
                    print("W7:     sample id='" + control.ControlId + "' url=" + quad.ImageUrl
                        + " visible=" + (control.Visible ? "1" : "0")
                        + " pos=" + control.AbsolutePosition_V3.x + "," + control.AbsolutePosition_V3.y
                        + " size=" + control.Size.x + "x" + control.Size.y);
                }
            } else {
                loaded++;
                // If this ever fires, W2 is back on the table. Always print it.
                print("W7:     IMAGE NOT NULL id='" + control.ControlId + "' url=" + quad.ImageUrl);
            }
        }

        print("W7:     imageQuads=" + quads + " loaded=" + loaded
            + " pending=" + pending + " nullImage=" + nullImage);

        totalQuads += quads;
        totalLoaded += loaded;
        totalPending += pending;
        totalNullImage += nullImage;
    }

    print("W7: === totals ===");
    print("W7: quads=" + totalQuads + " loaded=" + totalLoaded
        + " pending=" + totalPending + " nullImage=" + totalNullImage);
    print("W7: layersWithRealAttachId=" + namedLayers + "/" + maniaApp.UILayers.Length);
    print("W7: layersWhereCacheWasEmptyButDescentFoundControls=" + emptyCacheWithContent);
    print("W7: done");
}

void RenderMenu()
{
    if (UI::MenuItem("W7 Readback Probe: dump UI layers")) {
        DumpLayers();
    }
}
