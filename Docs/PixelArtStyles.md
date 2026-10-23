# 像素美术套装

入口：`Game/地图编辑器`，左侧「美术套装」选择 `PixelKingdom` 或原来的 `MapVisualCatalog`。「参考图城镇地图」打开新地图；「独立试玩」继承所选套装，试玩 HUD 也可切换。

原高清图片和目录保留。新增套装放在 `Assets/Content/Maps/Styles/PixelKingdom/`。未来添加 MapVisualCatalog 资产到 `Assets/Content/Maps` 下即可自动出现在列表。各套装引用同一份 facilities.json，稳定 definitionId、占地和碰撞规则共用。切换偏好存 EditorPrefs，不写入地图、不增加撤销记录。试玩场景直接引用套装资源，运行时不使用 AssetDatabase。

新增内容：民居、教堂、集市、水井、瓦砾五种可放置内容；其规则加入共用 facilities.json。旧套装缺少村落图片时用已有建筑代替，瓦砾使用旧套装岩石代替。新增可选墙节点塔楼图片与建筑视觉宽度制作参数，不改变物理占地。城墙、城门与遮挡继续使用现有共享几何能力。

新地图为64×44，双层四向城门、中心主堡、民居、教堂、集市、麦田、铁匠铺、水井、树林与瓦砾。它是参考图构图的可编辑起点；墙高仍遵循现有规则，未复刻截图中的高大城门楼、旗帜、攻城梯和破墙动画。士兵素材未改。水面暂沿用原套装。

阅读路径：MapArtSelection（发现与选择）→ MapEditorWindow（画布及调色板重建）→ MapEditorSetup.StartPlaytest（传递选择）→ MapPlayController.SwitchArt（只重建表现）→ MapWorldView / MapSceneSprites（共用渲染）。PixelArtSetup 是资源制作与示例地图生成入口。

素材使用内置 image_gen 生成，源图 kingdom-sheet.png 保留透明通道，导入使用 Point、无 mipmap、无压缩。提示词：中世纪塔防，正交略俯视，清晰像素簇、有限色板、蓝色屋顶、米色灰泥和灰石、橄榄绿树木；4×4素材表依序为主堡、民居、教堂、集市、铁匠铺、塔楼、麦田、水井、阔叶树、松树、岩石、瓦砾、草地、土路、砖墙、铺石；透明背景，无人物、文字或UI。生成表格并非精确等分，制作代码记录实际裁切区域，地表纹理取不透明内区，防止透明接缝。

验证：Unity 6000.6.4f1 已编译；新地图通过 MapJson.LoadDefinition 规则校验，使用真实 MapWorldView 渲染到 Docs/Validation/pixel-citadel.png 并检查。未声称完成手机像素对齐、SpriteAtlas 构建打包或完整战斗运行验收。


## 参考图修订（2026-10-23）
敌台改为独立 open-bastion.png：无屋顶、开放石铺站兵面、垛口与蓝旗；用于墙节点、门洞两侧和 tower 类型。素材使用内置 image_gen 新生成，提示词明确“open flat fighting platform, no roof, no blue conical roof, no characters”。旧蓝顶塔图仍保留在源素材表。站兵面是本次美术表达，未新增士兵登城或驻守系统。

参考地图重新布置：外郭、内城、主堡院墙三层；南门外凸瓮城；主堡增大；民居与教堂、左右市场和农田按参考图分区，城内散植树木。宽十字大道改为南北门道与主堡土坪。美术目录增加墙体表现宽高和主堡比例参数，原套装默认均为1，领域墙体规则未改；门的表现点击区域使用对应表现高度。平台按近侧墙脚排序以覆盖接缝。

更新入口：Game/地图工具/更新参考图城镇布局（明确更新示例源地图）。正常打开地图或切换套装不会重写文档。PixelArtSetup.ReviseReference → CreateMap → MapJson.LoadDefinition 校验 → 保存新布局；MapSceneSprites 负责共享的敌台与门边平台绘制。实际渲染：Docs/Validation/pixel-citadel-v2.png。

敌台尺寸修正：宽度从2.7降至1.55（缩小约43%），高度独立设为3.1，形成窄身敌台。制作入口 PixelArtSetup.ReviseReference 与已保存目录同步更新；MapVisualCatalog.wallJunctionHeight 为0时保持其他套装原宽高比。MapSceneSprites.AddPlatform 保持脚底、近侧排序和窄身比例；使用独立敌台图片时不重复叠加宽大的程序化节点底座。近景验收：Docs/Validation/pixel-bastion-size.png。Unity 编译通过并检查实际渲染。
