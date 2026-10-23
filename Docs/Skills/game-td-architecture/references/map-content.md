# 地图共享、内容与素材

## 地图引擎与创作工具

```text
Game/MapEngine/
  Domain/Geometry, Grid, Terrain, SpatialRules
  Contracts/MapDefinition, ObstacleDescriptions, Queries
  Runtime/MapState, SpatialIndex, ChangeSets
  Navigation/Contracts, Runtime
  Simulation/                 必要的 Job/ECS 实现
  Unity/CoordinateMapping, TilemapViews, Picking, DebugViews
Game/Tools/MapAuthoring/
  Core/                       编辑文档、选择、历史
  Application/                地形编辑、放入口、预放内容、导出
  Validation/
  Editor/                     Unity Editor UI 与 Scene 工具
```

游戏与编辑器共用地形模型、实际长度几何、占地/阻挡查询、导航和可复用绘制能力。
工事连接、墙区段与门洞规则仍归 Fortifications；地图引擎接收其导出的有效轮廓。
正式建造还验证阶段、费用和投资，编辑器放初始内容不扣玩家金币。两者调用共享规划规则，各自实现用例，不堆砌 `isEditor/ignoreCost/ignorePhase` 分支。
编辑文档、地图初始定义、正式对局运行状态分开。游戏拆除障碍/建墙不改写源地图。
试玩创建独立会话和 World，不能污染编辑历史；玩家内置编辑器可复用 Core/Application 加运行时 UI，不能引用 UnityEditor。

## 空间语义

- Rectangle 网格管理地形、对齐和离散查询，连续二维地面坐标管理任意方向墙和移动；默认 XY 地面。
- 格宽高不同时，距离按真实长度计算。脚底/墙脚锚点与图像中心分开。
- 地面占地、移动阻挡、射线阻挡、画面轮廓分别表达，不能共享一个含义不明的 Blocked。
- 动态占地从建筑、墙段和门的权威状态派生，索引不另存可任意编辑的位置或门状态。
- 墙节点/边拓扑、稳定破坏区段、可随遮挡切分的渲染片段分开。Mesh 分片变化不能重分血量或重复收费。
- 斜墙有有效厚度，禁止穿角或穿过不足宽度的门洞。无步行路线可以触发破障策略，不自动等于非法建设。
- 破墙/关门在明确模拟边界生效；导航缓存未重算时局部约束仍阻止穿墙。导航算法不擅自决定敌人业务目标优先级。

## 内容存放与处理

```text
Content/Units/Heroes/Knight/
  KnightVisual.asset 等资源引用（数值配置集中在 Combat/Prototype 的 JSON）
  KnightVisual.asset
  Sprites/
  Animations/
  Prefabs/
  Materials/
  Audio/
Content/Units/Enemies/<Kind>/...
Content/Buildings/<Kind>/...
Content/Fortifications/<Kind>/...
Content/Maps/<Map>/             可编辑地图源和预览
Content/Encounters/<Level>/     波次、目标、开发者调校
Content/Shared/                 共用 Shader、材质、字体、音频、Atlas
```

按真实需要建目录，不强制每个内容类型具备所有资产种类。
规则配置引用稳定 DefinitionId；视图目录映射 Sprite/动画/Prefab/音效/锚点。Authoring 资产可暂时合并，但导出到领域和 ECS 时分开。
产品可调配置以 `Config/GameContent.xlsx` 为唯一编辑源，通过 `Tools/ContentConfiguration/export_config.py` 或 Unity 的“Game/内容配置/导出 Excel”生成 JSON。生成结果统一放在 `Assets/Content/Configuration/Resources/GameConfig/`；规则投影为 `battle.json`，表现为 `presentation.json`，完整战斗与建设定义分别为 `combat-content.json`、`construction-content.json`。不得手工维护第二份生效配置。使用方式和当前执行范围见 `Docs/ExcelConfiguration.md`。人物产品配置只选择资源 ID。动画帧范围、帧率、循环、标准动作定义、默认朝向、pivot 与视觉比例是素材制作元数据，随 `UnitAnimationSet` 资产保存和验收，不暴露到产品 JSON，也不在开局时重写。ScriptableObject 可以保存这些固定制作元数据与 Sprite 引用，不承载产品可调规则或对局可变状态。英雄、怪物等可调属性延续 JSON 配置加载约定，DTO 与验证/转换留在基础设施或 Authoring，不把 JsonUtility 带入纯领域层。
配置在创建或替换会话前验证，明确内容版本；共享配置不保存实例当前血量、技能剩余冷却。
保持 Windows/手机可用的加载边界；不要硬编码桌面绝对路径，StreamingAssets 等平台差异由适配器处理。
Sprite Atlas 按加载生命周期/材质/打包需求组织，默认内置交付；无真实需要不引入 Addressables。
图集生成进入构建预处理；Editor 创作与 Runtime 查询分开，使用当前版本 API，实际验证打包而非仅检查文件存在。
资产与 .meta 一起保留，移动时保持 GUID；不手改 Sprite 元数据。美术源文件按 Unity 是否需要导入决定放 Assets 或 SourceArt。
动画通知可触发声音和视觉反馈，逻辑命中、技能许可、死亡与 Boss 阶段不由动画单独裁决。
