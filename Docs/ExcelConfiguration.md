# Excel 内容配置

唯一编辑源：`Config/GameContent.xlsx`。JSON 为导出产物，请不要直接修改。

## 使用

1. Unity 菜单 `Game/内容配置/打开 Excel`，修改数据并保存、关闭工作簿。
2. 选择 `Game/内容配置/导出 Excel`。成功与错误详情显示在 Console。
3. 重新进入 Play Mode，或重新打开地图编辑器，使已加载的只读配置更新。

命令行：`python Tools/ContentConfiguration/export_config.py`。只检查是否与 Excel 同步：附加 `--check`。

导出器只依赖 Python 3 标准库，不需要安装 Excel、openpyxl 或第三方库。Unity 优先读取 `GAME_TD_PYTHON` 环境变量，其次使用菜单 `选择 Python 解释器` 的本机偏好；本机已有的 Codex Python 可直接复用，否则尝试 PATH 中的 python。解释器路径不进入 Player。

## 表结构与输入

第1行为中文字段说明，第2行为稳定字段名，第3行起为配置。可追加数据行，不能改字段名或删必需表。ID 必须唯一，引用按 ID 填写；多个引用以英文分号分隔。TRUE/FALSE 表示布尔值。不要在数据区合并单元格。

不接受公式与 Excel 错误值，避免读取过期公式缓存。时间单位为秒，距离为逻辑地面单位，占地为格，比例为0至1，金币与数量为整数。Excel 有数字和布尔输入验证，导出器会再次完整校验。

| 表 | 模块归属 | 内容与生效范围 |
| --- | --- | --- |
| 单位 | 单位与战斗 | 稳定 ID、基础属性、碰撞半径、显示高度及能力引用；当前三个选定槽位直接生效 |
| 攻击 | 单位与战斗 | 普通伤害、间隔、距离、效果引用；近战攻城间隔/距离与塔的直伤属性生效；Contact 仍按进入接触触发 |
| 技能、效果 | 单位与战斗 | 英雄两个主动槽已接入瞬时范围扣血；冷却来自技能表，范围/伤害来自引用攻击；通用附加效果尚未执行，详见 PlayerOperations.md |
| 怪物行为 | 单位与战斗 | MainCastle/Hero 目标策略、破门许可生效 |
| 单位.animationResourceId | 单位与战斗 Unity 表现 | 直接填写一次整套动画素材名，如 alien-actions；原动画绑定表与 animationId 已删除 |
| 建筑 | 建设与发展，组合战斗能力 | 占地、阻挡、基础生命、攻击引用生效；不可拆除标记用于正式试玩拆除检查 |
| 升级分支 | 建设与发展 | 费用、前置、互斥组和属性增量；普通建筑、主堡升级与兵营扩编已执行，当前工期要求为0 |
| 兵营 | 建设与发展 | 训练单位ID、1级人数、阵亡补兵冷却、驻守距离。兵营扩编升级已执行 |
| 全局参数 | 对局与关卡、地图表现、建设与发展等 | 每行包含说明、单位与生效范围；键名和类型不应修改 |
| 枚举与动作说明 | 配置使用说明，不导出 | 攻击模式、效果类型、目标策略、槽位、布尔值与动作约定 |

当前 Hero/Goblin/Runner 是旧模拟的三个选定槽位，分别对应 Alien/骷髅兵/亡灵；稳定产品 ID 独立于槽位。新增储备单位可留空 slot，其完整定义会导出，但不会自动加入波次。未来扩展生成接口时不需要改变这些内容 ID。

Soldier/Archer 是两个己方士兵槽位，由近战/远程兵营训练，不计入敌人存活数，不攻击英雄、主堡或城门。单位与攻击表设置生命、伤害、距离、间隔、移速和动画资源。近战兵与远程兵默认在兵营附近的分散驻守点待命，不跟随英雄；攻击范围内有敌人时优先停步交战，敌人死亡或离开射程后返回驻守点。跟随英雄的能力保留为显式的会话组装选项，默认不开启。敌人近身后停步反击士兵。

英雄、近战兵和远程兵统一使用我方通行规则：完好城门自动放行，墙体仍阻挡；敌方只有破门后才能通过城门。

`attackId` 引用攻击表的战斗规则（伤害、间隔、距离、效果），需要保留。动画使用 `animationResourceId` 直接选择已导入资源，无需再填写中间绑定 ID。待机、行走、攻击、受伤、死亡统一使用内部动作名 `Idle/Walk/Attack/Hurt/Death`；帧范围与制作参数仍由动画资源拥有。

攻击 `mode` 使用单位与战斗领域的 `AttackMode` 枚举，导出与加载都拒绝未知值及数字枚举值。`Contact` 表示进入接触时触发，`Melee` 表示近战距离内按间隔攻击，`Direct` 表示射程内直接结算。当前分别由英雄接触、怪物攻城和防御塔系统执行，修改 mode 本身不会自动切换系统或生成飞行箭矢。工作簿的枚举说明表与下拉选项提供填表提示。

城门生命已在“全局参数”的 `facilities.wall.gateHealth` 配置。新增 `facilities.wall.wallHealth` 表示每个逻辑墙段的基础生命，起始设计值为300。两者都要求正整数，随 facilities.json 加载到建设与发展中工事的只读 `WallDefinition`。城门已有破门结算；墙段独立受损与摧毁尚未实现，此配置不创建第二份运行血量。

建筑表新增 `category`（PlayerBuildable / Decoration / BuiltIn）和 `placementKind`（Building / Wall / Gate / Bastion）。装饰无战斗功能，内置建筑保留功能但不能由玩家新建或售卖；主堡可配置升级。普通建筑已接入本局费用、升级与售卖退款，建设与升级当前要求工期为0，非零值在导出时拒绝。完整规则和操作见 [战斗建设](BattleConstruction.md)。两类兵营默认建造费用100，升2级150、升3级250，编制4/6/8人。兵营升级只扩编，不修改现有士兵属性；训练按固定模拟时钟推进。

每个阵亡槽位独立等待 retrainSeconds（默认8秒）后训练补员。扩编新增槽位同样等待训练。首批4个名额立即就绪，士兵统一从建筑下方营门外依次出现。入口拥堵时等待，不堆叠生成、不丢名额、不重计冷却。建造会检查下方出口地形净空。兵营被毁停止补员；主动售卖则同时撤离所属士兵。退款只按本局实际投资及配置计算，当前兵营退款率为0。

预置示例地图 `reference-citadel.map.json` 在英雄出生广场两侧放置真实的 `barracks.melee` 与 `barracks.ranged`，开战后各部署4人。民居 house 和教堂 church 即使外观相似，也不具备训练能力。HUD逐座显示当前人数、训练人数及剩余冷却、待出营人数；Inspect点击建筑可查看其真实名称及是否训练士兵。等待出营不代表冷却未完成，通常需要给营门留出地面净空。

全局参数 economy.initialGold 设置每局初始金币，默认1000。金币唯一运行状态归 Economy.ResourceAccount，建设协调器提交成功后支付，失败不扣费。重开恢复初始金币与原始兵营编制，本轮没有引入收入系统。

指定原始目录实际为 soldier_ 和 mage shooter_。前者有行走1～6、挥砍7～9、受击10、死亡11～15帧；后者是法杖射手素材，仅含站立与施法姿势。远程兵沿用该素材，行走使用图像起伏，受击使用统一后仰，死亡使用表现层倾倒收尾。两类兵营建筑暂沿用既有房屋/法师塔外观，编辑器和游戏读取相同的建筑ID与占地规则。

## 导出与目录

全部 JSON 集中在 `Assets/Content/Configuration/Resources/GameConfig/`：

- `combat-content.json`：完整单位、攻击、技能、效果与行为，单位直接携带动画素材引用。
- `construction-content.json`：建设发展与升级定义。
- `battle.json`：现有模拟使用的运行参数投影，由 Excel 能力引用生成。
- `facilities.json`：现有地图编辑与建筑战斗使用的空间、能力投影。
- `presentation.json`：产品表现与动画资源选择。
- `DefenseEncounter.json`、`MapPlayCamera.json`：原有对局和镜头配置。
- `economy.json`：本局金币初值。

投影不是另一套手工配置源。五个原 JSON 文件已通过 AssetDatabase.MoveAsset 迁移，保留 GUID；旧路径不再保留 JSON 副本，代码与资源加载路径同步更新。地图源 JSON 和动画制作资产仍放在各自内容目录，避免把地图与资源制作数据混入全局数值目录。

同次导出的八份 JSON 使用相同的 `contentVersion`，由工作簿实际配置内容计算，便于诊断版本是否一致。

导出先解析所有表、检查数值、ID、资源引用、升级环与跨建筑前置，再准备全部文件。发布逐文件原子替换；普通写入失败会回滚本次修改。错误包含工作表、行号及字段，不输出一半通过校验、一半未通过的配置。文件系统断电等跨文件崩溃原子性不在此机制保证范围。

## 代码入口与状态归属

- `Tools/ContentConfiguration/export_config.py`：唯一导出实现，命令行和 Unity 菜单共用。
- `Assets/Game/Tools/ContentConfiguration/Editor/ContentConfigurationMenu.cs`：打开 Excel、选择解释器、异步导出和资源刷新。
- `Gameplay/Battle/Domain/Definitions/`：AttackDefinition、SkillDefinition、EffectDefinition、EnemyBehaviorDefinition 与只读目录。
- `Gameplay/Construction/Domain/Definitions/`：BuildingDevelopmentDefinition、UpgradeBranchDefinition 与 ConstructionContentDefinition。
- `Infrastructure/Configuration/JsonCombatContentSource.cs`、`JsonConstructionContentSource.cs`：JSON DTO 转纯领域定义。
- `JsonBattleDefinitionSource → UnitDefinition → EcsBattleSimulation.Spawn → CombatProfile`：配置在生成边界烘焙为模拟参数，不逐帧读取文件。
- `MapPlaySession.TryRemove`：使用建设领域的拆除许可；当前生命仍由 ECS 拥有。
- `MapPlaySession.UpdateBarracks / TryUpgradeBarracks`：在步边界协调编制、部署、扩编与账户支付。`BarracksRoster` 保存训练槽和升级选择，`SoldierNavigationSystem / SoldierCombatSystem` 保存战斗结果；`TrySpawnSoldier` 只在营门净空时生成新身份。

Excel、文件系统与 JsonUtility 不进入 Domain；资源编辑器不保存战斗生命、账户余额或实例升级状态。本轮没有引入运行中热更新。

## 验证

使用 `python -m unittest discover -s Tools/ContentConfiguration -v` 验证同步、能力投影、动画素材直连、工事生命、枚举合法性、升级环以及失败时保留旧输出。Unity EditMode 测试验证领域加载、攻击模式枚举、工事生命与空间尺寸独立、非法生命拒绝等行为。
