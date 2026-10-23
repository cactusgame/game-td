# 最小主堡防守闭环

试玩入口：`Assets/Scenes/Development/DefensePlayground.unity`。地图工具的试玩也采用同一套规则。
WASD 移动英雄，Build tower 后点击空地建箭楼；Restart from map 恢复源地图和全部对局状态。

## 规则

- 每个敌人入口开局生成 5 只哥布林；模拟时钟到第 10 秒，再生成 10 只。两批为整批生成，不等待首波清空。
- 第二波已生成且所有怪物死亡才胜利。任意一座主堡生命为零即失败；同一步防守失败优先。
- `hall` 是“曙光主堡”的稳定定义 ID。地图校验和试玩要求至少一座；草稿仍可保存。
- 有主堡时怪物选择可达的主堡攻击位置，不受英雄感知距离限制。缺少可达攻击位置时等待，不改追英雄。
- 现有英雄接触战斗保留；英雄死亡不是本玩法的失败条件。
- 箭楼自动寻找射程内怪物并直接结算伤害；怪物接近主堡后每秒攻击。
- 终局停止模拟并拒绝建设、拆除、门操作。防守主堡不能主动拆除。

## 状态归属和调用顺序

`MapPlayController → MapPlaySession → BattleSession.Advance → EcsBattleSimulation.Step`。

`Encounter.Domain/DefenseEncounter` 拥有波次提交进度与终局结果，不拥有生命。`MapPlaySession` 通过公开战斗端口协调对局，初始波次在候选会话初始化内完成后才切换旧局。

每个固定步：提交到期波次 → 怪物目标选择 → 导航 → 移动与地形碰撞 → 英雄接触战斗 → 箭楼攻击与攻城伤害 → 城门后果 → 胜败检查 → 快照。

`MonsterObjectiveSystem` 选择主堡及可达攻击位；`MonsterNavigationSystem` 只生成路径。`StructureCombatSystem` 顺序结算建筑伤害。建筑生命与攻击时钟只保存在 ECS `DefenseStructure`，应用层和 HUD 读取快照。地图实例 ID 与 ECS Entity 的映射由桥接器持有，重试后重建。

## 配置入口

- `Assets/Content/Encounters/Prototype/Resources/DefenseEncounter.json`：两波数量与间隔。控制器可显式指定 TextAsset，未指定时从 Resources 加载。
- `Assets/Content/Maps/Shared/facilities.json`：设施占地、最大生命、攻击、射程、攻击间隔。箭楼初始攻击 25、间隔 1 秒、射程 5；主堡生命 500。
- `Assets/Content/Combat/Prototype/battle.json`：英雄与怪物属性。地图防守不使用压力测试场景的初始怪物总数。
- `Assets/Content/Maps/Example/defense.map.json`：干净的双入口防守示例。原 `example.map.json` 保留，当前存在建筑与墙重叠，须修正后才能试玩。

## 当前范围

本轮完成波次、主堡目标、建筑攻击与胜败。建造仍沿用试玩免费放置；资源支付、奖励、升级、研究尚未接入。箭楼首版使用直伤，尚无投射物或射击遮挡判定。主堡毁坏目前以生命归零和终局 HUD 表达，未增加废墟动画。静态墙无路时仍等待，城门沿用现有破门机制。

验证：`DefenseLoopTests` 覆盖双入口数量与间隔、重试、地图缺主堡、主堡被毁、英雄不被追击、两波清空胜利；其余 EditMode 测试回归现有地图与战斗行为。

本轮 Unity 6000.6.4f1 编译无错误、无警告，187 项 EditMode 测试全部通过。实际 `DefensePlayground` 试玩验证：原始单塔布局在约 22.6 秒失败；重试后通过公开建造入口补建 12 座箭楼，约 19.1 秒消灭两波，主堡仍有 500 生命；运行期间未产生新增 Console 错误。这是行为验证，不代表平衡性或性能验收。

## 怪物群体间距

同波保持同时生成，不通过延后刷怪改变波次规则。`EcsBattleSimulation.SpawnEnemies` 从入口开始按同心环搜索位置，检查地形净空、入口可达性，以及与现有活单位和本次新单位的距离。极端拥堵无法容纳整波时，选择重叠最少的可达合法点，再由运行中的分离求解持续疏散。

移动顺序增加为 `MovementSystem → MonsterSeparationSystem → TerrainCollisionSystem → 接触与攻击`。分离系统每步从活怪物快照构建空间哈希，每只怪物只查询周围九个桶；并行 Job 读取同一快照并只输出自己的修正。完全重合的单位使用稳定 ID 对生成相反方向，避免零向量导致永远无法分开。修正速度有上限，死亡单位既不推挤也不被推挤。地形扫掠对主动移动与分离的合计位移执行约束，因此不会以避让为由穿墙。

`battle.json.navigation.separationGap` 是两怪物地面半径以外的期望间距（默认 0.12），`separationSpeed` 是每秒最大位置修正长度（默认 3）。两者属于战斗移动规则，不是画面偏移。空间不足时允许临时压缩，不承诺在任何拥堵中都保持刚体级别的不重叠；不同美术图片仍可能因高度产生合理遮挡。

算法验证见 `MonsterSeparationTests`（完全重合、五只疏散、死亡排除、墙边约束）与 `DefenseLoopTests.SameWaveSpawnsApartAndKeepsVisibleSpacingWhileFollowingTheCastle`（同波生成及持续寻路间距）。空间哈希避免常规全场两两比较，极密集桶仍需额外测量，未据此承诺千怪或手机性能。

本次间距修复后编译通过，192 项 EditMode 测试全部通过。实际试玩确认模拟时钟持续推进，同波怪物可辨识地分开移动，Console 无运行错误。验证截图位于项目临时目录 `Temp/crowd-separation.png`，不作为游戏资源导入。
