# 战斗 JSON 配置

战斗配置统一位于 `Assets/Content/Combat/Prototype/`：

- `battle.json`：英雄/怪物属性、出生数量、地图逻辑尺寸、固定步长、随机种子、导航格长与追踪参数。
- `presentation.json`：`Death` 死亡播放与停留时间，`Map` 相机视野、边框、棋盘格和颜色，以及 `Knight` / `Goblin` / `Runner` 动画帧率和画布内身体高度比例。

两个文件都使用 `schemaVersion: 1`，JSON 字段名区分大小写。配置通过场景的 TextAsset 引用加载，可随构建交付，不读取桌面绝对路径。编辑并等待 Unity 导入后，重启对局或重新进入 Play Mode 生效；运行中不会每帧解析 JSON。

`navigation.detectionRadius = 5`：尚未追踪的怪物与英雄地面中心距离 ≤5 世界单位时开始追踪。
`navigation.loseTargetRadius = 5`：已经追踪的怪物距离 >5 世界单位时停止并恢复随机游走。两种怪物各自判断；英雄或怪物死亡也会终止追踪。可将脱离距离调为 7，进入距离仍保留 5，以避免边缘处反复切换。脱离距离必须 ≥进入距离，所有半径与重算间隔必须有限且 >0。

`Death.AnimationSeconds = 0.6`，`Death.CorpseHoldSeconds = 1.5`：首次显示死亡起总计 2.1 模拟秒后移除视图。暂停冻结计时；重启清除旧视图和计时。

`UnitAnimationSet` 的 `.asset` 仅保存 Idle、Move、Attack、Hurt、Death 的 Sprite 帧引用，属于表现资源。旧的 `unit-death-settings.asset` 与 `map-view-settings.asset` 已移除。重新导入图片不会覆盖 JSON 数值。

规则调用链：`BattleSession.Restart → JsonBattleDefinitionSource → BattleDefinition → ECS`。
表现调用链：`BattleView` 在新对局代次读取 `presentation.json → BattlePresentationSettings → UnitView / BattleArenaView / BattleCameraFraming`。表现参数留在 Presentation 边界，纯领域和共享导航地图不依赖 Unity 或 JSON API。

本次验证：Unity 编译通过，62 项编辑器测试全部通过。Play Mode 验证两种怪物从 Death_0 播放至 Death_3，读取 JSON 中的 0.6 秒动画和 1.5 秒停留后移除，单位视图从 1001 减至 999，重启恢复 1001。验证后退出 Play Mode。
