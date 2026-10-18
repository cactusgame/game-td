# 寻路网格与怪物追踪

## 边界与状态归属

`Game.MapEngine.Domain` 是 `noEngineReferences` 的纯 C# 程序集，提供 `GridMap`、`GridCell`、连续地面坐标 `MapPoint` 和 `GridPathfinder`。它不依赖 UnityEngine、Tilemap、Entities、场景或美术素材，不保存英雄/怪物的位置、血量或目标。

当前 `BattleDefinition.NavigationMap` 创建覆盖场地的全通行地图：96 列 × 54 行，每格 1 × 1 地面单位，左下角为 (-48, -27)。格子采用八邻接，所有格子连通。左/下边界包含，右/上边界不包含。格子尺寸不能整除场地时，会略微缩小实际格宽/格高以精确覆盖场地。相机只显示大地图的一部分，跟随规则见 `Docs/LargeMap.md`。

地图是不可变的对局定义。`GridMap` 构造函数可接收列数、行数、实际格宽/格高、原点和按 `y * Columns + x` 排列的移动通行数据；它复制输入数组，编辑文档不能通过共享数组修改对局。地图的坐标转换和 A* 可以直接供将来的关卡导出、验证或试玩使用。

每只怪物的可变状态只存在于 ECS：

- `MonsterNavigationState`：是否追踪、路径游标、目标格和下次重算时间。
- `DynamicBuffer<NavigationWaypoint>`：该怪物独有的连续地面路径点。
- `WanderState`：该怪物独有的随机流、游走方向和剩余方向持续时间。

导航系统复用一个 A* 搜索工作区，依次处理路径请求，再复制到各自的 Buffer。复用的是计算内存，不是怪物路径、目标选择或移动进度。重启销毁旧单位 Buffer 并重建搜索工作区。

## 可调配置

在 `Assets/Content/Combat/Prototype/battle.json` 的同一个 `navigation` 节点修改：

```json
"navigation": {
  "cellSize": 1.0,
  "detectionRadius": 5.0,
  "repathSeconds": 0.25
}
```

- `cellSize`：期望格长，单位是连续地面长度。实际格宽/格高可通过地图定义查询。
- `detectionRadius`：怪物与英雄地面中心间的欧氏距离，等于边界也算进入范围。两种怪物使用同一配置，各自判断距离。
- `repathSeconds`：持续追踪时两次完整 A* 的最小间隔，基于模拟时间。进入范围立即寻路，离开范围立即清空路线并恢复随机游走，不等待该计时。

三个值必须有限且大于 0，过细网格会在加载时被格子数量上限拒绝。JSON 加载器先验证，再创建对局。修改 JSON 后重启对局生效。

## 实际调用链

`BattleSession.Restart` → `JsonBattleDefinitionSource.Load` → `BattleDefinition` / `GridMap.CreateOpenArena` → `EcsBattleSimulation.Reset` 配置导航系统、生成 ECS 组件与路径 Buffer。

每个固定步：

1. `MonsterNavigationSystem` 读取当前英雄和怪物状态；范围内的活怪物调用八邻接 A*，范围外或英雄死亡时恢复原有随机游走，死亡怪物清空路线。
2. `MovementSystem` 的并行 Job 消费每个实体自己的路径或随机方向。它是位置的唯一写入者，一步可消费多个路径点，但距离不超过速度 × 步长；接近最后路径点时不越过目标。同格内也跟随英雄的连续坐标。
3. `CircleContactSystem` 继续按现有接触规则结算伤害。
4. `CopyUnits` → `BattleSession` → `BattleView`，表现层照常读取位置、朝向、攻击/受伤/死亡状态。

A* 按实际格宽/格高计算直线和对角代价，使用固定容量二叉堆；重复搜索复用数组和输出列表。当前完整路径搜索在主线程低频批量运行，位移仍使用 Burst/并行 Job；这不是万单位性能结论。

## 将来的 Tilemap 编辑器接入点

将来的创作工具可使用 Tilemap 表达设计文档，再把它转换成 `GridMap` 的纯数据参数。工具适配器负责 Unity 坐标和素材含义的转换，导出/加载层负责数据验证。战斗只接收导出的地图定义，不读取 Tilemap。

`BattleDefinition` 已提供可选 `navigationMap` 参数用于接收导出的定义，并检查它与战斗场地的原点、宽高一致。当前 JSON 的加载入口只生成空地图，本次没有新增编辑器、Tilemap、NavMesh 或场景网格对象。

当前战斗场地全通行。纯地图查询已支持静态移动通行数据与禁止对角穿角；障碍关卡的出生验证、带单位半径的障碍避让、随机游走避障和运行中建墙/破墙的导航更新，需要随障碍玩法接入，不能只导出一份阻挡数组就视为这些玩法全部完成。

## 验证

Unity 编译通过，编辑器测试共 39 项通过，包括全图连通、坐标转换、非整除覆盖、矩形格子路径长度、地图数据不可变、寻路失败/重用、独立感知与路径、范围边界、出入范围、英雄同格移动、重算间隔、高速不越点和死亡停止追踪，以及原有移动/伤害/死亡表现回归。

Play Mode 验证：同一场景中，一只范围内怪物追踪而另一只范围外怪物游走；两只进入范围后路径不同；英雄移动后只有超出范围的怪物停止追踪；重入可立即追踪；死亡怪物不再移动，英雄死亡后结束追踪。死亡动画/停留/移除和重启恢复也已回归验证，完成后退出 Play Mode。
