using Game.Fortifications.Domain.Definitions;
using System;
using System.Collections.Generic;

namespace Game.Maps.Domain
{
    [Serializable]
    public sealed class FacilityDefinition
    {
        public string id;
        public string name;
        public int maxHealth = 300;
        public int attack;
        public float attackRange = 5;
        public float attackInterval = 1;
        public int width = 1;
        public int height = 1;
        public bool blocksMovement = true;
        public bool blocksShots = true;
        public bool hasFunction = true;
        public bool wallMounted;
    }

    /// <summary>只读内容目录；预放和试玩建设共用占地规则，不包含费用或阶段许可。</summary>
    public sealed class MapContentCatalog
    {
        private readonly Dictionary<string, FacilityDefinition> _facilities = new Dictionary<string, FacilityDefinition>();
        public IReadOnlyList<string> FacilityIds => new List<string>(_facilities.Keys).AsReadOnly();
        public WallDefinition Wall { get; }

        public MapContentCatalog(IEnumerable<FacilityDefinition> definitions, WallDefinition wall = null)
        {
            Wall = wall ?? WallDefinition.Default;
            foreach (var item in definitions)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id) || item.width <= 0 || item.height <= 0 || item.maxHealth <= 0 || item.attack < 0 ||
                    float.IsNaN(item.attackRange) || float.IsInfinity(item.attackRange) || item.attackRange < 0 ||
                    float.IsNaN(item.attackInterval) || float.IsInfinity(item.attackInterval) || item.attackInterval <= 0 ||
                    _facilities.ContainsKey(item.id))
                    throw new ArgumentException("Invalid or duplicate facility definition.");
                _facilities.Add(item.id, Copy(item));
            }
        }

        public FacilityDefinition Find(string id)
        {
            return id != null && _facilities.TryGetValue(id, out var item) ? Copy(item) : null;
        }

        private static FacilityDefinition Copy(FacilityDefinition item)
        {
            return new FacilityDefinition
            {
                id = item.id, name = item.name, width = item.width, height = item.height,
                blocksMovement = item.blocksMovement, blocksShots = item.blocksShots, hasFunction = item.hasFunction, wallMounted = item.wallMounted, maxHealth = item.maxHealth, attack = item.attack, attackRange = item.attackRange, attackInterval = item.attackInterval
            };
        }
    }
}
