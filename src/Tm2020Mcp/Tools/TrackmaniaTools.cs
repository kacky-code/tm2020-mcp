using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Tm2020Mcp.EmojiChat;
using Tm2020Mcp.EditorBridge;
using Tm2020Mcp.Manialinks;
using Tm2020Mcp.Maps;

namespace Tm2020Mcp.Tools;

[McpServerToolType]
public sealed class TrackmaniaTools
{
    private readonly OpenPlanetClient _client;
    private readonly EmojiChatAnalyzer _emojiChat = new();
    private readonly ManialinkInspector _manialinks = new();
    private readonly ManialinkVideoProbeBuilder _videoProbe = new();
    private readonly ManialinkValidator _validator = new();
    private readonly MapGbxReader _mapReader = new();
    private readonly BlockDirectionAnalyzer _blockDirections = new();
    private readonly TrackVerifier _trackVerifier = new();
    private readonly Lazy<BlockConnectionModel> _connections = new(BlockConnectionModel.LoadBundled);
    private readonly MotifLearner _motifs = new();
    private readonly MotifStamper _motifStamper = new();
    private readonly MapGbxWriter _mapWriter = new();
    private readonly ManialinkMediaProbe _mediaProbe = new(new HttpClient { Timeout = TimeSpan.FromSeconds(10) });

    public TrackmaniaTools(OpenPlanetClient client)
    {
        _client = client;
    }

    [McpServerTool(Name = "set_openplanet_bridge_url"), Description("Configure the TM2020 OpenPlanet bridge base URL. Defaults to http://127.0.0.1:29100.")]
    public string SetOpenPlanetBridgeUrl(
        [Description("Bridge base URL, for example http://127.0.0.1:29100.")] string url)
    {
        try
        {
            _client.SetBaseUrl(url);
            return $"OpenPlanet bridge URL set to: {url.Trim().TrimEnd('/')}";
        }
        catch (ArgumentException ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_tm2020_status"), Description("Return current TM2020 OpenPlanet bridge and editor status.")]
    public async Task<string> GetTm2020Status()
    {
        var status = await _client.GetStatusAsync();
        if (status is null)
            return "OpenPlanet bridge not reachable. Check that Trackmania 2020 is running and the TM2020Bridge plugin is loaded.";

        return $"running={status.Running}, editor_open={status.EditorOpen}, map_editor={status.MapEditor}, interface_designer={status.InterfaceDesigner}, module_editor={status.ModuleEditor}, manialink_preview={status.ManialinkPreview}";
    }

    [McpServerTool(Name = "list_ui_layers"), Description("List the server-sent HUD layers on the local TM2020 client. This is how to see what a Nadeo UI module actually renders: the module scripts are not published, but the ManiaLink they produce is readable live. Requires the game running and connected to a server.")]
    public async Task<string> ListUiLayers()
    {
        var result = await _client.GetUiLayersAsync();
        if (result is null)
            return "Bridge unreachable. Is Trackmania running with the TM2020Bridge plugin loaded?";

        if (!result.Connected)
            return $"Not connected to a server ({result.Error ?? "no playground"}). HUD layers only exist in a playground, so join a server and try again.";

        if (result.Layers.Count == 0)
            return "Connected, but no HUD layers are present.";

        var lines = result.Layers.Select(layer =>
            $"[{layer.Index}] {layer.Type} visible={layer.Visible} script={layer.ScriptRunning} xml={layer.XmlLength}B {layer.Tag}");

        return $"{result.Layers.Count} layers. AttachId is Unassigned on every layer in TM2020, so the tag identifies them.\n"
            + string.Join("\n", lines)
            + "\nUse get_ui_layer_xml with an index to read one layer's ManiaLink.";
    }

    [McpServerTool(Name = "get_ui_layer_xml"), Description("Return one HUD layer's ManiaLink XML by index, as listed by list_ui_layers. Use it to read how a Nadeo UI module is built rather than guessing at its unpublished source.")]
    public async Task<string> GetUiLayerXml(
        [Description("Layer index from list_ui_layers.")] int index)
    {
        if (index < 0)
            return "Layer index must be zero or greater.";

        var xml = await _client.GetUiLayerXmlAsync(index);
        if (xml is null)
            return $"No layer at index {index}, or the bridge is unreachable. Run list_ui_layers first: indexes shift as layers come and go.";

        return xml;
    }

    [McpServerTool(Name = "preview_manialink_xml"), Description("Push raw ManiaLink XML into TM2020 through the OpenPlanet TM2020Bridge plugin.")]
    public async Task<string> PreviewManialinkXml(
        [Description("Full ManiaLink XML.")] string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return "Error: XML is empty.";

        var status = await _client.GetStatusAsync();
        if (status is null)
            return "OpenPlanet bridge not reachable. Check that TM2020Bridge is loaded.";

        if (!status.MapEditor)
            return "OpenPlanet bridge is running, but the map editor is not active. ManiaLink preview currently targets the map editor PluginMapType.ManialinkText path.";

        var result = await _client.PreviewManialinkXmlAsync(xml);
        return result.Success
            ? $"OpenPlanet: ManiaLink preview updated ({xml.Length} chars).\n{result.Body}"
            : $"OpenPlanet: ManiaLink preview failed.\n{result.Body}";
    }

    [McpServerTool(Name = "preview_manialink_file"), Description("Read a ManiaLink XML file from disk and push it into TM2020.")]
    public async Task<string> PreviewManialinkFile(
        [Description("Absolute path to a .xml file.")] string path)
    {
        if (!File.Exists(path))
            return $"Error: File does not exist: {path}";

        var xml = await File.ReadAllTextAsync(path);
        return await PreviewManialinkXml(xml);
    }

    [McpServerTool(Name = "clear_manialink_preview"), Description("Clear the current TM2020 ManiaLink XML preview.")]
    public async Task<string> ClearManialinkPreview()
    {
        var result = await _client.ClearManialinkPreviewAsync();
        return result.Success
            ? $"OpenPlanet: ManiaLink preview cleared.\n{result.Body}"
            : $"OpenPlanet: Failed to clear ManiaLink preview.\n{result.Body}";
    }

    [McpServerTool(Name = "autosave_map_editor"), Description("Trigger AutoSave in the current TM2020 map editor via OpenPlanet.")]
    public async Task<string> AutosaveMapEditor()
    {
        var result = await _client.AutosaveMapEditorAsync();
        return result.Success
            ? $"OpenPlanet: map editor autosave triggered.\n{result.Body}"
            : $"OpenPlanet: autosave failed.\n{result.Body}";
    }

    [McpServerTool(Name = "create_map"), Description("Create a new map in the TM2020 editor through the OpenPlanet bridge, optionally place a minimal start/straight/finish track, and optionally save it to disk. The game must be at the main menu with no editor open.")]
    public async Task<string> CreateMap(
        [Description("File name to save the map under, for example \"MCP/dummy.Map.Gbx\". Leave empty to leave the new map open and unsaved.")] string? saveAs = null,
        [Description("Place a minimal start/straight/finish track so the map is more than an empty base.")] bool withTrack = true,
        [Description("Number of straight blocks between start and finish. Ignored when routeLength is set.")] int straightCount = 1,
        [Description("Place a generated turning route of up to this many blocks instead of a straight track. 0 keeps the straight track.")] int routeLength = 0,
        [Description("Seed for the generated route.")] int routeSeed = 1,
        [Description("Chance of taking a curve in a generated route, 0 to 1.")] double turnChance = 0.35,
        [Description("Block palette for a generated route: \"plain\" or \"tricks\".")] string routeStyle = "plain",
        [Description("Direction the track runs: North, East, South, or West.")] string direction = "North",
        [Description("Map coordinate the track starts at, along X.")] int originX = DummyTrackBuilder.DefaultOriginX,
        [Description("Map coordinate the track starts at, along Z.")] int originZ = DummyTrackBuilder.DefaultOriginZ,
        [Description("Engine environment name.")] string environment = "Stadium",
        [Description("Engine decoration name. Picks map size and time of day.")] string decoration = "48x48Screen155Day",
        [Description("Engine map type.")] string mapType = "TrackMania\\TM_Race",
        [Description("How many seconds to wait for the editor to finish loading.")] int waitSeconds = 60)
    {
        if (!DummyTrackBuilder.IsKnownDirection(direction))
            return $"Error: unknown direction '{direction}'. Use North, East, South, or West.";

        if (straightCount < 0)
            return "Error: straightCount cannot be negative.";

        if (!RoutePalette.IsKnownStyle(routeStyle))
            return $"Error: unknown routeStyle '{routeStyle}'. Use 'plain' or 'tricks'.";

        var status = await _client.GetStatusAsync();
        if (status is null)
            return "OpenPlanet bridge not reachable. Check that Trackmania 2020 is running and the TM2020Bridge plugin is loaded.";

        if (status.EditorOpen)
            return "An editor is already open. Go back to the main menu first: the bridge refuses to swap editors so it cannot discard unsaved work.";

        var steps = new List<string>();

        var created = await _client.CreateMapAsync(new NewMapRequest(environment, decoration, mapType));
        if (!created.Success)
            return $"OpenPlanet: map creation failed.\n{created.Body}";

        steps.Add($"Requested new map: environment={environment}, decoration={decoration}, map_type={mapType}.\n{created.Body}");

        if (!await WaitForMapEditorAsync(waitSeconds))
        {
            steps.Add($"The map editor was still not open after {waitSeconds}s, so nothing else ran. Check that the environment, decoration and map type names are ones this client accepts.");
            return string.Join("\n\n", steps);
        }

        steps.Add("Map editor is open.");

        if (withTrack)
        {
            IReadOnlyList<MapBlockPlacement> blocks;

            if (routeLength > 0)
            {
                var plan = BuildRoute(routeSeed, routeLength, turnChance, originX, originZ, direction, routeStyle);
                var check = _trackVerifier.Verify(plan.Blocks.Select(b => b with { Y = 9 }).ToList(), _connections.Value);

                blocks = plan.ToPlacements();
                steps.Add($"Generated a {plan.Blocks.Count}-block route (seed {routeSeed}), finish={plan.HasFinish}, verified={check.Connected}."
                    + (plan.Notes.Count > 0 ? "\n" + string.Join("\n", plan.Notes) : ""));
            }
            else
            {
                blocks = DummyTrackBuilder.Build(originX, originZ, straightCount, direction);
            }

            var placement = await _client.PlaceMapBlocksAsync(blocks);
            steps.Add(placement.Success
                ? $"Placed {blocks.Count} block(s) from ({originX}, {originZ}) heading {direction}. The bridge reports each block it could not place:\n{placement.Body}"
                : $"Block placement failed.\n{placement.Body}");
        }

        if (!string.IsNullOrWhiteSpace(saveAs))
        {
            var saved = await _client.SaveMapAsAsync(saveAs.Trim());
            steps.Add(saved.Success
                ? $"Saved.\n{saved.Body}"
                : $"Save failed.\n{saved.Body}");
        }
        else
        {
            steps.Add("Left open and unsaved. Pass saveAs to write it to disk, or call autosave_map_editor.");
        }

        return string.Join("\n\n", steps);
    }

    private async Task<bool> WaitForMapEditorAsync(int waitSeconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(Math.Max(1, waitSeconds));

        while (true)
        {
            var status = await _client.GetStatusAsync();
            if (status?.MapEditor == true)
                return true;

            if (DateTime.UtcNow >= deadline)
                return false;

            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }

    [McpServerTool(Name = "inspect_map_gbx"), Description("Read a .Map.Gbx from disk and list its blocks with coordinates and directions. Needs no running game. Free blocks carry an absolute rotation instead of a grid direction and are marked as such.")]
    public string InspectMapGbx(
        [Description("Absolute path to a .Map.Gbx file.")] string path,
        [Description("Optional case-insensitive substring to filter block names, for example \"RoadTech\".")] string? nameFilter = null,
        [Description("Maximum number of blocks to list.")] int limit = 200)
    {
        if (!File.Exists(path))
            return $"File not found: {path}";

        MapGbxFile map;
        try
        {
            map = _mapReader.Read(path);
        }
        catch (Exception ex)
        {
            return $"Could not parse {Path.GetFileName(path)}: {ex.GetType().Name} {ex.Message}";
        }

        var blocks = map.Blocks
            .Where(b => nameFilter is null || b.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(b => b.Name, StringComparer.Ordinal)
            .ThenBy(b => b.X)
            .ThenBy(b => b.Z)
            .ToList();

        var report = new StringBuilder();
        report.AppendLine($"{map.FileName}: name={map.MapName} size={map.Size} deco={map.Decoration}");
        report.AppendLine($"{map.Blocks.Count} blocks ({map.FreeBlockCount} free, {map.TiltedBlockCount} tilted), {blocks.Count} matched.");
        report.AppendLine();

        foreach (var block in blocks.Take(limit))
        {
            if (block is { IsFree: true, Position: { } position })
            {
                // A free block's grid coord is <-1, 0, -1>; its geometry is the world
                // position and rotation, and half of a modern map lives there.
                var tilt = block.IsTilted ? " TILTED" : "";
                report.AppendLine($"  {block.Name,-32} FREE pos={position} rot={block.Rotation}{tilt}");
            }
            else
            {
                report.AppendLine($"  {block.Name,-32} <{block.X}, {block.Y}, {block.Z}> dir={block.Direction}");
            }
        }

        if (blocks.Count > limit)
            report.AppendLine($"  ... {blocks.Count - limit} more not shown.");

        return report.ToString().TrimEnd();
    }

    [McpServerTool(Name = "analyze_map_block_directions"), Description("Derive what a block direction means in world coordinates by counting neighbours across a corpus of maps. Point it at a file or a directory. This answers questions the editor API cannot: PlaceBlock reports whether a block fits, never whether the road connects.")]
    public string AnalyzeMapBlockDirections(
        [Description("Absolute path to a .Map.Gbx file or a directory of them (searched recursively).")] string path,
        [Description("Optional case-insensitive substring to filter block names, for example \"RoadTech\".")] string? nameFilter = null,
        [Description("Ignore (block, direction) pairs seen fewer times than this.")] int minimumSamples = 5)
    {
        var paths = MapGbxReader.EnumerateMaps(path);
        if (paths.Count == 0)
            return $"No .Map.Gbx files found at: {path}";

        var maps = new List<IReadOnlyList<MapBlock>>();
        var failures = new List<string>();

        foreach (var mapPath in paths)
        {
            try
            {
                maps.Add(_mapReader.Read(mapPath).Blocks);
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(mapPath)}: {ex.GetType().Name}");
            }
        }

        var observations = _blockDirections.Analyze(maps, nameFilter, minimumSamples);

        var report = new StringBuilder();
        report.AppendLine($"Parsed {maps.Count} of {paths.Count} map(s).");
        if (failures.Count > 0)
            report.AppendLine($"Skipped {failures.Count}: {string.Join(", ", failures.Take(5))}{(failures.Count > 5 ? ", ..." : "")}");
        report.AppendLine();
        report.AppendLine(BlockDirectionAnalyzer.Format(observations));

        return report.ToString().TrimEnd();
    }

    [McpServerTool(Name = "verify_map_track"), Description("Walk a saved .Map.Gbx from its start block to check the road actually connects to the finish. The bridge cannot answer this: a track laid in the wrong direction places cleanly and reports nothing but successes.")]
    public string VerifyMapTrack(
        [Description("Absolute path to a .Map.Gbx file.")] string path)
    {
        if (!File.Exists(path))
            return $"File not found: {path}";

        try
        {
            var map = _mapReader.Read(path);
            return $"{map.FileName}: {TrackVerifier.Format(_trackVerifier.Verify(map.Blocks, _connections.Value))}";
        }
        catch (Exception ex)
        {
            return $"Could not parse {Path.GetFileName(path)}: {ex.GetType().Name} {ex.Message}";
        }
    }

    [McpServerTool(Name = "generate_track_plan"), Description("Generate a connected, turning route from block shapes learned out of real maps, and verify it before it ever touches the game. Returns a block plan; create_map can place it. This makes a track that connects, not a good or hard one.")]
    public string GenerateTrackPlan(
        [Description("Seed. The same seed always gives the same route.")] int seed = 1,
        [Description("Maximum number of blocks between start and finish.")] int length = 12,
        [Description("Chance of taking a curve rather than continuing straight, 0 to 1.")] double turnChance = 0.35,
        [Description("Map coordinate the route starts at, along X.")] int originX = 24,
        [Description("Map coordinate the route starts at, along Z.")] int originZ = 8,
        [Description("Direction the start block faces: North, East, South or West.")] string direction = "North",
        [Description("Block palette: \"plain\" for tech road only, \"tricks\" to mix in turbo, no-engine, reset and ice/bump/water/dirt surfaces.")] string style = "plain")
    {
        if (!DummyTrackBuilder.IsKnownDirection(direction))
            return $"Error: unknown direction '{direction}'. Use North, East, South, or West.";

        if (!RoutePalette.IsKnownStyle(style))
            return $"Error: unknown style '{style}'. Use 'plain' or 'tricks'.";

        var plan = BuildRoute(seed, length, turnChance, originX, originZ, direction, style);
        var verification = _trackVerifier.Verify(
            plan.Blocks.Select(b => b with { Y = 9 }).ToList(),
            _connections.Value);

        var report = new StringBuilder();
        report.AppendLine($"{plan.Blocks.Count} blocks, finish={plan.HasFinish}, verified={verification.Connected}");

        foreach (var note in plan.Notes)
            report.AppendLine($"note: {note}");

        if (!verification.Connected)
            report.AppendLine($"verifier: {verification.Failure}");

        report.AppendLine();
        foreach (var group in plan.Blocks.GroupBy(b => b.Name).OrderByDescending(g => g.Count()))
            report.AppendLine($"  {group.Count(),3}x {group.Key}");

        report.AppendLine();
        foreach (var block in plan.Blocks)
            report.AppendLine($"  {block.Name,-26} <{block.X}, {block.Z}> dir={block.Direction}");

        return report.ToString().TrimEnd();
    }

    [McpServerTool(Name = "learn_map_block_connections"), Description("Learn which neighbouring cells each block connects to, by counting neighbours across a corpus of maps. Writes the model as JSON. This is how the bundled road-family model was produced.")]
    public string LearnMapBlockConnections(
        [Description("Absolute path to a directory of .Map.Gbx files (searched recursively).")] string path,
        [Description("Optional path to write the model JSON to.")] string? outputPath = null,
        [Description("Ignore blocks seen fewer times than this.")] int minimumSamples = 20,
        [Description("Restrict to one block family by name prefix, for example \"Road\".")] string? namePrefix = "Road",
        [Description("Restrict to one block variant. The bridge places variant 0, and a curve's variant changes its shape.")] int variant = 0)
    {
        var paths = MapGbxReader.EnumerateMaps(path);
        if (paths.Count == 0)
            return $"No .Map.Gbx files found at: {path}";

        var maps = new List<IReadOnlyList<MapBlock>>();
        var failed = 0;

        foreach (var mapPath in paths)
        {
            try { maps.Add(_mapReader.Read(mapPath).Blocks); }
            catch { failed++; }
        }

        var model = BlockConnectionModel.Learn(maps, minimumSamples, variant: variant, namePrefix: namePrefix);
        var json = model.ToJson();

        if (outputPath is not null)
            File.WriteAllText(outputPath, json);

        var report = new StringBuilder();
        report.AppendLine($"Parsed {maps.Count} of {paths.Count} map(s){(failed > 0 ? $", {failed} unreadable" : "")}.");
        report.AppendLine($"Learned {model.EntryCount} (block, direction) entries.");
        report.AppendLine(outputPath is not null ? $"Written to {outputPath}." : "Not written to disk; pass outputPath to keep it.");

        return report.ToString().TrimEnd();
    }

    private RoutePlan BuildRoute(
        int seed,
        int length,
        double turnChance,
        int originX,
        int originZ,
        string direction,
        string style = "plain") =>
        new RouteBuilder(_connections.Value).Build(new RouteRequest(
            Seed: seed,
            Length: length,
            TurnChance: turnChance,
            OriginX: originX,
            OriginZ: originZ,
            Direction: direction,
            Style: style));

    [McpServerTool(Name = "learn_map_motif"), Description("Learn a multi-block structure - a loop, a reset-gate run, a scenery cluster - by measuring what sits around an anchor block across a corpus of maps. Some things in Trackmania are a shape, not a block: a loop is a five-wide wall over a base row with a support underneath.")]
    public string LearnMapMotif(
        [Description("Absolute path to a .Map.Gbx file or a directory of them.")] string path,
        [Description("Anchor block name, for example PlatformTechLoopStart or GateSpecialReset.")] string anchor,
        [Description("How far to look horizontally, in cells.")] int radius = 2,
        [Description("Share of anchor sightings an offset must reach to join the motif, 0 to 1.")] double threshold = 0.5,
        [Description("Optional path to write the motif JSON to, for stamp_map_motif.")] string? outputPath = null,
        [Description("Comma-separated block name prefixes to ignore. Defaults to DecoWall, the engine's own scenery layer, which the editor refuses to place by name.")] string? excludePrefixes = null)
    {
        var paths = MapGbxReader.EnumerateMaps(path);
        if (paths.Count == 0)
            return $"No .Map.Gbx files found at: {path}";

        var maps = new List<IReadOnlyList<MapBlock>>();
        foreach (var mapPath in paths)
        {
            try { maps.Add(_mapReader.Read(mapPath).Blocks); }
            catch { /* reported by count below */ }
        }

        var excluded = excludePrefixes?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var motif = _motifs.Learn(maps, anchor, radius, threshold: threshold, excludePrefixes: excluded);

        if (outputPath is not null && motif.Samples > 0)
            File.WriteAllText(outputPath, motif.ToJson());

        var report = new StringBuilder();
        report.AppendLine($"Parsed {maps.Count} of {paths.Count} map(s).");
        report.AppendLine(MotifLearner.Format(motif));

        if (outputPath is not null && motif.Samples > 0)
            report.AppendLine($"\nWritten to {outputPath}.");

        return report.ToString().TrimEnd();
    }

    [McpServerTool(Name = "stamp_map_motif"), Description("Place a learned motif into the open map editor, rotated to face a direction. Checks the whole footprint for collisions first: a loop missing two of its five wall pieces is worse than no loop.")]
    public async Task<string> StampMapMotif(
        [Description("Path to a motif JSON written by learn_map_motif.")] string motifPath,
        [Description("Map coordinate to place the anchor at, along X.")] int x,
        [Description("Map coordinate to place the anchor at, along Z.")] int z,
        [Description("Height to place the anchor at. Ground level in a 48x48Screen155Day map is 9.")] int y = 9,
        [Description("Direction the motif faces: North, East, South or West.")] string direction = "North",
        [Description("Drop motif blocks weaker than this share of anchor sightings.")] double minimumSupport = 0.5,
        [Description("Report what would be placed without touching the game.")] bool dryRun = false)
    {
        if (!File.Exists(motifPath))
            return $"File not found: {motifPath}";

        if (!DummyTrackBuilder.IsKnownDirection(direction))
            return $"Error: unknown direction '{direction}'. Use North, East, South, or West.";

        BlockMotif motif;
        try
        {
            motif = BlockMotif.FromJson(await File.ReadAllTextAsync(motifPath));
        }
        catch (Exception ex)
        {
            return $"Could not read the motif: {ex.GetType().Name} {ex.Message}";
        }

        var stamp = _motifStamper.Stamp(motif, x, y, z, direction, minimumSupport: minimumSupport);
        var report = new StringBuilder();
        report.AppendLine($"{motif.Anchor} facing {direction} at <{x}, {y}, {z}>:");
        report.AppendLine(MotifStamper.Format(stamp));

        if (dryRun)
        {
            report.AppendLine();
            foreach (var block in stamp.Blocks)
                report.AppendLine($"  {block.Name,-28} <{block.X}, {block.Y}, {block.Z}> dir={block.Direction}");

            return report.ToString().TrimEnd();
        }

        if (stamp.Blocks.Count == 0)
            return report.ToString().TrimEnd();

        var status = await _client.GetStatusAsync();
        if (status is null)
            return "OpenPlanet bridge not reachable. Check that TM2020Bridge is loaded.";

        if (!status.MapEditor)
            return "OpenPlanet bridge is running, but the map editor is not open.";

        var placement = await _client.PlaceMapBlocksAsync(stamp.ToPlacements());
        report.AppendLine();
        report.AppendLine(placement.Success
            ? $"Bridge reports:\n{placement.Body}"
            : $"Placement failed.\n{placement.Body}");

        return report.ToString().TrimEnd();
    }

    [McpServerTool(Name = "write_free_blocks"), Description("Write free blocks into a copy of a .Map.Gbx, at explicit world positions and rotations, with no game running. This is the only way to place geometry the editor plugin API cannot express: every placement method it exposes takes a grid coordinate and a cardinal direction, while roughly half the blocks in a map like Deep Dip sit off the grid at arbitrary angles.")]
    public string WriteFreeBlocks(
        [Description("Absolute path to the .Map.Gbx to use as the base. It is never modified. Save an empty map from the editor once and reuse it.")] string sourcePath,
        [Description("Absolute path to write the result to. Must differ from the source.")] string outputPath,
        [Description("JSON array of blocks: [{\"name\":\"PlatformIceWallStraight\",\"x\":768,\"y\":80,\"z\":768,\"yaw\":45,\"pitch\":0,\"roll\":-90}]. Positions are world units unless cells is true; angles are degrees.")] string blocksJson,
        [Description("Treat x/y/z as grid cells rather than world units. A cell is 32 wide and 8 tall.")] bool cells = false)
    {
        if (!File.Exists(sourcePath))
            return $"Source map not found: {sourcePath}";

        FreeBlockRequest[]? requested;
        try
        {
            requested = JsonSerializer.Deserialize<FreeBlockRequest[]>(blocksJson, FreeBlockJson);
        }
        catch (JsonException ex)
        {
            return $"Could not read blocksJson: {ex.Message}";
        }

        if (requested is null || requested.Length == 0)
            return "blocksJson held no blocks.";

        var placements = requested
            .Select(b => cells
                ? FreeBlockPlacement.AtCell(b.Name, b.X, b.Y, b.Z, b.Yaw, b.Pitch, b.Roll)
                : new FreeBlockPlacement(b.Name, b.X, b.Y, b.Z, b.Yaw, b.Pitch, b.Roll))
            .ToArray();

        MapGbxWriteResult result;
        try
        {
            result = _mapWriter.AddFreeBlocks(sourcePath, outputPath, placements);
        }
        catch (Exception ex)
        {
            return $"Write failed: {ex.GetType().Name} {ex.Message}";
        }

        var report = new StringBuilder();
        report.AppendLine($"{result.Path}");
        report.AppendLine($"{result.BlocksBefore} blocks in, {result.BlocksAfter} out ({result.Added} added, {result.FreeBlocks} free in total).");
        report.AppendLine("Read back from the saved file:");

        foreach (var block in result.Written.Take(20))
        {
            var rotation = block.Rotation ?? new Vector3(0, 0, 0);
            report.AppendLine($"  {block.Name,-28} pos={block.Position} "
                + $"yaw={rotation.X * 180 / MathF.PI:0.#}° pitch={rotation.Y * 180 / MathF.PI:0.#}° roll={rotation.Z * 180 / MathF.PI:0.#}°"
                + (block.IsTilted ? " TILTED" : ""));
        }

        if (result.Written.Count > 20)
            report.AppendLine($"  ... {result.Written.Count - 20} more.");

        report.AppendLine();
        report.AppendLine("Parsing the file back is not proof the game accepts it. Open the map in Trackmania to confirm.");

        return report.ToString().TrimEnd();
    }

    private sealed record FreeBlockRequest(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("x")] float X,
        [property: JsonPropertyName("y")] float Y,
        [property: JsonPropertyName("z")] float Z,
        [property: JsonPropertyName("yaw")] float Yaw = 0,
        [property: JsonPropertyName("pitch")] float Pitch = 0,
        [property: JsonPropertyName("roll")] float Roll = 0);

    private static readonly JsonSerializerOptions FreeBlockJson = new() { PropertyNameCaseInsensitive = true };

    [McpServerTool(Name = "remove_map_block"), Description("Remove one grid block from the open map editor. Reports what was there before it went, and optionally probes what the engine does to a block handle it has just deleted.")]
    public async Task<string> RemoveMapBlock(
        [Description("Map coordinate along X.")] int x,
        [Description("Map coordinate along Z.")] int z,
        [Description("Height. A negative value scans the column and takes whatever is stacked there.")] int y = -1,
        [Description("Read engine state off the deleted block's handle afterwards. This answers whether the engine clears block units and whether GetBlock still returns the same handle, and it is the kind of read that can crash the client. Use a scratch map.")] bool probe = false)
    {
        var status = await _client.GetStatusAsync();
        if (status is null)
            return "OpenPlanet bridge not reachable. Check that TM2020Bridge is loaded.";

        if (!status.MapEditor)
            return "OpenPlanet bridge is running, but the map editor is not open.";

        var result = await _client.RemoveMapBlocksAsync([new MapBlockRemoval(x, z, y)], probe);

        return result.Success
            ? $"OpenPlanet: remove requested at ({x}, {y}, {z}).\n{result.Body}"
            : $"OpenPlanet: remove failed.\n{result.Body}";
    }

    [McpServerTool(Name = "get_recent_manialink_events"), Description("Return recent ManiaLink event payloads recorded by the OpenPlanet bridge.")]
    public async Task<string> GetRecentManialinkEvents()
    {
        var events = await _client.GetRecentManialinkEventsAsync();
        if (events is null)
            return "OpenPlanet bridge not reachable or event endpoint unavailable.";

        if (events.Count == 0)
            return "No ManiaLink events recorded.";

        return string.Join(
            "\n",
            events.Select(e => $"[{e.Index}] {e.Body}"));
    }

    [McpServerTool(Name = "record_manialink_event"), Description("Record a ManiaLink event payload in the OpenPlanet bridge event buffer. Useful for probe/debug flows.")]
    public async Task<string> RecordManialinkEvent(
        [Description("Event payload, usually JSON with control id/action/source fields.")] string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "Error: event body is empty.";

        var result = await _client.RecordManialinkEventAsync(body);
        return result.Success
            ? $"OpenPlanet: ManiaLink event recorded.\n{result.Body}"
            : $"OpenPlanet: failed to record ManiaLink event.\n{result.Body}";
    }

    [McpServerTool(Name = "clear_manialink_events"), Description("Clear the OpenPlanet bridge ManiaLink event buffer.")]
    public async Task<string> ClearManialinkEvents()
    {
        var result = await _client.ClearManialinkEventsAsync();
        return result.Success
            ? $"OpenPlanet: ManiaLink event buffer cleared.\n{result.Body}"
            : $"OpenPlanet: failed to clear ManiaLink event buffer.\n{result.Body}";
    }

    [McpServerTool(Name = "inspect_manialink_interactions"), Description("Inspect ManiaLink XML for interactive label/quad controls with action, scriptaction, or scriptevents.")]
    public string InspectManialinkInteractions(
        [Description("Raw ManiaLink XML or Interface Designer fragment.")] string xml)
    {
        return _manialinks.InspectInteractiveControls(xml);
    }

    [McpServerTool(Name = "validate_manialink_xml"), Description("Check ManiaLink XML against Trackmania 2020 constraints: element names, media formats the client can actually decode, the 320x180 coordinate space, script-event wiring, duplicate ids, and Interface Designer paste-safety. Run this before pushing XML into the game.")]
    public string ValidateManialinkXml(
        [Description("Raw ManiaLink XML or Interface Designer fragment.")] string xml,
        [Description("Where the XML is going: \"manialink\" for a document pushed to the game or served as HUD, \"designer\" for a fragment pasted into the in-game Interface Designer.")] string target = "manialink")
    {
        var parsed = target.Trim().ToLowerInvariant() switch
        {
            "designer" or "interfacedesigner" or "interface-designer" => ManialinkTarget.InterfaceDesigner,
            "manialink" or "" => ManialinkTarget.Manialink,
            _ => (ManialinkTarget?)null
        };

        if (parsed is null)
            return $"Unknown target '{target}'. Use 'manialink' or 'designer'.";

        return ManialinkValidator.Format(_validator.Validate(xml, parsed.Value));
    }

    [McpServerTool(Name = "validate_manialink_file"), Description("Read a ManiaLink XML file from disk and validate it against Trackmania 2020 constraints.")]
    public string ValidateManialinkFile(
        [Description("Absolute path to a .xml file.")] string path,
        [Description("Where the XML is going: \"manialink\" or \"designer\".")] string target = "manialink")
    {
        if (!File.Exists(path))
            return $"File not found: {path}";

        return ValidateManialinkXml(File.ReadAllText(path), target);
    }

    [McpServerTool(Name = "check_manialink_media"), Description("Fetch every http(s) image, video and audio URL in ManiaLink XML and report the ones the game will silently fail to render: dead URLs, non-200 responses, web pages served where a media file was expected, and animated WebP confirmed from its header. Needs no running game.")]
    public async Task<string> CheckManialinkMedia(
        [Description("Raw ManiaLink XML or Interface Designer fragment.")] string xml)
    {
        return ManialinkValidator.Format(await _mediaProbe.ProbeAsync(xml));
    }

    [McpServerTool(Name = "analyze_emoji_chat_message"), Description("Analyze a Kacky EmojiChat message for emoji shortcodes, Trackmania format codes, unknown emoji, and ManiaLink-safe text.")]
    public string AnalyzeEmojiChatMessage(
        [Description("Raw chat message.")] string message,
        [Description("Optional comma-separated known emoji names to merge with defaults.")] string? knownEmojiNames = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Error: message is empty.";

        var analysis = _emojiChat.Analyze(message, knownEmojiNames);
        return $"""
            Original: {analysis.Original}
            Plain text: {analysis.PlainText}
            Emoji tokens: {FormatList(analysis.EmojiTokens)}
            Unknown emoji: {FormatList(analysis.UnknownEmoji)}
            Trackmania format codes: {FormatList(analysis.TrackmaniaFormatCodes)}
            ManiaLink-safe text: {analysis.ManialinkSafeText}
            """;
    }

    [McpServerTool(Name = "build_emoji_chat_preview_xml"), Description("Build a small paste-safe ManiaLink fragment to preview one EmojiChat message.")]
    public string BuildEmojiChatPreviewXml(
        [Description("Raw chat message.")] string message,
        [Description("Optional comma-separated known emoji names to merge with defaults.")] string? knownEmojiNames = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Error: message is empty.";

        return _emojiChat.BuildLabelPreviewXml(message, knownEmojiNames);
    }

    [McpServerTool(Name = "build_manialink_video_probe_xml"), Description("Build a small ManiaLink XML document with a video tag for GPS/video experiments.")]
    public string BuildManialinkVideoProbeXml(
        [Description("Video data path or URL, for example file://Media/Videos/gps.webm.")] string data,
        [Description("Whether to route the video as music/audio.")] bool music = true,
        [Description("Whether playback starts immediately.")] bool play = true,
        [Description("Whether the video element is hidden.")] bool hidden = false)
    {
        try
        {
            return _videoProbe.Build(data, music, play, hidden);
        }
        catch (ArgumentException ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static string FormatList(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? "(none)" : string.Join(", ", values);
    }
}
