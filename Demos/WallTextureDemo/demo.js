"use strict";

// 教学坐标写死。x 沿墙，y 横跨墙宽，z 是离地高度；这些值不是游戏产品配置。
const SIZE = {
  length: 7, width: 1.8, height: 1.25,
  railWidth: 0.22, railHeight: 0.18,
  merlonLength: 0.55, merlonSpacing: 1, merlonHeight: 0.45
};
const canvas = document.getElementById("scene");
const context = canvas.getContext("2d");
const state = { step: 4, mode: "texture", wire: false, textureScale: 1, selected: "body-front" };
const textures = {};
let visibleFaces = [];

const explanations = [
  "0 / 地面轮廓：四个坐标决定长与宽。此时没有墙面，也没有贴图。",
  "1 / 墙身：把地面边缘连接到墙顶高度，生成竖直表面。本例只画朝向观察者的正面和右侧面。",
  "2 / 墙顶路面：在相同高度放一块水平面，使用另一张铺地图片。",
  "3 / 女墙：沿平台两边各生成一条矮墙。总宽 1.80 − 2 × 女墙厚度 0.22 ＝ 路面净宽 1.36。",
  "4 / 垛口：每隔 1.00 放一个长 0.55 的凸起，中间留空 0.45。它们与女墙使用同样两张图片。"
];

// 简单斜俯视投影：同一地面位置的点，提高 z 就在画面上向上移动。
function project(point) {
  return { x: 120 + point.x * 100 + point.y * 35, y: 420 - point.y * 42 - point.z * 110 };
}

// 一个表面由四个空间顶点组成。UV 不改变形状，只决定图片怎样铺在表面上。
function makeFace(id, label, points, material, plane) {
  const uv = points.map(point => {
    const scale = state.textureScale;
    if (plane === "top") return { u: point.x / (2 * scale), v: point.y / (2 * scale) };
    const along = plane === "front" ? point.x : point.y;
    return { u: along / (2 * scale), v: -point.z / (1.5 * scale) };
  });
  return {
    id, label, points, uv, material, plane,
    screen: points.map(project),
    // 简化的绘画顺序：远处先画，同一深度低处先画；只用于这个固定示例。
    depth: points.reduce((sum, point) => sum + point.y - point.z * 0.01, 0) / 4
  };
}

// 用三个可见面表达一个长方体：正面、右侧、顶面。没有用带透明洞的垛口图片。
function addBox(faces, id, name, x0, x1, y0, y1, z0, z1, includeTop = true) {
  const p = (x, y, z) => ({ x, y, z });
  faces.push(makeFace(id + "-front", name + " · 正面", [
    p(x0, y0, z0), p(x1, y0, z0), p(x1, y0, z1), p(x0, y0, z1)
  ], "masonry", "front"));
  faces.push(makeFace(id + "-side", name + " · 右侧面", [
    p(x1, y0, z0), p(x1, y1, z0), p(x1, y1, z1), p(x1, y0, z1)
  ], "masonry", "side"));
  if (includeTop) {
    faces.push(makeFace(id + "-top", name + " · 顶面", [
      p(x0, y0, z1), p(x1, y0, z1), p(x1, y1, z1), p(x0, y1, z1)
    ], "walkway", "top"));
  }
}

// 从最基本的墙身，逐步增加平台、连续女墙、间断垛口。
function buildFaces() {
  const faces = [];
  const s = SIZE;
  if (state.step >= 1) addBox(faces, "body", "墙身", 0, s.length, 0, s.width, 0, s.height, false);
  if (state.step >= 2) {
    faces.push(makeFace("deck", "墙顶路面", [
      { x: 0, y: 0, z: s.height }, { x: s.length, y: 0, z: s.height },
      { x: s.length, y: s.width, z: s.height }, { x: 0, y: s.width, z: s.height }
    ], "walkway", "top"));
  }
  if (state.step >= 3) {
    for (const y of [0, s.width - s.railWidth]) {
      const side = y === 0 ? "near" : "far";
      addBox(faces, "rail-" + side, "女墙", 0, s.length, y, y + s.railWidth, s.height, s.height + s.railHeight);
      if (state.step >= 4) {
        for (let x = 0; x < s.length; x += s.merlonSpacing) {
          // 只在这一小段生成凸起；余下的间隔完全没有高垛口的几何。
          addBox(faces, "merlon-" + side + "-" + x, "垛口", x, Math.min(x + s.merlonLength, s.length),
            y, y + s.railWidth, s.height + s.railHeight, s.height + s.railHeight + s.merlonHeight);
        }
      }
    }
  }
  return faces.sort((left, right) => right.depth - left.depth);
}

function path(points) {
  context.beginPath();
  context.moveTo(points[0].x, points[0].y);
  for (const point of points.slice(1)) context.lineTo(point.x, point.y);
  context.closePath();
}

// 一个四边形拆为 (0,1,2)、(0,2,3) 两个三角形。
// 这里用 Canvas 的仿射变换演示 GPU 通常承担的纹理映射：
// 先从三组 UV/画面坐标求出变换，再把重复图片限制在三角形内部。
function drawTexturedTriangle(screen, uv, texture) {
  const source = uv.map(point => ({ x: point.u * texture.image.width, y: point.v * texture.image.height }));
  const [a, b, c] = source;
  const [p, q, r] = screen;
  const ux = b.x - a.x, uy = b.y - a.y;
  const vx = c.x - a.x, vy = c.y - a.y;
  const determinant = ux * vy - vx * uy;
  if (Math.abs(determinant) < 0.000001) return;
  const m11 = ((q.x - p.x) * vy - (r.x - p.x) * uy) / determinant;
  const m12 = ((q.y - p.y) * vy - (r.y - p.y) * uy) / determinant;
  const m21 = ((r.x - p.x) * ux - (q.x - p.x) * vx) / determinant;
  const m22 = ((r.y - p.y) * ux - (q.y - p.y) * vx) / determinant;
  const tx = p.x - m11 * a.x - m21 * a.y;
  const ty = p.y - m12 * a.x - m22 * a.y;
  context.save();
  path(screen);
  context.clip();
  context.transform(m11, m12, m21, m22, tx, ty);
  context.fillStyle = texture.pattern;
  const xs = source.map(point => point.x), ys = source.map(point => point.y);
  context.fillRect(Math.min(...xs) - 1, Math.min(...ys) - 1,
    Math.max(...xs) - Math.min(...xs) + 2, Math.max(...ys) - Math.min(...ys) + 2);
  context.restore();
}

function drawFace(face) {
  path(face.screen);
  context.fillStyle = face.material === "walkway" ? "#c1c9a0" : face.plane === "side" ? "#7c888c" : "#a6b3b9";
  context.fill();
  if (state.mode === "texture") {
    for (const indices of [[0, 1, 2], [0, 2, 3]]) {
      drawTexturedTriangle(indices.map(index => face.screen[index]), indices.map(index => face.uv[index]), textures[face.material]);
    }
    // 固定明暗只帮助区分朝向，不改变图片或几何。
    path(face.screen);
    context.fillStyle = face.plane === "side" ? "rgba(15,25,30,.28)" : face.plane === "front" ? "rgba(15,25,30,.10)" : "rgba(255,244,203,.06)";
    context.fill();
  }
  if (state.wire) {
    path(face.screen);
    context.strokeStyle = "#72eddf";
    context.lineWidth = 1.3;
    context.stroke();
    context.beginPath();
    context.moveTo(face.screen[0].x, face.screen[0].y);
    context.lineTo(face.screen[2].x, face.screen[2].y);
    context.stroke();
  }
}

function showFace(face) {
  if (!face) {
    document.getElementById("faceInfo").textContent = "当前只有地面轮廓，还没有可贴纹理的表面。选择步骤 1～4。";
    return;
  }
  const lines = [face.label + " → " + (face.material === "masonry" ? "wall-masonry.png" : "wall-walkway.png")];
  face.points.forEach((point, index) => {
    const uv = face.uv[index];
    lines.push(`P${index}: 空间 (${point.x.toFixed(2)}, ${point.y.toFixed(2)}, ${point.z.toFixed(2)})  →  UV (${uv.u.toFixed(2)}, ${uv.v.toFixed(2)})`);
  });
  lines.push("三角形索引：[0, 1, 2] 和 [0, 2, 3]", "顶点决定表面在哪里；UV 决定图片的哪一部分贴在那里。");
  document.getElementById("faceInfo").textContent = lines.join("\n");
}

function render() {
  context.clearRect(0, 0, canvas.width, canvas.height);
  // 固定的地面参考线，展示未加高度之前的坐标。
  context.strokeStyle = "#30444c";
  context.lineWidth = 1;
  for (let x = 0; x <= 8; x++) {
    const a = project({ x, y: -0.6, z: 0 }), b = project({ x, y: 3, z: 0 });
    context.beginPath(); context.moveTo(a.x, a.y); context.lineTo(b.x, b.y); context.stroke();
  }
  visibleFaces = buildFaces();
  for (const face of visibleFaces) drawFace(face);
  if (state.step === 0) {
    const footprint = [{ x: 0, y: 0, z: 0 }, { x: 7, y: 0, z: 0 }, { x: 7, y: 1.8, z: 0 }, { x: 0, y: 1.8, z: 0 }];
    path(footprint.map(project)); context.strokeStyle = "#72eddf"; context.lineWidth = 3; context.stroke();
    context.font = "17px Microsoft YaHei, sans-serif"; context.fillStyle = "#d8fff7";
    footprint.forEach((point, index) => {
      const at = project(point);
      context.fillText(`P${index} (${point.x}, ${point.y}, 0)`, at.x - 20, at.y + (index < 2 ? 28 : -13));
    });
  }
  const selected = visibleFaces.find(face => face.id === state.selected) || visibleFaces[0];
  showFace(selected);
  document.getElementById("description").textContent = explanations[state.step];
  document.getElementById("stats").textContent = `${visibleFaces.length} 个四边形表面 · ${visibleFaces.length * 2} 个三角形。点击表面查看 UV；切换纹理尺寸时，三角形数量不变。`;
  document.getElementById("scaleValue").textContent = state.textureScale.toFixed(1) + " 倍";
  document.querySelectorAll("[data-step]").forEach(button => button.setAttribute("aria-pressed", String(Number(button.dataset.step) === state.step)));
}

document.querySelectorAll("[data-step]").forEach(button => button.addEventListener("click", () => {
  state.step = Number(button.dataset.step); render();
}));
document.getElementById("mode").addEventListener("change", event => { state.mode = event.target.value; render(); });
document.getElementById("wire").addEventListener("change", event => { state.wire = event.target.checked; render(); });
document.getElementById("textureScale").addEventListener("input", event => { state.textureScale = Number(event.target.value); render(); });
document.getElementById("reset").addEventListener("click", () => {
  Object.assign(state, { step: 4, mode: "texture", wire: false, textureScale: 1, selected: "body-front" });
  document.getElementById("mode").value = "texture";
  document.getElementById("wire").checked = false;
  document.getElementById("textureScale").value = "1";
  render();
});
canvas.addEventListener("click", event => {
  const rect = canvas.getBoundingClientRect();
  const x = (event.clientX - rect.left) * canvas.width / rect.width;
  const y = (event.clientY - rect.top) * canvas.height / rect.height;
  for (const face of [...visibleFaces].reverse()) {
    path(face.screen);
    if (context.isPointInPath(x, y)) { state.selected = face.id; showFace(face); break; }
  }
});

// 直接加载同目录 PNG，file:// 双击即可运行；不使用 fetch、CDN 或服务器。
async function loadTexture(name, url) {
  const image = new Image();
  image.src = url;
  await image.decode();
  textures[name] = { image, pattern: context.createPattern(image, "repeat") };
}
Promise.all([
  loadTexture("masonry", "textures/wall-masonry.png"),
  loadTexture("walkway", "textures/wall-walkway.png")
]).then(render).catch(error => {
  const message = document.getElementById("error");
  message.hidden = false;
  message.textContent = "素材加载失败。请保留 demo.js 和 textures 文件夹，再打开 index.html。";
  console.error(error);
});
