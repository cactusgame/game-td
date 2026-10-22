using System;
using System.IO;
using System.Linq;
using Game.Infrastructure.Maps.Serialization;
using Game.MapEngine.Domain;
using Game.Maps.Domain;
using Game.Maps.Unity.Assets;
using UnityEditor;
using UnityEngine;

/// <summary>将已导入的 V3 门图接入目录，并创建可独立试玩的城门示例；不覆盖已有地图。</summary>
public static class ConnectGates
{
    public static string Main()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MapVisualCatalog>("Assets/Content/Maps/Shared/MapVisualCatalog.asset");
        catalog.gateClosed = Sprite("gate-closed.png");
        catalog.gateOpen = Sprite("gate-open.png");
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        const string path = "Assets/Content/Maps/Example/gates.map.json";
        if (!File.Exists(path))
        {
            var map = new MapLayout { id = "gate-courtyard" };
            for (int y = 0; y < map.rows; y++)
                for (int x = 11; x <= 13; x++)
                    map.terrain[y * map.columns + x] = TerrainKind.Road;
            map.markers.Add(new MapMarker { id = "hero", name = "城门南侧", kind = MarkerKind.HeroSpawn, x = 0.5f, y = -2.5f });
            map.markers.Add(new MapMarker { id = "entry", name = "城门北侧", kind = MarkerKind.EnemyEntry, x = 0.5f, y = 6.5f });
            map.wallNodes.Add(new WallNode { id = "west", x = -10.5f, y = 1.5f });
            map.wallNodes.Add(new WallNode { id = "east", x = 10.5f, y = 1.5f });
            map.wallEdges.Add(new WallEdge
            {
                id = "main-gate", from = "west", to = "east", hasGate = true,
                gate = new WallGate { position = 11f / 21f }
            });
            var rules = FacilityCatalogJson.Read(catalog.facilityRules.text);
            var issues = MapContentValidator.Validate(map, rules);
            if (issues.Any(issue => issue.IsError))
                throw new InvalidOperationException(string.Join("; ", issues.Select(issue => issue.Message)));
            File.WriteAllText(path, MapJson.WriteDocument(map));
        }
        AssetDatabase.Refresh();
        return "V3 open/closed gates connected; playable map: " + path;
    }

    private static Sprite Sprite(string file) => AssetDatabase.LoadAllAssetsAtPath("Assets/Content/Maps/ArtV3/" + file).OfType<Sprite>().Single();
}
