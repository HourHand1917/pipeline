shader_type canvas_item;

// 猫和老鼠式圆形转场（iris wipe）。
// radius 越大越透明（露出画面），越小越黑（遮住画面）。
uniform float radius : hint_range(-0.5, 1.5) = 1.5;

void fragment() {
	vec2 center = vec2(0.5, 0.5);
	float dist = distance(UV, center);
	// dist < radius（圆孔内）透明可见；dist >= radius（圆孔外）黑色遮住
	float a = smoothstep(radius, radius + 0.03, dist);
	COLOR = vec4(0.0, 0.0, 0.0, a);
}
