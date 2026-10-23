# 城墙领域、表现与运行提交

## 当前边界（2026-10-23）

编辑器预放和游戏运行共用工事连接规则与 Unity 绘制能力，各自拥有文档或对局提交用例。

| 模块 | 当前职责 |
| --- | --- |
| `Gameplay/Fortifications/Domain` | 只读墙尺寸、墙网与门状态、连接判定、连接底座半径和连接弦；不引用 Unity、地图文档或战斗 |
| `Gameplay/Fortifications/Unity` | 墙身、顶面、女墙、垛口的网格、材质、UV、深度裁切与资源释放；消费工事快照和素材元数据 |
| `Gameplay/Maps/Domain` | 地图初始描述、内容校验、描述与工事快照的转换、现有导航的过渡适配 |
| `Gameplay/Construction/Application` | 运行布置候选、单位净空检查、空间派生与版本提交；持有独立 `WallNetwork` 聚合 |
| `MapEngine/Unity` | 通用网格数据、绘制条目、素材坐标采样；不引用工事或地图玩法 |
| `Tools/MapAuthoring` | 编辑草稿、输入、选择、历史、保存与试玩入口 |

`WallNetwork` 是运行墙与门的唯一可变状态来源。`MapConstructionState` 的地图部分不保留第二份墙节点或墙边列表；对外的 `MapLayout` 按需从聚合导出。改动返回的快照不能修改对局，也不能修改地图源文件。

地图里的 `WallNode`、`WallEdge`、`WallGate` 仍是 schemaVersion 1 的预放描述，不保存血量、投资或 ECS Entity。移动脚本时保留原 `.meta` GUID。

## 尺寸与素材

`Assets/Content/Maps/Shared/facilities.json` 是现有共享空间内容目录。新增 `wall` 段，由基础设施加载器转换为不可变 `WallDefinition`：

```json
"wall": {
  "width": 0.52,
  "height": 0.5,
  "junctionRadius": 0.4
}
```

尺寸使用世界地面单位，改变地形格宽高或更换图片不再改变墙宽和墙高。旧目录没有 `wall` 段时使用兼容默认值；单位格地图保留之前的基础尺寸。非单位格地图不再自动把墙体乘以格大小。

`MapVisualCatalog.asset` 保留素材和固定制作元数据：墙面石材、墙顶铺地、女墙/垛口造型、材质重复尺度、门图透明边距。原序列化字段 `wallDimensions` 保留名称以兼容既有素材元数据，但类型改为 `WallVisualStyle`，不再包含墙宽、墙高或节点半径。读取 `WallStyle` 返回副本，不改写共享资产。

圆形连接底座用于封合墙端、转角和分叉，不是能够升级或驻军的敌台设施。底座仍按邻接夹角和相邻墙长解析连接范围。后续敌台需要独立设施身份与定义，不能从这段外观推导容量、生命或升级状态。墙顶是表现表面，不因此成为单位可行走区域。

## 实际调用链

编辑画墙：

`MapEditorWindow` 输入 → 草稿端点 → `MapWallSnapshotMapper` → `WallNetwork.TryConnect` → 回写预放描述 → `MapEditSession.Commit`。

编辑器与试玩绘制：

`MapSceneSprites` → 只读墙网快照 → `RaisedWallPresentation` / `WallJunctionPresentation` → `WallSurfaceBuilder` → 共享绘制数据 → 编辑画布或运行 `WallMeshView`。

运行开关门、拆除：

`MapPlayController` → `MapPlaySession` → `MapConstructionState.CreateCandidate` → 工事聚合操作 → 派生导航、单位净空校验 → 模拟接受新空间 → 发布候选 → 重建表现。

关门被单位占用或模拟空间更新失败时，不发布新墙状态和版本；空间恢复也失败时，试玩停止推进并要求重启。本次保留原有同步边界。

## 优先阅读

1. `Assets/Game/Gameplay/Fortifications/Domain/Aggregates/WallNetwork/WallNetwork.cs`：墙网状态与受控操作。
2. `Assets/Game/Gameplay/Fortifications/Domain/Rules/WallConnectionRules.cs`：中心线、邻接方向、底座与连接弦。
3. `Assets/Game/Gameplay/Construction/Application/Sessions/MapConstructionState.cs`：运行建设和空间提交协调。
4. `Assets/Game/Gameplay/Maps/Domain/Mappings/MapWallSnapshotMapper.cs`：地图初始内容与工事之间的转换。
5. `Assets/Game/Gameplay/Fortifications/Unity/Views/RaisedWallPresentation.cs`、`WallJunctionPresentation.cs`：墙身与连接底座表现。

## 范围与验证

本次是现有墙体能力的边界重构，没有新增运行画墙 UI、经济支付、阶段许可、建筑升级、敌台驻军、墙段承伤或自动交叉切分。设施仍沿用首版布置模型，尚未扩展成完整的 Facilities 生命周期。

按用户要求暂缓精确通行。`MapSpatialRules.LegacyNavigationEnvelopeWidth` 明确表示旧导航投影包络，不是第二套物理墙宽。它保留既有建设与移动结果，连接底座的真实轮廓尚未接入导航。精确轮廓接入时应从工事快照与定义导出障碍，不能把表现 Mesh 作为玩法真值。

自动验证覆盖编译、墙网候选隔离、失败关门不提交、重复与无效连接拒绝、尺寸独立于格子/素材、程序集依赖、源文档与运行状态隔离，以及既有地图编辑、导出、加载和真实 ECS 试玩回归。结果记录在 `Docs/Validation/wall-refactor-tests.json`。未进行逐场景画面、端点拖动、角色前后遮挡或精确通行的人工验收。

## 城门扩展

城门已改为独立 ID 的多门列表，支持任意墙向和整数 Tile 区间；墙身按多个门框分段，门柱、门楣与铁栅由公共工事表现生成。运行放门入口为 `MapPlaySession.TryPlaceGate`，开关门命令使用门 ID。旧单门字段只保留在导入迁移 DTO 中。当前门的操作、素材与兼容策略见 [城门说明](Gates.md)。
