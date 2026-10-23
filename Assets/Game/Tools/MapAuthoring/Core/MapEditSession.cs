using System;
using System.Collections.Generic;
using Game.Maps.Domain;

namespace Game.Tools.MapAuthoring
{
    /// <summary>编辑文档和历史的唯一拥有者。一笔笔刷或一次拖动形成一次历史记录。</summary>
    public sealed class MapEditSession
    {
        /// <summary>编辑器在候选副本上替换完整占地，通过空间校验后由调用者提交历史；玩家建造仍使用严格空间规则。</summary>
        public static bool TryReplaceFacility(MapLayout layout, MapContentCatalog catalog, InitialFacility facility, out MapLayout result, out string reason)
        {
            result = layout.Copy();
            var definition = catalog.Find(facility.definitionId);
            if (definition == null)
                return MapSpatialRules.CanPlaceFacility(layout, catalog, facility, out reason);
            result.facilities.RemoveAll(item =>
            {
                var other = catalog.Find(item.definitionId);
                return item.id != facility.id && other != null && item.x < facility.x + definition.width &&
                    item.x + other.width > facility.x && item.y < facility.y + definition.height && item.y + other.height > facility.y;
            });
            for (int y = facility.y; y < facility.y + definition.height; y++)
            {
                for (int x = facility.x; x < facility.x + definition.width; x++)
                {
                    if (!result.Geometry.Contains(new Game.MapEngine.Domain.GridCell(x, y)))
                        continue;
                    int index = y * result.columns + x;
                    if (MapLayout.IsTerrainObstacle(result.terrain[index]))
                        result.terrain[index] = result.GroundAt(index);
                }
            }
            if (!MapSpatialRules.CanPlaceFacility(result, catalog, facility, out reason))
                return false;
            result.facilities.RemoveAll(item => item.id == facility.id);
            result.facilities.Add(facility.Copy());
            return true;
        }
        public static void Paint(MapLayout layout, MapContentCatalog catalog, int index, Game.MapEngine.Domain.TerrainKind kind)
        {
            if (!MapLayout.IsTerrainObstacle(kind))
            {
                layout.PaintGround(index, kind);
                return;
            }
            int x = index % layout.columns;
            int y = index / layout.columns;
            layout.facilities.RemoveAll(item =>
            {
                var definition = catalog.Find(item.definitionId);
                return definition != null && x >= item.x && x < item.x + definition.width && y >= item.y && y < item.y + definition.height;
            });
            layout.PaintGround(index, layout.GroundAt(index));
            layout.terrain[index] = kind;
        }

        [Serializable]
        public sealed class Revision
        {
            public MapLayout Layout;
            public long Number;
        }
        private readonly Stack<Revision> _undo = new Stack<Revision>();
        private readonly Stack<Revision> _redo = new Stack<Revision>();
        private Revision _current;
        private long _nextRevision;
        private long _savedRevision;
        public MapLayout Document => _current.Layout.Copy();
        public bool IsDirty => _current.Number != _savedRevision;
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public MapEditSession(MapLayout document, bool saved = false)
        {
            _current = new Revision { Layout = document.Copy(), Number = ++_nextRevision };
            _savedRevision = saved ? _current.Number : 0;
        }

        public void Commit(MapLayout replacement)
        {
            _undo.Push(_current);
            _redo.Clear();
            _current = new Revision { Layout = replacement.Copy(), Number = ++_nextRevision };
        }

        public void Undo()
        {
            if (!CanUndo)
                return;
            _redo.Push(_current);
            _current = _undo.Pop();
        }

        public void Redo()
        {
            if (!CanRedo)
                return;
            _undo.Push(_current);
            _current = _redo.Pop();
        }

        public void MarkSaved() => _savedRevision = _current.Number;

        [Serializable]
        public sealed class History
        {
            public Revision current;
            public Revision[] undo;
            public Revision[] redo;
            public long nextRevision;
            public long savedRevision;
        }

        public History CaptureHistory()
        {
            return new History
            {
                current = _current, undo = _undo.ToArray(), redo = _redo.ToArray(),
                nextRevision = _nextRevision, savedRevision = _savedRevision
            };
        }

        public static MapEditSession Restore(History history)
        {
            var session = new MapEditSession(history.current.Layout);
            session._current = history.current;
            session._nextRevision = history.nextRevision;
            session._savedRevision = history.savedRevision;
            for (int i = history.undo.Length - 1; i >= 0; i--)
                session._undo.Push(history.undo[i]);
            for (int i = history.redo.Length - 1; i >= 0; i--)
                session._redo.Push(history.redo[i]);
            return session;
        }
    }
}
