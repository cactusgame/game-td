using Game.Presentation.Views;
using UnityEngine;

namespace Game.Battle.Unity.Assets
{
    /// <summary>单位与战斗的表现资源目录；引用制作资产及产品外观配置，不拥有战斗状态。</summary>
    public sealed class UnitVisualCatalog : ScriptableObject
    {
        public TextAsset Presentation;
        public UnitAnimationSet[] Animations;
        public Sprite White;
        public Sprite Circle;
    }
}
