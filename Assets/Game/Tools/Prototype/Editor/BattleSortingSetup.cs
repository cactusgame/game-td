using System;
using System.Collections.Generic;
using Game.Presentation.Views;
using UnityEditor;
using UnityEngine;

namespace Game.Tools.Prototype
{
    /// <summary>通过 Unity 序列化 API 建立绘制层，保留原有层及其 ID。</summary>
    public static class BattleSortingSetup
    {
        public static void EnsureLayers()
        {
            var manager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var serialized = new SerializedObject(manager);
            var layers = serialized.FindProperty("m_SortingLayers");
            string[] names = { BattleRenderSorting.GroundLayer, BattleRenderSorting.ShadowLayer, BattleRenderSorting.UnitLayer };
            var usedIds = new HashSet<int>();
            for (int i = 0; i < layers.arraySize; i++)
                usedIds.Add(layers.GetArrayElementAtIndex(i).FindPropertyRelative("uniqueID").intValue);
            foreach (string name in names)
            {
                if (Find(name) >= 0) continue;
                layers.InsertArrayElementAtIndex(layers.arraySize);
                var layer = layers.GetArrayElementAtIndex(layers.arraySize - 1);
                layer.FindPropertyRelative("name").stringValue = name;
                int id;
                do { id = GUID.Generate().GetHashCode() & int.MaxValue; }
                while (id == 0 || !usedIds.Add(id));
                layer.FindPropertyRelative("uniqueID").intValue = id;
                layer.FindPropertyRelative("locked").boolValue = false;
            }
            // 保持其他层的相对顺序；三个战斗层按地面 → 阴影 → 单位排列。
            for (int i = 0; i < names.Length; i++)
                layers.MoveArrayElement(Find(names[i]), layers.arraySize - names.Length + i);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            int ground = SortingLayer.GetLayerValueFromID(SortingLayer.NameToID(BattleRenderSorting.GroundLayer));
            int shadows = SortingLayer.GetLayerValueFromID(SortingLayer.NameToID(BattleRenderSorting.ShadowLayer));
            int units = SortingLayer.GetLayerValueFromID(SortingLayer.NameToID(BattleRenderSorting.UnitLayer));
            if (ground >= shadows || shadows >= units)
                throw new InvalidOperationException("Battle sorting layers were not applied in the expected order.");

            int Find(string name)
            {
                for (int i = 0; i < layers.arraySize; i++)
                    if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name) return i;
                return -1;
            }
        }
    }
}
