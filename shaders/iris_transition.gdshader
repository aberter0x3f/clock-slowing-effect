shader_type canvas_item;

// 中心点 (0.0 - 1.0 UV 坐标)
uniform vec2 center = vec2(0.5, 0.5);
// 圆的半径 (0.0 - 1.5 左右，足以覆盖屏幕)
uniform float radius : hint_range(0.0, 2.0) = 1.0;
// 屏幕长宽比，用于修正圆变为椭圆的问题
uniform float aspect_ratio = 1.0;
// 遮罩颜色
uniform vec4 color : source_color = vec4(0.0, 0.0, 0.0, 1.0);

void fragment() {
  // 将当前像素 UV 坐标相对于中心点进行偏移
  vec2 uv = UV - center;

  // 根据长宽比修正 X 轴，保证是正圆
  uv.x *= aspect_ratio;

  // 计算距离
  float dist = length(uv);

  // 如果距离小于半径，则是透明（显示游戏画面）；否则显示黑色
  // 使用 smoothstep 做一点点边缘抗锯齿，或者直接硬切
  float alpha = smoothstep(radius, radius - 0.01, dist);

  // 注意：smoothstep(edge0, edge1, x): 如果 x < edge0 返回 0
  // 我们希望 radius 内部 alpha=0 (透明)，外部 alpha=1 (黑)
  if (dist < radius) {
    COLOR = vec4(0, 0, 0, 0);
  } else {
    COLOR = color;
  }
}
