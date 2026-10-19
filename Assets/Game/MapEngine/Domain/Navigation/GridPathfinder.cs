using System;
using System.Collections.Generic;

namespace Game.MapEngine.Domain
{
    /// <summary>
    /// 八邻接 A*，按实际格宽/格高计算代价。只依赖纯地图定义。
    /// 搜索工作区可复用；调用方依次使用它，将每次路径复制到各自单位的状态中。
    /// 不是线程安全服务，也不保存怪物目标或追踪状态。
    /// </summary>
    public sealed class GridPathfinder
    {
        private readonly GridMap _map;
        private readonly float[] _cost, _estimatedTotal;
        private readonly int[] _parent, _heap, _heapPosition;
        private readonly byte[] _state;
        private int _heapCount;
        public GridPathfinder(GridMap map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _cost = new float[map.CellCount];
            _estimatedTotal = new float[map.CellCount];
            _parent = new int[map.CellCount];
            _heap = new int[map.CellCount];
            _heapPosition = new int[map.CellCount];
            _state = new byte[map.CellCount];
        }

        /// <summary>写入包含起点和终点的格子序列；失败时清空输出，不返回上次路径。</summary>
        public bool TryFindPath(GridCell start, GridCell goal, List<GridCell> output)
        {
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            output.Clear();
            if (!_map.IsWalkable(start) || !_map.IsWalkable(goal))
                return false;
            Array.Clear(_state, 0, _state.Length);
            _heapCount = 0;
            int startIndex = _map.IndexOf(start);
            int goalIndex = _map.IndexOf(goal);
            _cost[startIndex] = 0;
            _parent[startIndex] = -1;
            _estimatedTotal[startIndex] = Distance(start, goal);
            Push(startIndex);
            while (_heapCount > 0)
            {
                int currentIndex = Pop();
                if (currentIndex == goalIndex)
                {
                    for (int index = goalIndex; index >= 0; index = _parent[index])
                        output.Add(_map.CellAt(index));
                    output.Reverse();
                    return true;
                }

                _state[currentIndex] = 2;
                var current = _map.CellAt(currentIndex);
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var next = new GridCell(current.X + dx, current.Y + dy);
                        if (!_map.CanStep(current, next))
                            continue;
                        int nextIndex = _map.IndexOf(next);
                        if (_state[nextIndex] == 2)
                            continue;
                        float cost = _cost[currentIndex] + Distance(current, next);
                        if (_state[nextIndex] == 1 && cost >= _cost[nextIndex])
                            continue;
                        _cost[nextIndex] = cost;
                        _parent[nextIndex] = currentIndex;
                        _estimatedTotal[nextIndex] = cost + Distance(next, goal);
                        if (_state[nextIndex] == 0)
                            Push(nextIndex);
                        else
                            SiftUp(_heapPosition[nextIndex]);
                    }
                }
            }

            return false;
        }

        // Octile 距离：矩形格子的对角成本也按真实长度计算，作为一致的 A* 启发式。
        private float Distance(GridCell from, GridCell to)
        {
            int dx = Math.Abs(to.X - from.X);
            int dy = Math.Abs(to.Y - from.Y);
            int diagonal = Math.Min(dx, dy);
            float diagonalCost = (float)Math.Sqrt(_map.CellWidth * _map.CellWidth + _map.CellHeight * _map.CellHeight);
            return diagonal * diagonalCost + (dx - diagonal) * _map.CellWidth + (dy - diagonal) * _map.CellHeight;
        }

        // 固定容量二叉堆，无每次搜索的节点对象分配；相同估值用格子索引稳定排序。
        private bool ComesBefore(int left, int right)
        {
            return _estimatedTotal[left] < _estimatedTotal[right] || (_estimatedTotal[left] == _estimatedTotal[right] && left < right);
        }

        private void Push(int index)
        {
            _state[index] = 1;
            _heap[_heapCount] = index;
            _heapPosition[index] = _heapCount;
            SiftUp(_heapCount++);
        }

        private int Pop()
        {
            int result = _heap[0];
            _heapCount--;
            if (_heapCount == 0)
                return result;
            _heap[0] = _heap[_heapCount];
            _heapPosition[_heap[0]] = 0;
            int position = 0;
            while (position * 2 + 1 < _heapCount)
            {
                int child = position * 2 + 1;
                if (child + 1 < _heapCount && ComesBefore(_heap[child + 1], _heap[child]))
                    child++;
                if (!ComesBefore(_heap[child], _heap[position]))
                    break;
                Swap(position, child);
                position = child;
            }

            return result;
        }

        private void SiftUp(int position)
        {
            while (position > 0)
            {
                int parent = (position - 1) / 2;
                if (!ComesBefore(_heap[position], _heap[parent]))
                    break;
                Swap(position, parent);
                position = parent;
            }
        }

        private void Swap(int left, int right)
        {
            int temp = _heap[left];
            _heap[left] = _heap[right];
            _heap[right] = temp;
            _heapPosition[_heap[left]] = left;
            _heapPosition[_heap[right]] = right;
        }
    }
}
