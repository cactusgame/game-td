# 战斗系统原型

## 运行

打开 `Assets/Scenes/Gameplay/BattlePrototype.unity`，点击 Play。

- 方向键 / WASD / 手柄左摇杆：移动英雄。
- 左下角摇杆：鼠标拖动或移动端触摸操作。支持指针归属，松开、失焦或禁用时归零。
- Restart：重新读取场景引用的 JSON 配置并开始新一局。
- 默认 1 名骑士、6 只普通哥布林、4 只快速小怪。
- 角色为三个透明背景原创静态 Sprite；行走只有表现层轻微起伏和朝向翻转，不包含逐帧动画。

## 配置

修改 `Assets/Content/Combat/Prototype/battle.json`。Unity 重新导入后重启原型即可生效。

`hero/goblin/runner` 支持 `maxHealth`、`attack`、`defense`、`speed`、`radius`、`visualHeight`。
`spawn` 定义两类怪物数量；`arena` 定义逻辑地面宽高；`fixedStep` 定义模拟步长；`seed` 定义随机流。
速度与半径使用实际地面单位，图像大小不参与碰撞。JSON 在转换成不可变定义时验证；非法配置不替换当前模拟。
构建内 JSON 通过序列化 TextAsset 引用加载，不依赖 Windows 路径、Resources 全局查找或同步 StreamingAssets 读取，适用于移动平台。

## 模块与依赖

- `Battle.Domain`：不可变定义、伤害和圆形接触规则；禁用 UnityEngine 引用。
- `Battle.Contracts`：移动意图、稳定 ID、只读快照、输入/配置/模拟端口。
- `Battle.Application`：初始化、重启、固定步推进；不引用 Entities 或表现层。
- `Battle.Simulation`：独立 World、并行随机移动、接触检测、生命真值与快照适配。
- `Infrastructure.Configuration`：JSON DTO、JsonUtility 转换与配置源实现。
- `Presentation`：Sprite 视图、输入适配、uGUI、性能指标；不引用 ECS。
- `Bootstrap`：VContainer 作用域与应用 Tick。只有这一处驱动模拟，World 不追加到默认 PlayerLoop。
- `Tools.Prototype.Editor`：场景搭建、素材导入、图集构建；不进入玩家构建。

英雄和怪物的生命、位置、冷却替代状态都由 ECS 持有，GameObject 不保存第二份玩法状态。
Domain 规则被 Burst 系统调用，DDD 不要求每个单位对应一个托管聚合对象。
模拟每个实例随机流独立，重启恢复种子；当前不承诺跨平台或跨版本确定性。

## 接触语义

英雄与每只活怪物从未接触进入接触时，双方同时各结算一次 `max(0, 攻击力 - 对方防御力)`。
持续重叠不重复扣血；分离后再次接触允许重击；生命最低为 0；死亡后不能移动或攻击。
同一步涉及多个怪物时，双方依据该步接触处理前的英雄属性结算。怪物之间不结算碰撞伤害。
扫掠圆检测使用相对运动，能检测两步之间穿越。单位不实施物理推挤，允许重叠。
`CircleContactSystem` 是可替换的检测与结算入口；当前单英雄对怪物扫描为 O(N)。

## 素材与 Atlas

- 骑士：`Assets/Content/Units/Heroes/Knight/Sprites/knight.png`
- 哥布林：`Assets/Content/Units/Enemies/Goblin/Sprites/goblin.png`
- 快速小怪：`Assets/Content/Units/Enemies/Runner/Sprites/runner.png`
- 图集：`Assets/Content/Shared/Atlases/BattleUnits.spriteatlasv2`

图集采用内置交付，构建预处理器自动更新。Sprite Atlas V2 开启、禁用旋转、4 像素间隔；Android/iOS 配置 ASTC 6x6。
开发场景仍通过源 Sprite 引用，由 Unity 图集打包和绑定机制替换纹理。白块、圆形摇杆和地面是代码生成的简单几何素材。
TextMeshPro 使用 Unity 随附、SIL OFL 许可的 Inter 字体生成静态 ASCII 字体，许可保留在 `Content/Shared/Fonts`。
使用项目自有的最小 SDF UI Shader，不依赖 TMP 额外资源包或 OS 字体文件；将来导入完整 TMP 资源时应统一其 Shader，避免同名定义。

## 性能指标与后续扩容

右上角显示采样 FPS、平均帧时、采样窗口最慢帧、模拟与快照耗时、Unity 已分配/保留内存、托管内存、存活单位数和可用时的 Draw Calls。
Editor 内存包含编辑器开销，Unity 内存计数不是操作系统进程工作集。应在独立 Player 和目标手机上评估最终性能。

逻辑已经使用 Entities + Burst，并行处理移动；当前视图是每单位数个 SpriteRenderer，快照有主线程同步点。
这是方便替换的首版显示适配，不承诺万单位渲染。扩容时优先测量，再替换批量渲染、血条显示、快照收集与接触 Job。
死亡实体首版保留至重启，避免运行时结构变更；长期刷怪时需要另加受控回收。

## 第三方依赖

Entities 通过 UPM 选择当前 Editor 兼容版本。Input System、uGUI、URP 和 Test Framework 使用原项目依赖。
VContainer 1.19.0 来自 OpenUPM，原始归档 SHA1：`1fd3ba2a5e2666ae5364a2827811d011e88504d9`。
嵌入 `Packages/jp.hadashikick.vcontainer`，保留 MIT LICENSE；仅移除了 Runtime asmdef 的可选 ECS/UniTask 集成引用和 versionDefines，采用显式桥接。
Unity Pipeline 是开发与验证工具，不参与领域规则。

## 验证

`Game.Battle.Tests` 包含领域规则、JSON 校验、ECS 接触、同步致死、固定步移动与随机重置测试。
验证结果与截图记录在 `Docs/Validation`。移动输入代码已实现，但实际 Android/iOS 构建和设备性能尚需目标平台模块与真机验证。
