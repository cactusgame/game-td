# 人物动画：产品选择与素材制作

## 产品只选择素材

配置入口：`Assets/Content/Combat/Prototype/presentation.json`，当前 `schemaVersion: 3`。对于 Hero，配置就是：

```json
{ "UnitKind": "Hero", "ResourceId": "alien-actions" }
```

把 `ResourceId` 改为已登记的 `knight-actions` 就切换为骑士，重启对局后生效。产品无需知道任何动作的帧范围、帧率、循环方式、默认朝向或透明画布比例；这些都跟随选中的素材。现有场景已登记 Alien、Knight、Goblin、Runner 四套资源。

产品仍可调整尸体停留等表现策略，但死亡动画本身的时长只由素材的帧数和帧率决定。

## 素材制作一次，标准动作供程序调用

制作资产：`UnitAnimationSet`，实际资源为：

- `Assets/Content/Units/Heroes/Alien/alien-actions.asset`
- `Assets/Content/Units/Heroes/Knight/Sprites/knight-actions.asset`
- `Assets/Content/Units/Enemies/Goblin/Sprites/goblin-actions.asset`
- `Assets/Content/Units/Enemies/Runner/Sprites/runner-actions.asset`

每套资产包含按源编号排列的 Sprite 引用，以及固定字段 `Idle`、`Walk`、`Attack`、`Skill` 等。动作名字由 `UnitAction` 统一约定，不能为不同人物另配一组字符串名称。

| 标准动作 | 含义 | 要求 |
| --- | --- | --- |
| Idle | 待机 | 必需 |
| Walk | 行走/基础移动 | 必需 |
| Attack | 普通攻击 | 必需 |
| Skill、Skill2、Skill3 | 技能 | 可选，怪物可以没有 |
| Run、Jump、WalkBack、Spawn | 跑步、跳跃、背向移动、出现 | 可选 |
| Hurt、Death | 受伤、倒地 | 可选 |

程序使用 `player.Find(UnitAction.Skill)` 查询可选动作；缺少动作时结果为 null，调用方不播放。缺失必需动作属于未制作完成的资源，开局和构建前均拒绝使用。当前技能、跳跃等还没有玩法触发流程，预览中可查看；动画不会自行生成玩法。

每个动作的 `FirstFrame / LastFrame / Fps / Loop` 是制作元数据：编号从 **1** 开始，包含首尾帧。可选动作留空（Unity 内联序列化中的全零对象也视为空）。身体高度占画布的比例、默认朝向也由素材作者校准一次，不再成为产品负担。

这些定义验收后随素材版本固化；运行时只读采样，不修改资产。需要修正错误分段时，由素材作者在 Unity Inspector 中修改该资产并重新验收，不通过产品 JSON 调整，也不通过导入工具反复覆盖。

## Unity 中的制作与预览流程

1. 美术或技术美术导入源帧，保持数字顺序、统一 PPU、画布与脚底 pivot。独立 PNG 使用 Single；整张序列图先在 Sprite Editor 使用 Multiple 正确切片。
2. 创建或选择 `UnitAnimationSet`，设置 Sprite 引用，并在已有的标准动作字段中制作起止帧、帧率和循环。无需填写动作名称。
3. 打开 **Game > Animation Preview**，选择资源，直接选择 Idle / Walk / Attack 等标准动作预览。可拖动 Source frame 核对原始帧。窗口只读展示制作结果，已移除产品 JSON 编辑、任意动作命名和保存配置功能。
4. 验收后把资源加入场景 `UnitViews / BattleView / Animation Sets` 内容目录。此登记是素材接入工作，只做一次。
5. 产品随后只需在 `presentation.json` 中为单位选择这套资源的名字。

`AlienAnimationImport` 重跑只更新 Sprite 引用和固定导入设置，不覆盖已制作的动作、帧率或视觉校准。`BattleAnimationSetup` 同样保留已有素材动作与手调 Sprite 边界。

## 原始小图与上线合图

小 PNG 是创作源；交付纹理由 **SpriteAtlas V2** 真正合并。无需先生成另一份拼接 PNG 再重复打包。

真实入口：`Assets/Game/Tools/Prototype/Editor/BattleAtlasPrebuild.cs`。它实现 `IPreprocessBuildWithReport`，构建前扫描 `Assets/Content/Units` 下的动画素材资产，校验标准动作和帧引用，并将 Sprite 去重后加入 `Assets/Content/Shared/Atlases/BattleUnits.spriteatlasv2`。不再把整个 Units 文件夹的未使用图片一并打包。

图集 `includeInBuild = true`，随 Player 内置交付；Android/iPhone 使用现有 ASTC 设置。SpriteRenderer 保持源 Sprite 引用，Unity 将其绑定到打包纹理，动画切帧不要求产品处理图集坐标。运行时没有导入、合图或重新打包流程。

一个 SpriteAtlas 可按最大纹理尺寸生成多个 page；合图不等于所有人物必须塞进一张无限大的纹理。未来按关卡或加载生命周期拆分图集时，产品的资源选择格式仍保持不变。当前未引入 Addressables。

## Alien 当前制作结果

源目录 `D:/projects/game-td-arts/王国守卫战/王国守卫战/动作/hero alien_` 有 94 张 336×224 PNG。本项目保留原像素，重命名为 `alien_001` 至 `alien_094`，共用脚底 pivot `(168, 68)`（左下角坐标）。编号总览：[alien-frames.png](Validation/alien-frames.png)。

| 帧范围 | 标准动作 | 画面依据 |
| --- | --- | --- |
| 1–6 | Walk | 正向跑动循环 |
| 7–21 | Attack | 举刃、连续横斩、回收 |
| 22–27 | Skill | 投掷/远程攻击姿态 |
| 28–34 | WalkBack | 背向跑动 |
| 35–37 | Idle | 静止站立 |
| 35 | Hurt（复用） | 未找到独立受伤片段，配合原有闪红 |
| 38–46 | Skill2 | 操作手臂装置、紫色投射/扫描 |
| 47–74 | Skill3 | 蓄能后大型爆炸并消散 |
| 75–80 | Death | 击飞、翻倒、躺下 |
| 81–94 | Spawn | 光柱/传送后恢复站立 |

这些分段基于逐帧画面观察；具体技能名称与原始播放速度未得到原作者标签确认。它们已作为素材制作结果保存，不要求产品判断。英雄死亡仍按现有玩法隐藏，未额外添加复活等玩法。

## 运行调用链与状态归属

`BattleSession` / ECS 固定步推进 → `UnitSnapshot` → `BattleView.LateUpdate` 读取单位的 ResourceId 并查内容目录 → `UnitAnimationPlayer` 从素材资产解析标准动作 → `UnitView.Render` 按移动、攻击、受伤事实选择 `UnitAction` → `UnitAnimationClip.Frame` 按模拟时间采样。

资源定义和只读片段共享。每个 UnitView 仅拥有移动循环起点、死亡表现起点等临时显示状态；权威位置、生命、伤害和事件时间仍属于模拟。死亡动画播放结束并停留 `CorpseHoldSeconds` 后删除视图；暂停冻结动画和停留计时。重启清理旧视图并重新读取产品的资源选择。

代码阅读顺序：`UnitAnimationSettings`（产品只选资源）→ `UnitAnimationSet` / `UnitAction`（素材标准动作）→ `BattleView` / `UnitView`（运行时播放）→ `BattleAtlasPrebuild`（构建交付）。

## 验证边界

本次重新执行脚本编译与 `Game.Battle.Tests` 的 81 项 EditMode 测试，全部通过。测试覆盖只改 ResourceId 即切换整套动作、怪物缺少 Skill、必需动作缺失、错误分段、动画顺序、暂停、死亡停留和渲染层级。

本次实际打包得到 150 个 Sprite、3 个纹理 page，Alien 全部 94 帧可绑定，SpriteAtlasV2 已启用且 includeInBuild 为 true。Play 中英雄的 Sprite.packed 为 true，实际纹理名称为 BattleUnits 图集页。

运行时仅替换产品 ResourceId 并重启，已验证 Alien → Knight → Alien 的完整切换；帧率和视觉校准随素材切换，未传入任何制作参数。当前已退出 Play，恢复 Alien 配置。运行截图：[alien-battle.png](Validation/alien-battle.png)。

预览窗口已验证标准 Attack 对应 alien_007，滑条可查看 alien_094，窗口中没有 FPS 编辑框。重复执行 Alien 导入和场景资源更新后，alien-actions.asset 的文件字节完全一致，确认不会重写已验收的素材定义。

未执行 Player 构建或手机设备验证；图集页数不代表最终各目标平台的构建结果。
