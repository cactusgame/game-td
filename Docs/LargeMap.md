# 大地图、跟随相机与边缘

游戏地图扩大到 96×54 世界单位，中心为 (0, 0)，范围为 X∈[-48, 48]、Y∈[-27, 27]。英雄出生和重启位置保持中央。英雄和怪物的移动边界由 ECS 按地图半尺寸减各自碰撞半径计算；怪物在完整范围内出生、游走和追踪。

寻路格长保持 1，因此导航地图为 96 列 × 54 行，共 5,184 格。修改格长只改变寻路采样；地图外观的棋盘格独立配置。此次仍没有 Tilemap 或地图编辑器。

## 配置入口

地图逻辑尺寸和寻路参数：`Assets/Content/Combat/Prototype/battle.json` 的 `arena`、`navigation`。

相机与视觉参数：`Assets/Content/Maps/Prototype/map-view-settings.asset`。

| 参数 | 默认值 | 含义 |
| --- | --- | --- |
| Visible World Height | 18 | 单屏可见的世界高度 |
| Edge Margin | 0.75 | 地图边缘外允许显示的留白 |
| Top Edge Margin | 2 | 上沿额外留白，避免完整角色和血条被镜头裁切 |
| Border Thickness | 0.3 | 外沿金色边框宽度 |
| Ground Pattern Size | 1 | 视觉地面棋盘格大小，与寻路格长独立 |

16:9 下单屏显示约 32×18 世界单位，96×54 对应约 3×3 屏。横竖屏和超宽屏按实际宽高比显示不同的世界宽度，世界尺寸保持不变。

相机跟随英雄的连续地面位置，不再为了显示整个地图而拉远。接近边缘时，相机中心被限制在地图可显示范围内，并保留足够留白观察边框；某一方向的地图小于视野时，该方向居中。

## 调用与状态归属

`battle.json` → `BattleDefinition` → `EcsBattleSimulation.Reset` 设置移动边界、寻路地图和出生位置。

`BattleSession` 快照 → `BattleView.LateUpdate` → `BattleCameraFraming.Center`，相机读取英雄位置，不回写 ECS。对局加载/重启时，`BattleView` 调用 `BattleArenaView.Configure`，按本局地图大小重建视觉地面和边框。

`BattleMapViewSetup.Apply()` 用于在当前场景更新编辑器地面预览及配置引用，不重建 UI 或单位资源。编辑器预览和运行时复用 `BattleArenaMesh.Build`；运行时使用单独的 Mesh，并在场景销毁时释放，不修改源网格资产。

修改地图或边框配置后重启对局，运行时地面会使用新配置。编辑器预览可再次执行 `BattleMapViewSetup.Apply()`。

## 验证

Unity 编译通过，编辑器测试 49 项全部通过，包括大地图四角移动、中央出生与重启、完整导航覆盖，以及横屏/竖屏/超宽屏的相机边界。

当前场景 Play Mode 检查了 1,001 个单位（英雄 + 配置中的 1,000 只怪物）。中央相机位置为 (0, 0)，右下角相机位置约为 (32.75, -18.75)，视野高度始终为 18，边框可见。验证后恢复正常时间和出生状态，再退出 Play Mode。

运行画面保存在 `Docs/Validation/large-map-center.png` 和 `Docs/Validation/large-map-edge.png`。截图仅用于检查地图显示，不是性能基准。

## 上沿只剩阴影的缺陷修复

旧渲染全部使用 Default Sorting Layer。单位身体的排序值是 `1000 - Round(Y * 50)`，地面为 -100、阴影为 0。小地图没有触发问题；扩大到 Y=27 后，Y>22 的单位排序比地面更低，身体和血条被不透明地面覆盖，阴影仍绘制在地面上方。死亡动画也使用了同一错误公式。

现在地面、阴影和单位分别使用 Battle Ground、Battle Shadows、Battle Units 三个 Sorting Layer。单位的 Y 排序仅比较单位间前后关系，不再影响其是否在地面上方；活体、尸体和血条共用一处深度计算，并限制在渲染器合法整数范围内。排序层通过编辑器序列化 API 创建，现有层及其 ID 保留。

同时新增 Top Edge Margin=2，给站在上沿的英雄头部和血条留出空间，下沿/侧沿留白仍为 0.75。

本次编译通过，55 项编辑器测试全部通过；Play Mode 在英雄 Y=26.58、两种怪物 Y=24 的场景复现并验证修复。英雄的完整 Sprite 包围盒和血条均落在视野内。修复前后画面分别为 `Docs/Validation/upper-edge-before.png`、`Docs/Validation/upper-edge-after.png`，验证后已恢复对局并退出 Play Mode。
