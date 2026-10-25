# 《英雄之后》四格宣传海报：画风与生成稿

状态：2026-10-25，内置 image_gen 两次因网络错误失败，未生成成品图像。

## 画风

温暖的手绘奇幻绘本，水粉色块与清楚的深青色线条，约4.5至5头身。面向10岁以上、不同年龄和性别的国际观众：人物自然、服装实用，配色采用森林青绿、木材赭黄、陶土红和柔和蓝色。表达轻微战斗，不出现血液、尸体、受伤特写或恐怖亡灵。

## 版式与分镜

竖版约2:3，上方标题，下方2×2四格，按左上、右上、左下、右下阅读。

标题：英雄之后
英文副标题：AFTER THE HEROES

1. **英雄拯救了世界。** 远处英雄队伍凯旋离开；近处普通士兵洛恩仍在营地执勤。
2. **他们离开以后，生活还要继续。** 洛恩与医师照顾恢复中的伤员，木匠检查破损入口，居民搬运物资。
3. **留下的人，开始重建家园。** 同一营地自由布置木栅、箭楼、医帐与公共建筑；士兵训练、居民参与建设。
4. **这一次，我们一起守住它。** 黄昏，士兵、木匠、医护、弓手协同保护聚落，敌人在外围远景；屋内透出温暖灯光。

页脚：建设家园 · 集结伙伴 · 并肩守护

## 完整生成提示词

执行方式：内置 image_gen；新图生成；不透明背景；无参考图片。

```text
Use case: illustration-story / ads-marketing.
Create one finished, polished promotional FOUR-PANEL COMIC POSTER for an original tower-defense + RTS + free-building fantasy game titled "英雄之后" with small English subtitle "AFTER THE HEROES".
Format: high-resolution portrait poster, roughly 2:3 aspect, a tasteful title header and a very clear 2x2 comic grid beneath, read top-left, top-right, bottom-left, bottom-right. Four large art panels, equal visual weight, clean cream gutters, refined warm cream outer border. Each panel has a restrained caption strip with accurately rendered Simplified Chinese. The illustration should dominate, not typography.

STYLE IS THE MOST IMPORTANT REQUIREMENT: original premium hand-painted storybook adventure illustration, warm gouache color shapes, fine confident dark-teal linework, subtle paper texture, clear silhouettes and appealing expressive faces. Sophisticated enough for adults, approachable for children age 10+, all genders and an international audience. Slightly stylized human proportions approximately 4.5 to 5 heads tall, believable gestures, varied silhouettes; not toddler chibi, not gritty realism, not manga, not a clone of any existing game or artist. Attractive readable visual design achievable as a coherent game art direction: simple buildings, economical shapes, limited materials. Palette: forest teal and sage, warm ochre timber, terracotta, soft cornflower blue, golden sunrise accents. Quietly moving, hopeful, dignified, gentle humor in small teamwork details. Adults of varied ages and skin tones, women and men with equal agency, practical nonsexualized clothing, natural faces.

WORLD AND PROTAGONIST: Famous heroes genuinely saved the world, defeated the tyrant, slew the dragon and sealed evil. The ordinary people left behind must survive and rebuild. Protagonist is Lorne, an ordinary young adult foot soldier with short dark hair, humble iron cap, faded cornflower-blue scarf, simple patched brown-grey tunic and modest shoulder armor, wooden round shield. No crown, no ornate glowing sword, no chosen-one aura. Consistent appearance across all four panels. Supporting characters recur consistently: sturdy auburn-haired woman carpenter Ruda in rolled sleeves and leather apron, older woman field medic Martha with a white-and-sage satchel, ordinary archers and shield soldiers, residents young and old. Do not portray the famous heroes as villains.

PANEL 1 (top-left), caption exactly: "英雄拯救了世界。"
A wide afternoon scene: celebrated small band of splendid but friendly champions departing far along a road toward a sunlit distant castle, colorful banners and a modest celebratory crowd in background. In foreground at the roadside stands the ordinary blue-scarfed soldier, still on duty beside a small supply cart and a folded hospital tent. Composition contrasts distant celebration with his grounded presence. Show relief, fatigue and uncertainty, no blood or bodies, no huge dragon corpse, no ruin spectacle.

PANEL 2 (top-right), caption exactly: "他们离开以后，生活还要继续。"
Closer human scene at a vulnerable temporary encampment near an old stone quarry: same soldier kneels to help the medic and a seated recovering adult with a small clean bandage on one forearm; woman carpenter inspects a broken gate, a parent carries a blanket, a child holds a bowl. People are capable and caring, not helpless misery. The soldier notices the open gap toward the forest. Scarce supplies and a damaged fence imply what must be done. No injury detail, no sickly horror.

PANEL 3 (bottom-left), caption exactly: "留下的人，开始重建家园。"
Lively elevated three-quarter RTS-like view of the same site becoming a settlement. Same soldier directs a small squad; carpenter and residents raise timber palisades and a simple arrow tower, medic organizes a canvas medical tent, two archers train, people bring boards and grain sacks. Show a central common hall / command tent, coherent walkable ground, tower, enclosure with visible gate and open spaces where buildings are being freely laid out. Warm activity and practical teamwork, tasteful design, enough empty ground to read layout. No floating UI, no upgrade icons or technical diagrams.

PANEL 4 (bottom-right), caption exactly: "这一次，我们一起守住它。"
Hopeful defensive battle at dusk, village windows glowing warmly. Same ordinary soldier and varied companions stand together at the timber gate, shield-bearers form a line, carpenter helps brace it, archers defend from a wooden tower, medic helps just behind. Small distant stylized enemy raiders and simple fantasy creatures approach outside the perimeter; keep opposition readable but secondary. No on-contact weapon impacts, no injury, no blood, no death, no frightening undead. Inside the palisade, residents pass supplies and a parent guides a child into a warm house. Emphasis on coordination, protection and the settlement they built, not an individual superhero. Blue evening light and golden windows, calm courage amid mild action.

TITLE HEADER: "英雄之后" in large beautiful legible warm-dark-teal illustrated lettering, restrained decorative wood-and-leaf motif, with "AFTER THE HEROES" small underneath.
FOOTER, exact text: "建设家园 · 集结伙伴 · 并肩守护"
Do not add other text, logos, fake age-rating badges or watermark. Keep all captions legible and spacious, never cover key faces. No photorealism, no excessive detail noise, no sexualization, no gore, no bodies, no frightening imagery. Deliver a unified finished comic poster, not a mood board.
```

