using System.Collections.Generic;
using System.Linq;
using Game.Maps.Unity.Assets;
using UnityEditor;

namespace Game.Tools.MapAuthoring.Editor
{
    /// <summary>编辑器拥有外观偏好；目录资源只含制作元数据，不改变地图规则和历史。</summary>
    public static class MapArtSelection
    {
        private const string Preference = "GameTD.MapArtCatalog";
        public static string Path => EditorPrefs.GetString(Preference, MapEditorWindow.VisualPath);
        public static MapVisualCatalog Current => AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(Path)
            ?? AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(MapEditorWindow.VisualPath);

        public static List<MapVisualCatalog> All()
        {
            return AssetDatabase.FindAssets("t:MapVisualCatalog", new[] { "Assets/Content/Maps" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<MapVisualCatalog>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(item => item != null).OrderBy(item => item.name).ToList();
        }

        public static void Select(MapVisualCatalog catalog)
        {
            EditorPrefs.SetString(Preference, AssetDatabase.GetAssetPath(catalog));
        }
    }
}
