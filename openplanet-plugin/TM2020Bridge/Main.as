// TM2020Bridge - local OpenPlanet HTTP bridge for MCP clients.
//
// Endpoints:
//   GET  /status
//   POST /save
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

bool IsDigit(const string &in ch)
{
    return ch == "0" || ch == "1" || ch == "2" || ch == "3" || ch == "4"
        || ch == "5" || ch == "6" || ch == "7" || ch == "8" || ch == "9";
}
