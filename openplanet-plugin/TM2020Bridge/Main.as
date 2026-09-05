// TM2020Bridge - local OpenPlanet HTTP bridge for MCP clients.
//
// Endpoints:
//   GET  /status
//   POST /save
//   POST /map/new
//   POST /map/blocks
//   POST /map/blocks/remove
//   POST /map/save
//   POST /map/open
//   POST /manialink/preview
//   POST /manialink/clear
//   GET  /manialink/current
//   GET  /manialink/events
//   POST /manialink/events
//   POST /manialink/events/clear

Net::Socket@ g_server;
string g_currentManialinkXml = "";
array<string> g_recentManialinkEvents;
const uint MaxRecentManialinkEvents = 50;

void Main()
{
    @g_server = Net::Socket();
    if (!g_server.Listen("127.0.0.1", 29100))
    {
        warn("TM2020Bridge: failed to listen on http://127.0.0.1:29100");
        return;
    }

    print("TM2020Bridge: listening on http://127.0.0.1:29100");

    while (true)
    {
        auto client = g_server.Accept();
        if (client !is null)
            HandleClient(client);

        yield();
    }
}

void OnDestroyed()
{
    if (g_server !is null)
    {
        g_server.Close();
        @g_server = null;
    }

    print("TM2020Bridge: stopped");
}

void HandleClient(Net::Socket@ client)
{
    int waited = 0;
    while (client.Available() == 0 && !client.IsHungUp() && waited < 50)
        waited++;

    if (client.Available() == 0)
    {
        client.Close();
        return;
    }

    string request = ReadHttpRequest(client);
    string method = "";
    string path = "";
    string body = GetRequestBody(request);

    int spaceIdx = request.IndexOf(" ");
    if (spaceIdx > 0)
    {
        method = request.SubStr(0, spaceIdx);
        string rest = request.SubStr(spaceIdx + 1);
        int pathEnd = rest.IndexOf(" ");
        if (pathEnd > 0)
            path = rest.SubStr(0, pathEnd);
    }

    string responseBody = "";
    int status = 404;

    if (method == "GET" && path == "/status")
    {
        bool mapEditorOpen = (cast<CGameCtnEditorFree>(GetApp().Editor) !is null);
        bool editorBaseOpen = (GetApp().EditorBase !is null);
        bool interfaceDesignerOpen = (cast<CGameEditorManialink>(GetApp().Editor) !is null)
            || (cast<CGameEditorManialink>(GetApp().EditorBase) !is null);
        bool moduleEditorOpen = (cast<CGameEditorModule>(GetApp().Editor) !is null);

        responseBody = '{"running":true'
            + ',"editor_open":' + ((mapEditorOpen || interfaceDesignerOpen || moduleEditorOpen) ? 'true' : 'false')
            + ',"editor_base":' + (editorBaseOpen ? 'true' : 'false')
            + ',"map_editor":' + (mapEditorOpen ? 'true' : 'false')
            + ',"interface_designer":' + (interfaceDesignerOpen ? 'true' : 'false')
            + ',"module_editor":' + (moduleEditorOpen ? 'true' : 'false')
            + ',"manialink_preview":' + (g_currentManialinkXml.Length > 0 ? 'true' : 'false')
            + '}';
        status = 200;
    }
    else if (method == "POST" && path == "/save")
    {
        string err = AutosaveMapEditor();
        if (err.Length == 0)
        {
            responseBody = '{"saved":true}';
            status = 200;
            print("TM2020Bridge: map editor autosave triggered");
        }
        else
        {
            responseBody = '{"error":"' + JsonEscape(err) + '"}';
            status = 400;
        }
    }
    else if (method == "POST" && path == "/map/new")
    {
        string summary = "";
        string err = CreateNewMap(body, summary);
        if (err.Length == 0)
        {
            responseBody = summary;
            status = 200;
            print("TM2020Bridge: new map requested");
        }
        else
        {
            responseBody = '{"error":"' + JsonEscape(err) + '"}';
            status = 400;
        }
    }
    else if (method == "POST" && path == "/map/blocks")
    {
        string summary = "";
        string err = PlaceMapBlocks(body, summary);
        if (err.Length == 0)
        {
            responseBody = summary;
            status = 200;
        }
        else
        {
            responseBody = '{"error":"' + JsonEscape(err) + '"}';
            status = 400;
        }
    }
    else if (method == "POST" && path == "/map/blocks/remove")
    {
        string summary = "";
        string err = RemoveMapBlocks(body, summary);
        if (err.Length == 0)
        {
            responseBody = summary;
            status = 200;
        }
        else
        {
            responseBody = '{"error":"' + JsonEscape(err) + '"}';
            status = 400;
        }
    }
    else if (method == "POST" && path == "/map/save")
    {
        string summary = "";
        string err = SaveMapAs(body, summary);
        if (err.Length == 0)
        {
            responseBody = summary;
            status = 200;
            print("TM2020Bridge: map saved");
        }
        else
        {
            responseBody = '{"error":"' + JsonEscape(err) + '"}';
            status = 400;
        }
    }
    else if (method == "POST" && path == "/map/open")
    {
        string summary = "";
        string err = OpenMapInEditor(body, summary);
        if (err.Length == 0)
        {
            responseBody = summary;
            status = 200;
            print("TM2020Bridge: opening map in editor");
        }
        else
        {
            responseBody = '{"error":"' + JsonEscape(err) + '"}';
            status = 400;
        }
    }
    else if (method == "POST" && path == "/manialink/preview")
    {
        if (body.Length == 0)
        {
            responseBody = '{"error":"empty request body"}';
            status = 400;
        }
        else
        {
            string err = ApplyManialinkPreview(body);
            if (err.Length == 0)
            {
                g_currentManialinkXml = body;
                responseBody = '{"preview":true,"chars":' + body.Length + '}';
                status = 200;
                print("TM2020Bridge: ManiaLink preview updated (" + body.Length + " chars)");
            }
            else
            {
                responseBody = '{"error":"' + JsonEscape(err) + '"}';
                status = 400;
            }
        }
    }
    else if (method == "POST" && path == "/manialink/clear")
    {
        string err = ApplyManialinkPreview("");
        if (err.Length == 0)
        {
            g_currentManialinkXml = "";
            responseBody = '{"preview":false}';
            status = 200;
            print("TM2020Bridge: ManiaLink preview cleared");
        }
        else
        {
            responseBody = '{"error":"' + JsonEscape(err) + '"}';
            status = 400;
        }
    }
    else if (method == "GET" && path == "/manialink/current")
    {
        if (g_currentManialinkXml.Length == 0)
        {
            status = 404;
        }
        else
        {
            SendResponse(client, 200, "application/xml", g_currentManialinkXml);
            client.Close();
            return;
        }
    }
    else if (method == "GET" && path == "/layers")
    {
        responseBody = GetUiLayersJson();
        status = 200;
    }
    else if (method == "GET" && path.Length > 8 && path.SubStr(0, 8) == "/layers/")
    {
        string indexText = path.SubStr(8, path.Length - 8);
        string layerXml = "";
        string err = GetUiLayerXml(indexText, layerXml);
        if (err.Length > 0)
        {
            responseBody = '{"error":"' + JsonEscape(err) + '"}';
            status = 404;
        }
        else
        {
            SendResponse(client, 200, "application/xml", layerXml);
            client.Close();
            return;
        }
    }
    else if (method == "GET" && path == "/manialink/events")
    {
        responseBody = GetRecentManialinkEventsJson();
        status = 200;
    }
    else if (method == "POST" && path == "/manialink/events")
    {
        if (body.Length == 0)
        {
            responseBody = '{"error":"empty request body"}';
            status = 400;
        }
        else
        {
            AddRecentManialinkEvent(body);
            responseBody = '{"recorded":true,"events":' + g_recentManialinkEvents.Length + '}';
            status = 200;
        }
    }
    else if (method == "POST" && path == "/manialink/events/clear")
    {
        g_recentManialinkEvents.Resize(0);
        responseBody = '{"events":0}';
        status = 200;
    }
    else if (method == "GET" && path == "/interface-designer/selection")
    {
        auto designer = GetInterfaceDesigner();
        if (designer is null)
        {
            responseBody = '{"error":"interface designer not open"}';
            status = 400;
        }
        else
        {
            responseBody = GetInterfaceDesignerSelectionJson(designer);
            status = 200;
        }
    }
    else
    {
        responseBody = '{"error":"not found","method":"' + JsonEscape(method) + '","path":"' + JsonEscape(path) + '"}';
    }

    SendResponse(client, status, "application/json", responseBody);
    client.Close();
}

string ReadHttpRequest(Net::Socket@ client)
{
    string request = "";
    int waited = 0;

    while (!client.IsHungUp() && waited < 200)
    {
        int available = int(client.Available());
        if (available > 0)
        {
            request += client.ReadRaw(available);

            int headerEnd = request.IndexOf("\r\n\r\n");
            if (headerEnd >= 0)
            {
                int contentLength = GetContentLength(request.SubStr(0, headerEnd));
                int bodyStart = headerEnd + 4;
                if (int(request.Length) >= bodyStart + contentLength)
                    break;
            }
        }

        waited++;
        yield();
    }

    return request;
}

int GetContentLength(const string &in headers)
{
    string lower = ToLowerAscii(headers);
    string needle = "content-length:";
    int idx = lower.IndexOf(needle);
    if (idx < 0)
        return 0;

    int valueStart = idx + int(needle.Length);
    while (valueStart < int(headers.Length) && (headers.SubStr(valueStart, 1) == " " || headers.SubStr(valueStart, 1) == "\t"))
        valueStart++;

    int valueEnd = valueStart;
    while (valueEnd < int(headers.Length))
    {
        string ch = headers.SubStr(valueEnd, 1);
        if (!IsDigit(ch))
            break;
        valueEnd++;
    }

    if (valueEnd <= valueStart)
        return 0;

    return Text::ParseInt(headers.SubStr(valueStart, valueEnd - valueStart));
}

string GetRequestBody(const string &in request)
{
    int headerEnd = request.IndexOf("\r\n\r\n");
    if (headerEnd < 0)
        return "";

    return request.SubStr(headerEnd + 4);
}

string AutosaveMapEditor()
{
    auto editor = cast<CGameCtnEditorFree>(GetApp().Editor);
    if (editor is null)
        return "map editor not open";

    auto pmt = editor.PluginMapType;
    if (pmt is null)
        return "PluginMapType not available";

    pmt.AutoSave();
    return "";
}

// Map creation / editing -------------------------------------------------------

// Defaults match a plain TM2020 Stadium map. They are request fields, not constants baked
// into the endpoint, so a caller can correct one without a plugin reload.
const string DefaultEnvironment = "Stadium";
const string DefaultDecoration = "48x48Screen155Day";
const string DefaultMapType = "TrackMania\\TM_Race";

// The editor loads asynchronously, so /map/new returns before it is necessarily up.
// Wait only briefly here and let the caller poll /status for the rest.
const int NewMapEditorWaitFrames = 120;

// Height range scanned when a block placement asks for an automatic ground level.
const int MaxGroundScanY = 40;

// SaveMap is asynchronous too. Stay under the .NET client's 5s HTTP timeout.
const int SaveMapWaitFrames = 180;

string CreateNewMap(const string &in body, string &out summary)
{
    summary = "";

    if (GetApp().Editor !is null || GetApp().EditorBase !is null)
        return "an editor is already open; leave it before creating a new map";

    auto app = cast<CGameManiaPlanet>(GetApp());
    if (app is null)
        return "app is not a ManiaPlanet-derived app";

    auto titleApi = app.ManiaTitleControlScriptAPI;
    if (titleApi is null)
        return "ManiaTitleControlScriptAPI not available";

    auto options = ParseJsonObject(body);
    string environment = JsonString(options, "environment", DefaultEnvironment);
    string decoration = JsonString(options, "decoration", DefaultDecoration);
    string mapType = JsonString(options, "map_type", DefaultMapType);
    string mod = JsonString(options, "mod", "");
    string playerModel = JsonString(options, "player_model", "");
    bool simpleEditor = JsonBool(options, "use_simple_editor", false);

    titleApi.EditNewMap2(environment, decoration, mod, playerModel, mapType, simpleEditor, "", "");

    int waited = WaitForMapEditor(NewMapEditorWaitFrames);
    bool editorOpen = (cast<CGameCtnEditorFree>(GetApp().Editor) !is null);

    summary = '{"created":true'
        + ',"map_editor":' + (editorOpen ? 'true' : 'false')
        + ',"environment":"' + JsonEscape(environment) + '"'
        + ',"decoration":"' + JsonEscape(decoration) + '"'
        + ',"map_type":"' + JsonEscape(mapType) + '"'
        + ',"waited_frames":' + waited
        + '}';

    return "";
}

int WaitForMapEditor(int maxFrames)
{
    int waited = 0;
    while (waited < maxFrames && cast<CGameCtnEditorFree>(GetApp().Editor) is null)
    {
        waited++;
        yield();
    }

    return waited;
}

int WaitForMapFileName(CGameEditorPluginMap@ pmt, int maxFrames)
{
    int waited = 0;
    while (waited < maxFrames && ToJsonString(pmt.MapFileName).Length == 0)
    {
        waited++;
        yield();
    }

    return waited;
}

string PlaceMapBlocks(const string &in body, string &out summary)
{
    summary = "";

    auto editor = cast<CGameCtnEditorFree>(GetApp().Editor);
    if (editor is null)
        return "map editor not open";

    auto pmt = editor.PluginMapType;
    if (pmt is null)
        return "PluginMapType not available";

    auto options = ParseJsonObject(body);
    if (options is null || !options.HasKey("blocks"))
        return "request body needs a \"blocks\" array";

    auto blocks = options["blocks"];
    if (blocks is null || blocks.GetType() != Json::Type::Array)
        return "\"blocks\" must be an array";

    string results = "";
    int placedCount = 0;

    for (int i = 0; i < int(blocks.Length); i++)
    {
        auto entry = blocks[i];
        string name = JsonString(entry, "name", "");
        int x = JsonInt(entry, "x", 0);
        int y = JsonInt(entry, "y", -1);
        int z = JsonInt(entry, "z", 0);
        string dirName = JsonString(entry, "dir", "North");

        bool knownDir = false;
        auto dir = CardinalDirectionFromName(dirName, knownDir);

        string failure = "";
        int resolvedY = y;
        bool placed = false;

        if (name.Length == 0)
        {
            failure = "block name missing";
        }
        else if (!knownDir)
        {
            failure = "unknown direction: " + dirName;
        }
        else
        {
            auto model = pmt.GetBlockModelFromName(name);
            if (model is null)
            {
                failure = "unknown block model";
            }
            else
            {
                if (resolvedY < 0)
                    resolvedY = FindGroundY(pmt, model, x, z, dir);

                if (resolvedY < 0)
                    failure = "no placeable ground level found at this x/z";
                else if (!pmt.PlaceBlock(model, int3(x, resolvedY, z), dir))
                    failure = "PlaceBlock refused this coordinate";
                else
                    placed = true;
            }
        }

        if (placed)
            placedCount++;

        if (results.Length > 0)
            results += ',';

        results += '{"name":"' + JsonEscape(name) + '"'
            + ',"placed":' + (placed ? 'true' : 'false')
            + ',"x":' + x + ',"y":' + resolvedY + ',"z":' + z
            + ',"dir":"' + JsonEscape(dirName) + '"'
            + ',"error":"' + JsonEscape(failure) + '"}';
    }

    summary = '{"requested":' + blocks.Length + ',"placed":' + placedCount + ',"blocks":[' + results + ']}';
    return "";
}

// Removes grid blocks, and optionally probes what the engine does to a handle it just
// deleted. The probe answers three of the questions in HANDOFF-editor-facts.md - E2, E4 and
// E5 - which is why it reports engine state rather than just a success flag.
//
// The probe reads members off a block handle AFTER the engine has removed it. That is the
// question being asked, and it is also the thing that could take the client down, so it is
// opt-in per request and never runs unless asked for.
string RemoveMapBlocks(const string &in body, string &out summary)
{
    summary = "";

    auto editor = cast<CGameCtnEditorFree>(GetApp().Editor);
    if (editor is null)
        return "map editor not open";

    auto pmt = editor.PluginMapType;
    if (pmt is null)
        return "PluginMapType not available";

    auto options = ParseJsonObject(body);
    if (options is null || !options.HasKey("blocks"))
        return "request body needs a \"blocks\" array";

    auto blocks = options["blocks"];
    if (blocks is null || blocks.GetType() != Json::Type::Array)
        return "\"blocks\" must be an array";

    bool probe = JsonBool(options, "probe", false);

    string results = "";
    int removedCount = 0;

    for (int i = 0; i < int(blocks.Length); i++)
    {
        auto entry = blocks[i];
        int x = JsonInt(entry, "x", 0);
        int y = JsonInt(entry, "y", -1);
        int z = JsonInt(entry, "z", 0);

        // A negative y means "whatever is stacked here", mirroring the placement scan.
        int resolvedY = y;
        if (resolvedY < 0)
            resolvedY = FindOccupiedY(pmt, x, z);

        string failure = "";
        bool removed = false;
        string blockName = "";
        bool infoNull = true;
        string probeJson = "";

        if (resolvedY < 0)
        {
            failure = "no block found in this column";
        }
        else
        {
            auto held = pmt.GetBlock(int3(x, resolvedY, z));
            if (held is null)
            {
                failure = "no block at this coordinate";
            }
            else
            {
                // IdName on the block is an internal numeric id ("#38241"), not the model
                // name. The readable name lives on BlockInfo.
                blockName = (held.BlockInfo !is null) ? held.BlockInfo.IdName : held.IdName;
                infoNull = (held.BlockInfo is null);

                removed = pmt.RemoveBlock(int3(x, resolvedY, z));
                if (!removed)
                {
                    failure = "RemoveBlock refused this coordinate";
                }
                else if (probe)
                {
                    auto after = pmt.GetBlock(int3(x, resolvedY, z));

                    probeJson = ',"probe":{"get_block_null":' + (after is null ? 'true' : 'false')
                        + ',"same_handle":' + ((after !is null && after is held) ? 'true' : 'false')
                        + ',"held_units_e":' + held.BlockUnitsE.Length
                        + ',"held_units":' + held.BlockUnits.Length
                        + ',"held_info_null":' + (held.BlockInfo is null ? 'true' : 'false')
                        + '}';
                }
            }
        }

        if (removed)
            removedCount++;

        if (results.Length > 0)
            results += ',';

        results += '{"x":' + x + ',"y":' + resolvedY + ',"z":' + z
            + ',"removed":' + (removed ? 'true' : 'false')
            + ',"name":"' + JsonEscape(blockName) + '"'
            + ',"block_info_null":' + (infoNull ? 'true' : 'false')
            + ',"error":"' + JsonEscape(failure) + '"'
            + probeJson
            + '}';
    }

    summary = '{"requested":' + blocks.Length + ',"removed":' + removedCount
        + ',"probed":' + (probe ? 'true' : 'false')
        + ',"blocks":[' + results + ']}';

    return "";
}

int FindOccupiedY(CGameEditorPluginMap@ pmt, int x, int z)
{
    for (int y = 0; y < MaxGroundScanY; y++)
    {
        if (pmt.GetBlock(int3(x, y, z)) !is null)
            return y;
    }

    return -1;
}

int FindGroundY(CGameEditorPluginMap@ pmt, CGameCtnBlockInfo@ model, int x, int z, CGameEditorPluginMap::ECardinalDirections dir)
{
    for (int y = 0; y < MaxGroundScanY; y++)
    {
        if (pmt.CanPlaceBlock(model, int3(x, y, z), dir, true, 0))
            return y;
    }

    return -1;
}

CGameEditorPluginMap::ECardinalDirections CardinalDirectionFromName(const string &in name, bool &out known)
{
    known = true;

    string lowered = ToLowerAscii(name);
    if (lowered == "" || lowered == "north")
        return CGameEditorPluginMap::ECardinalDirections::North;
    if (lowered == "east")
        return CGameEditorPluginMap::ECardinalDirections::East;
    if (lowered == "south")
        return CGameEditorPluginMap::ECardinalDirections::South;
    if (lowered == "west")
        return CGameEditorPluginMap::ECardinalDirections::West;

    known = false;
    return CGameEditorPluginMap::ECardinalDirections::North;
}

string SaveMapAs(const string &in body, string &out summary)
{
    summary = "";

    auto editor = cast<CGameCtnEditorFree>(GetApp().Editor);
    if (editor is null)
        return "map editor not open";

    auto pmt = editor.PluginMapType;
    if (pmt is null)
        return "PluginMapType not available";

    auto options = ParseJsonObject(body);
    string fileName = JsonString(options, "file_name", "");
    if (fileName.Length == 0)
        return "request body needs a non-empty \"file_name\"";

    // SaveMap returns void and finishes asynchronously, so the call returning proves
    // nothing. Wait for the editor to report a file name and answer with that instead.
    pmt.SaveMap(fileName);

    int waited = WaitForMapFileName(pmt, SaveMapWaitFrames);
    string savedFileName = ToJsonString(pmt.MapFileName);

    summary = '{"saved":' + (savedFileName.Length > 0 ? 'true' : 'false')
        + ',"requested_file_name":"' + JsonEscape(fileName) + '"'
        + ',"map_name":"' + JsonEscape(ToJsonString(pmt.MapName)) + '"'
        + ',"map_file_name":"' + JsonEscape(savedFileName) + '"'
        + ',"waited_frames":' + waited
        + '}';

    return "";
}

// Opens a map that already exists on disk. This is how a map written outside the game -
// by the .Map.Gbx writer, which can place free blocks the editor API cannot - gets loaded
// back in to be looked at.
string OpenMapInEditor(const string &in body, string &out summary)
{
    summary = "";

    if (GetApp().Editor !is null || GetApp().EditorBase !is null)
        return "an editor is already open; leave it before opening another map";

    auto app = cast<CGameManiaPlanet>(GetApp());
    if (app is null)
        return "app is not a ManiaPlanet-derived app";

    auto titleApi = app.ManiaTitleControlScriptAPI;
    if (titleApi is null)
        return "ManiaTitleControlScriptAPI not available";

    auto options = ParseJsonObject(body);
    string fileName = JsonString(options, "file_name", "");
    if (fileName.Length == 0)
        return "request body needs a non-empty \"file_name\"";

    titleApi.EditMap(fileName, "", "");

    int waited = WaitForMapEditor(NewMapEditorWaitFrames);
    bool editorOpen = (cast<CGameCtnEditorFree>(GetApp().Editor) !is null);

    summary = '{"opening":true'
        + ',"map_editor":' + (editorOpen ? 'true' : 'false')
        + ',"file_name":"' + JsonEscape(fileName) + '"'
        + ',"waited_frames":' + waited
        + '}';

    return "";
}

// Json request helpers ---------------------------------------------------------

Json::Value@ ParseJsonObject(const string &in body)
{
    int start = 0;
    while (start < int(body.Length) && IsWhitespace(body.SubStr(start, 1)))
        start++;

    if (start >= int(body.Length) || body.SubStr(start, 1) != "{")
        return null;

    auto parsed = Json::Parse(body.SubStr(start));
    if (parsed is null || parsed.GetType() != Json::Type::Object)
        return null;

    return parsed;
}

string JsonString(Json::Value@ obj, const string &in key, const string &in fallback)
{
    if (obj is null || obj.GetType() != Json::Type::Object || !obj.HasKey(key))
        return fallback;

    auto value = obj[key];
    if (value is null || value.GetType() != Json::Type::String)
        return fallback;

    string result = value;
    return result;
}

int JsonInt(Json::Value@ obj, const string &in key, int fallback)
{
    if (obj is null || obj.GetType() != Json::Type::Object || !obj.HasKey(key))
        return fallback;

    auto value = obj[key];
    if (value is null || value.GetType() != Json::Type::Number)
        return fallback;

    int result = value;
    return result;
}

bool JsonBool(Json::Value@ obj, const string &in key, bool fallback)
{
    if (obj is null || obj.GetType() != Json::Type::Object || !obj.HasKey(key))
        return fallback;

    auto value = obj[key];
    if (value is null || value.GetType() != Json::Type::Boolean)
        return fallback;

    bool result = value;
    return result;
}

string ApplyManialinkPreview(const string &in xml)
{
    auto editor = cast<CGameCtnEditorFree>(GetApp().Editor);
    if (editor is null)
        return "map editor not open";

    auto pmt = editor.PluginMapType;
    if (pmt is null)
        return "PluginMapType not available";

    pmt.ManialinkText = xml;
    return "";
}

CGameEditorManialink@ GetInterfaceDesigner()
{
    auto designer = cast<CGameEditorManialink>(GetApp().Editor);
    if (designer !is null)
        return designer;

    return cast<CGameEditorManialink>(GetApp().EditorBase);
}

void AddRecentManialinkEvent(const string &in body)
{
    g_recentManialinkEvents.InsertLast(body);
    while (g_recentManialinkEvents.Length > MaxRecentManialinkEvents)
        g_recentManialinkEvents.RemoveAt(0);
}

string GetRecentManialinkEventsJson()
{
    string json = '{"events":[';
    for (uint i = 0; i < g_recentManialinkEvents.Length; i++)
    {
        if (i > 0)
            json += ',';

        json += '{"index":' + i + ',"body":"' + JsonEscape(g_recentManialinkEvents[i]) + '"}';
    }

    return json + ']}';
}

string GetInterfaceDesignerSelectionJson(CGameEditorManialink@ designer)
{
    return '{"open":true'
        + ',"control_id":"' + JsonEscape(ToJsonString(designer.ControlId)) + '"'
        + ',"class":"' + JsonEscape(ToJsonString(designer.Class)) + '"'
        + ',"text":"' + JsonEscape(ToJsonString(designer.Text)) + '"'
        + ',"text_id":"' + JsonEscape(ToJsonString(designer.TextId)) + '"'
        + ',"action":"' + JsonEscape(ToJsonString(designer.Action)) + '"'
        + ',"pos":{"x":"' + JsonEscape(ToJsonString(designer.PosnX)) + '","y":"' + JsonEscape(ToJsonString(designer.PosnY)) + '","z":"' + JsonEscape(ToJsonString(designer.PosnZ)) + '"}'
        + ',"size":{"width":"' + JsonEscape(ToJsonString(designer.SizenWidth)) + '","height":"' + JsonEscape(ToJsonString(designer.SizenHeight)) + '"}'
        + ',"scale":"' + JsonEscape(ToJsonString(designer.Scale)) + '"'
        + ',"rotation":"' + JsonEscape(ToJsonString(designer.Rot)) + '"'
        + ',"align":{"h":"' + JsonEscape(ToJsonString(designer.Halign)) + '","v":"' + JsonEscape(ToJsonString(designer.Valign)) + '"}'
        + ',"style":"' + JsonEscape(ToJsonString(designer.Style)) + '"'
        + ',"substyle":"' + JsonEscape(ToJsonString(designer.Substyle)) + '"'
        + ',"text_style":"' + JsonEscape(ToJsonString(designer.StyleText)) + '"'
        + ',"text_size":"' + JsonEscape(ToJsonString(designer.TextSize)) + '"'
        + ',"text_font":"' + JsonEscape(ToJsonString(designer.TextFont)) + '"'
        + ',"image":"' + JsonEscape(ToJsonString(designer.Image)) + '"'
        + ',"image_focus":"' + JsonEscape(ToJsonString(designer.ImageFocus)) + '"'
        + ',"url":"' + JsonEscape(ToJsonString(designer.Url)) + '"'
        + ',"manialink":"' + JsonEscape(ToJsonString(designer.Manialink)) + '"'
        + ',"colors":{"text":"' + JsonEscape(ToJsonString(designer.TextColor)) + '","bg":"' + JsonEscape(ToJsonString(designer.BgColor)) + '","focus1":"' + JsonEscape(ToJsonString(designer.FocusAreaColor1)) + '","focus2":"' + JsonEscape(ToJsonString(designer.FocusAreaColor2)) + '"}'
        + ',"hidden":' + (designer.ButtonHidden ? 'true' : 'false')
        + ',"autoscale":' + (designer.ButtonAutoscale ? 'true' : 'false')
        + ',"autoscale_fixed_width":' + (designer.ButtonAutoscaleFixedWidth ? 'true' : 'false')
        + ',"translate":' + (designer.ButtonTranslate ? 'true' : 'false')
        + ',"frame_layout_editor":' + (designer.FrameLayoutEditor !is null ? 'true' : 'false')
        + '}';
}

string ToJsonString(const wstring &in value)
{
    return Text::StripFormatCodes(value);
}

void SendResponse(Net::Socket@ client, int status, const string &in contentType, const string &in body)
{
    string reason = status == 200 ? "OK" : status == 400 ? "Bad Request" : status == 404 ? "Not Found" : "Error";
    string response = "HTTP/1.1 " + status + " " + reason + "\r\n"
        + "Content-Type: " + contentType + "\r\n"
        + "Content-Length: " + body.Length + "\r\n"
        + "Connection: close\r\n"
        + "\r\n"
        + body;

    client.WriteRaw(response);
}

// Server-sent HUD layers, read off the local client.
//
// This is the only way to see what a Nadeo UI module actually renders: the module scripts
// live in the title pack and are not published, but ManialinkPageUtf8 is the XML they
// produce, live. AttachId is Unassigned on every layer in Trackmania 2020, so the
// <manialink> opening tag is the identifier (see _W7Probe).
//
// Requires being connected to a server. ClientManiaAppPlayground is null in the menus.
CGameManiaAppPlayground@ GetPlaygroundManiaApp()
{
    auto app = GetApp();
    if (app is null) return null;

    auto network = cast<CTrackManiaNetwork>(app.Network);
    if (network is null) return null;

    return network.ClientManiaAppPlayground;
}

string LayerTypeName(CGameUILayer::EUILayerType type)
{
    switch (type) {
        case CGameUILayer::EUILayerType::Normal:            return "Normal";
        case CGameUILayer::EUILayerType::ScoresTable:       return "ScoresTable";
        case CGameUILayer::EUILayerType::ScreenIn3d:        return "ScreenIn3d";
        case CGameUILayer::EUILayerType::AltMenu:           return "AltMenu";
        case CGameUILayer::EUILayerType::Markers:           return "Markers";
        case CGameUILayer::EUILayerType::CutScene:          return "CutScene";
        case CGameUILayer::EUILayerType::InGameMenu:        return "InGameMenu";
        case CGameUILayer::EUILayerType::EditorPlugin:      return "EditorPlugin";
        case CGameUILayer::EUILayerType::ManiaplanetPlugin: return "ManiaplanetPlugin";
        case CGameUILayer::EUILayerType::ManiaplanetMenu:   return "ManiaplanetMenu";
        case CGameUILayer::EUILayerType::LoadingScreen:     return "LoadingScreen";
    }
    return "Unknown(" + int(type) + ")";
}

// The <manialink> opening tag, which carries the id attribute. Short enough to list.
string ManialinkTag(const string &in xml)
{
    if (xml.Length == 0) return "";

    int at = xml.IndexOf("<manialink");
    if (at < 0) return "";

    uint remaining = xml.Length - uint(at);
    uint take = remaining;
    if (take > 160) take = 160;
    return xml.SubStr(uint(at), take);
}

string GetUiLayersJson()
{
    auto maniaApp = GetPlaygroundManiaApp();
    if (maniaApp is null)
    {
        return '{"connected":false,"layers":[],"error":"not connected to a server"}';
    }

    string json = '{"connected":true,"layers":[';
    for (uint i = 0; i < maniaApp.UILayers.Length; i++)
    {
        auto layer = maniaApp.UILayers[i];
        if (layer is null) continue;

        string xml = layer.ManialinkPageUtf8;
        if (i > 0) json += ",";
        json += '{"index":' + i
            + ',"attachId":"' + JsonEscape(layer.AttachId) + '"'
            + ',"type":"' + JsonEscape(LayerTypeName(layer.Type)) + '"'
            + ',"visible":' + (layer.IsVisible ? "true" : "false")
            + ',"animInProgress":' + (layer.AnimInProgress ? "true" : "false")
            + ',"scriptRunning":' + (layer.IsLocalPageScriptRunning ? "true" : "false")
            + ',"xmlLength":' + xml.Length
            + ',"tag":"' + JsonEscape(ManialinkTag(xml)) + '"'
            + '}';
    }
    json += "]}";
    return json;
}

// Returns "" on success and writes the XML to _xml, otherwise an error string.
string GetUiLayerXml(const string &in indexText, string &out _xml)
{
    _xml = "";

    auto maniaApp = GetPlaygroundManiaApp();
    if (maniaApp is null) return "not connected to a server";

    for (uint i = 0; i < indexText.Length; i++)
    {
        if (!IsDigit(indexText.SubStr(i, 1))) return "layer index must be a number";
    }
    if (indexText.Length == 0) return "layer index must be a number";

    uint index = Text::ParseUInt(indexText);
    if (index >= maniaApp.UILayers.Length) return "no layer at index " + index;

    auto layer = maniaApp.UILayers[index];
    if (layer is null) return "no layer at index " + index;

    _xml = layer.ManialinkPageUtf8;
    return "";
}

string HexByte(uint8 b)
{
    const string digits = "0123456789abcdef";
    return digits.SubStr(b >> 4, 1) + digits.SubStr(b & 0x0F, 1);
}

string JsonEscape(const string &in value)
{
    string escaped = "";
    for (uint i = 0; i < value.Length; i++)
    {
        string ch = value.SubStr(i, 1);
        if (ch == "\\")
            escaped += "\\\\";
        else if (ch == "\"")
            escaped += "\\\"";
        else if (ch == "\r")
            escaped += "\\r";
        else if (ch == "\n")
            escaped += "\\n";
        else if (ch == "\t")
            escaped += "\\t";
        // Nadeo's ManiaLink is indented with tabs and carries the odd other control
        // byte. JSON forbids those raw, and one of them makes the whole response
        // unparseable, which reaches the MCP client as "bridge unreachable".
        else if (ch < " ")
            escaped += "\\u00" + HexByte(value[i]);
        else
            escaped += ch;
    }

    return escaped;
}

string ToLowerAscii(const string &in value)
{
    string lowered = "";
    for (uint i = 0; i < value.Length; i++)
    {
        string ch = value.SubStr(i, 1);
        if (ch == "A") lowered += "a";
        else if (ch == "B") lowered += "b";
        else if (ch == "C") lowered += "c";
        else if (ch == "D") lowered += "d";
        else if (ch == "E") lowered += "e";
        else if (ch == "F") lowered += "f";
        else if (ch == "G") lowered += "g";
        else if (ch == "H") lowered += "h";
        else if (ch == "I") lowered += "i";
        else if (ch == "J") lowered += "j";
        else if (ch == "K") lowered += "k";
        else if (ch == "L") lowered += "l";
        else if (ch == "M") lowered += "m";
        else if (ch == "N") lowered += "n";
        else if (ch == "O") lowered += "o";
        else if (ch == "P") lowered += "p";
        else if (ch == "Q") lowered += "q";
        else if (ch == "R") lowered += "r";
        else if (ch == "S") lowered += "s";
        else if (ch == "T") lowered += "t";
        else if (ch == "U") lowered += "u";
        else if (ch == "V") lowered += "v";
        else if (ch == "W") lowered += "w";
        else if (ch == "X") lowered += "x";
        else if (ch == "Y") lowered += "y";
        else if (ch == "Z") lowered += "z";
        else lowered += ch;
    }

    return lowered;
}

bool IsWhitespace(const string &in ch)
{
    return ch == " " || ch == "\t" || ch == "\r" || ch == "\n";
}

bool IsDigit(const string &in ch)
{
    return ch == "0" || ch == "1" || ch == "2" || ch == "3" || ch == "4"
        || ch == "5" || ch == "6" || ch == "7" || ch == "8" || ch == "9";
}
