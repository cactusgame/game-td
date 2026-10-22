# 环境素材 V3：林地石堡

2026-10-22。高清手绘卡通环境素材，偏高斜俯视，暖灰石墙、橄榄绿草地、赭色土路、青绿河水。人物不在本轮范围。使用内置 ImageGen 创作，原图保存在项目内，不依赖生成工具的外部缓存。

## 素材入口

`Assets/Content/Maps/ArtV3/` 共 9 张 PNG，通过 Unity Sprite 数据提供器导入为 17 个 Sprite。没有手工编辑 `.meta`。

| 文件 | 内容与使用方式 |
| --- | --- |
| wall-masonry.png | 城墙与程序化敌台共用的侧面石材 |
| wall-walkway.png | 独立的墙顶与敌台平台铺地 |
| meadow.png | 连续草地底纹 |
| earth-road.png | 夯实土路、泥土地面与现有河岸过渡底层 |
| river-water.png | 最终选用的低高光静水纹理；当前为静态水面 |
| gate-closed.png | 石拱门、铁箍双木门，关闭外观 |
| gate-open.png | 同画布、同切片、同锚点的开启外观；门洞有真实 Alpha |
| woodland-obstacles.png | 大橡树、针叶树、幼橡树、苔石、碎石堆、倒木六种障碍物 |
| fortification-components.png | 横墙、圆敌台、方敌台、端柱四种造型源与缩略图 |

连续墙身与敌台继续使用现有共用几何，新增石材用于其立面与顶面。造型源中的独立墙段/敌台图片不替代任意方向的连接几何。三种树和两种石块已接入地图变体；倒木先作为可复用美术资源，未引入新的地形或碰撞类型。

现有 `Assets/Content/Maps/Shared/MapVisualCatalog.asset` 已切换以上地表、树石与墙材质引用。原有建筑、墙体制作尺寸、导航和地图文档保持原值；旧 ArtV2 图片仍保留。当前地表渲染器共用泥土与道路的表面采样，因此这两类暂用相同土路底纹，没有修改其地形规则。

## 墙门的交付边界

后续玩法接入已完成：编辑器可放置、移动、移除和保存横向城门，试玩可开关并更新导航，详见 [地图编辑器的城门说明](MapEditor.md#城门artv3)。`Docs/Validation/gates-v3.png` 是已接入门状态后的实际渲染；下面描述保留最初美术交付时的范围。

已完成横向墙线上使用的正面开、关两态素材。当前地图没有墙门的放置、状态和通行契约，本轮没有补充这类玩法。验收图里的墙门是临时场景中的美术摆放，不是可交互的门，也不保存到地图墙网。其他方向门的透视及动态遮挡需随墙门接入再制作和验收。

两态使用统一 `1120×820` 切片，避免分别裁剪造成显示尺寸变化；生成图不作为已经严格对齐的逐帧动画。障碍物图集按实际轮廓单独切片，而非假定等分网格。Sprite 使用中心 pivot，脚底由现有 `MapSceneSprites.AddFoot` 的显示位置计算，避免把脚底偏移计算两次。

## 实际画面与制作路径

- `Docs/Validation/environment-v3-overview.png`：2400×1600 实际地图渲染总览。
- `Docs/Validation/environment-v3-detail.png`：墙面、墙顶、敌台和门框近景。
- `Assets/Content/Maps/ArtV3/environment-showcase.map.json`：独立的环境样例地图，无人物或旧建筑；墙门、倒木仅在截图场景摆放。
- `SourceArt/MapEnvironmentV3/ImportEnvironment.cs`：导入设置、能力受检的 Sprite 切片、引用更新及画布缓存刷新。通过 Unity Pipeline `run_script` 执行 `ImportEnvironment.Main`。
- `SourceArt/MapEnvironmentV3/CaptureEnvironment.cs`：通过实际 `MapWorldView` 在临时附加场景截图，执行 `CaptureEnvironment.Main`；结束恢复原活动场景。
- `SourceArt/MapEnvironmentV3/Prompts.md`：实际生成提示词全集。
- `SourceArt/MapEnvironmentV3/RiverRefinement.md`：河水最终修订提示词与选用源文件。

地表流程：`MapVisualCatalog → MapSurfaceGeometry → MapSurfaceView`。墙体流程：`MapVisualCatalog 的两种石材 → MapRaisedWallPresentation / MapWallNodePresentation → MapWallMeshView`。编辑器与游戏仍使用同一套表现生成逻辑。

## 本轮验证

已执行 Unity 6000.6.4f1 下的导入脚本编译与执行、Sprite 引用检查、透明通道检查，以及实际地图总览和近景渲染。打开门洞的中部像素 Alpha 为 0；树石在实际地图上无矩形背景。河水第一版高光过亮，已通过 ImageGen 修订并重新截图检查。

没有修改运行时代码，也未进行完整回归测试、Player 构建、移动端性能或新 Sprite Atlas 打包验收。素材当前沿现有源纹理引用方式使用。本轮图像证明当前地图渲染下的搭配与比例，不代替墙门玩法、全方向门透视或战斗遮挡验收。现有网格轮廓的河岸和道路仍可见阶梯形态；这是当前地形过渡几何的效果，后续可独立细化。
