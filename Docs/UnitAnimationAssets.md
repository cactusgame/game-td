# 单位标准动作与亡灵素材

## 当前使用

地图试玩入口仍是 `Assets/Scenes/Development/DefensePlayground.unity`，编辑器试玩同样生效。

英雄绑定已导入的 Alien；两种怪物分别绑定 skeleton（骷髅兵）、fallen（亡灵）。产品资源选择仍在 `Assets/Content/Combat/Prototype/presentation.json`，保留现有 Hero / Goblin / Runner 模拟槽位的兼容性：Goblin 槽绑定 skeleton-actions，Runner 槽绑定 fallen-actions。本次没有批量重命名模拟类型和 JSON 数值键，也没有调整单位速度、半径等属性。每入口每波交替选择两个槽位，总数量和波次时间不变。

## 素材识别记录

原始目录是 `D:/projects/game-td-arts/王国守卫战/王国守卫战/动作/skeleton_` 与同级 `fallen_`，本轮已逐帧查看联系表。复制进仓库后，构建和运行不依赖外部绝对路径。

| 素材 | Idle | Walk | Attack | Death | Spawn |
| --- | --- | --- | --- | --- | --- |
| skeleton，65 帧 | 13 | 1–12 | 14–22 | 23–33 | 34–65 |
| fallen，91 帧 | 37 | 1–12 | 38–48 | 49–59 | 60–91 |

fallen 的 13–24、25–36 是其他方向行走。当前仍采用左右朝向与水平翻转，不把其他方向帧混入一段行走。两套新素材没有独立受伤序列；Hurt 显式复用 Idle 单帧短暂停顿，并复用受击闪色反馈。这是素材不足时的适配，不是已识别出独立受伤动作。

原始画布保留，不逐帧裁切。Skeleton 普通脚底在画布下方 11 像素，fallen 在下方 6 像素；fallen 出土素材是双倍分辨率，用双倍 PPU 校准，避免切换缩放突变。片段帧率、帧范围、脚底锚点和视觉比例在 `UnitAnimationSet` 资产保存，不放进产品 JSON。

图集继续沿用 `BattleAtlasPrebuild` 的 SpriteAtlas V2 构建前流程，实际打包 306 个 Sprite，并已验证三套资源的 CanBindTo。不是运行时合成图片。

## 状态与阅读路径

- 语义归单位与战斗：生命归 ECS；攻击与受伤时点来自 `UnitSnapshot.LastAttackTime/LastHitTime`。动画不裁决命中。
- `Gameplay/Battle/Unity/Assets/UnitVisualCatalog.cs`：Unity 表现资源与现有 JSON 的持久引用。
- `Gameplay/Battle/Unity/Views/BattleUnitPresentation.cs`：从快照创建与回收表现，按标准动作资源绑定。
- `Presentation/Views/UnitView.cs`：已有共享播放器，处理移动、攻击、受伤、死亡帧与血条；英雄现在也播放死亡动作。
- `Tools/MapPlaytest/Unity/Views/MapPlayUnitView.cs`：仅适配统一表现，删除原静态 Sprite 实现。
- `Tools/Prototype/Editor/UndeadAnimationImport.cs`：可重复导入和动作映射，沿用 Sprite Editor 数据提供器与能力检查。

死亡时隐藏血条，播放非循环 Death，末帧按 `Death.CorpseHoldSeconds` 停留（当前 1.5 秒）后移除视图。已移除 ID 不会因模拟保留死亡快照而重建，重试清空移除记录。终局继续推进收尾表现时钟，模拟停止；正常对局没有模拟步时不会因渲染帧重复而重置动作。

## 验证

编译通过；199 项 EditMode 测试通过。覆盖原有规则，以及 Alien、骷髅兵、亡灵的死亡采样、模拟暂停时冻结帧、末帧停留与移除时点。图集实际打包与资源绑定已验证。

实际试玩已观察到 Alien 待机与两种怪物的行走、攻击、死亡帧，Console 无运行错误。截图保存于项目临时目录 `Temp/animated-units.png`。
