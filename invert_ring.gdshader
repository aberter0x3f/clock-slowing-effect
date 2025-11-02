shader_type canvas_item;
render_mode unshaded;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;

// uniform 变量可以从 C# 脚本中设置
uniform vec2 center; // 环的中心点，使用 UV 坐标 (0.0 to 1.0)
uniform float inner_radius; // 内环半径，UV 坐标系
uniform float outer_radius; // 外环半径，UV 坐标系
uniform float aspect_ratio; // 视口的宽高比 (width / height)

void fragment() {
  // 为了让圆形在非方形屏幕上看起来是圆的，我们需要校正 UV 坐标
  vec2 corrected_uv = (SCREEN_UV - center) * vec2(aspect_ratio, 1.0);
  float dist = length(corrected_uv);

  // 检查当前像素是否在内外环之间
  if (dist > inner_radius && dist < outer_radius) {
    // 获取屏幕上当前像素的原始颜色
    vec4 screen_color = texture(SCREEN_TEXTURE, SCREEN_UV);
    // 反转 RGB 颜色，保持 alpha 不变
    COLOR = vec4(1.0 - screen_color.rgb, screen_color.a);
  } else {
    // 如果不在环内，则完全透明，让后面的内容显示出来
    COLOR = vec4(0.0);
  }
}
