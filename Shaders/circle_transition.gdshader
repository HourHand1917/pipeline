shader_type canvas_item;

// 猫和老鼠式圆形转场（iris wipe）。
// radius 越大越透明（露出画面），越小越黑（遮住画面）。
uniform float radius : hint_range(-0.5, 1.5) = 1.5;
// 屏幕宽高比（宽/高），用于把 UV 距离换算成正圆；4:3 屏幕填 1.333。
// 不修正的话，4:3 下圆形会横向拉伸成椭圆。
uniform float aspect : hint_range(1.0, 2.5) = 1.333;

void fragment() {
	vec2 center = vec2(0.5, 0.5);
	vec2 p = UV - center;
	p.x *= aspect;  // 拉伸 x 抵消宽高比，保证是正圆
	float dist = length(p);
	// dist < radius（圆孔内）透明可见；dist >= radius（圆孔外）黑色遮住
	float a = smoothstep(radius, radius + 0.03, dist);
	COLOR = vec4(0.0, 0.0, 0.0, a);
}
