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
| 技能、效果 | 单位与战斗 | 导出并加载为只读定义；主动施放、效果执行系统尚未接入，示例技能默认禁用 |
| 怪物行为 | 单位与战斗 | MainCastle/Hero 目标策略、破门许可生效 |
| 动画绑定 | 单位与战斗 Unity 表现 | 选择已有动画资源 ID；不编辑制作帧范围、帧率或 pivot |
| 建筑 | 建设与发展，组合战斗能力 | 占地、阻挡、基础生命、攻击引用生效；不可拆除标记用于正式试玩拆除检查 |
| 升级分支 | 建设与发展 | 费用、时间、前置、互斥组和属性增量定义；升级执行系统尚未接入 |
| 全局参数 | 对局与关卡、地图表现等 | 保留现有参数，集中编辑；键名和类型不应修改 |

当前 Hero/Goblin/Runner 是旧模拟的三个选定槽位，分别对应 Alien/骷髅兵/亡灵；稳定产品 ID 独立于槽位。新增储备单位可留空 slot，其完整定义会导出，但不会自动加入波次。未来扩展生成接口时不需要改变这些内容 ID。

新增的建造费用、建设时间、退款比例、升级分支及技能效果示例是起始设计值。当前建设仍沿用免费即时试玩流程，尚无经济账户与建设队列，填写费用和时间不等于这些机制已经实现。技能效果和升级规则同样只是本轮完成配置与加载，不伪装为已经执行的玩法。

## 导出与目录

全部 JSON 集中在 `Assets/Content/Configuration/Resources/GameConfig/`：

- `combat-content.json`：完整单位、攻击、技能、效果、行为及动画绑定内容。
- `construction-content.json`：建设发展与升级定义。
- `battle.json`：现有模拟使用的运行参数投影，由 Excel 能力引用生成。
- `facilities.json`：现有地图编辑与建筑战斗使用的空间、能力投影。
- `presentation.json`：产品表现与动画资源选择。
- `DefenseEncounter.json`、`MapPlayCamera.json`：原有对局和镜头配置。

投影不是另一套手工配置源。五个原 JSON 文件已通过 AssetDatabase.MoveAsset 迁移，保留 GUID；旧路径不再保留 JSON 副本，代码与资源加载路径同步更新。地图源 JSON 和动画制作资产仍放在各自内容目录，避免把地图与资源制作数据混入全局数值目录。

同次导出的七份 JSON 使用相同的 `contentVersion`，由工作簿实际配置内容计算，便于诊断版本是否一致。

导出先解析所有表、检查数值、ID、资源引用、升级环与跨建筑前置，再准备全部文件。发布逐文件原子替换；普通写入失败会回滚本次修改。错误包含工作表、行号及字段，不输出一半通过校验、一半未通过的配置。文件系统断电等跨文件崩溃原子性不在此机制保证范围。

## 代码入口与状态归属

- `Tools/ContentConfiguration/export_config.py`：唯一导出实现，命令行和 Unity 菜单共用。
- `Assets/Game/Tools/ContentConfiguration/Editor/ContentConfigurationMenu.cs`：打开 Excel、选择解释器、异步导出和资源刷新。
- `Gameplay/Battle/Domain/Definitions/`：AttackDefinition、SkillDefinition、EffectDefinition、EnemyBehaviorDefinition 与只读目录。
- `Gameplay/Construction/Domain/Definitions/`：BuildingDevelopmentDefinition、UpgradeBranchDefinition 与 ConstructionContentDefinition。
- `Infrastructure/Configuration/JsonCombatContentSource.cs`、`JsonConstructionContentSource.cs`：JSON DTO 转纯领域定义。
- `JsonBattleDefinitionSource → UnitDefinition → EcsBattleSimulation.Spawn → CombatProfile`：配置在生成边界烘焙为模拟参数，不逐帧读取文件。
- `MapPlaySession.TryRemove`：使用建设领域的拆除许可；当前生命仍由 ECS 拥有。

Excel、文件系统与 JsonUtility 不进入 Domain；资源编辑器不保存战斗生命、账户余额或实例升级状态。本轮没有引入运行中热更新。

## 验证

工作簿已导出并检查布局。6 项 Python 测试覆盖同步检查、修改攻击/建筑生命后的输出、非法 ID/引用、数值、升级环以及公式错误时保留旧输出；202 项 Unity EditMode 测试通过，新增测试验证两个领域的配置加载、稳定单位 ID、行为参数和升级环拒绝。Unity 编译无错误、无警告。
