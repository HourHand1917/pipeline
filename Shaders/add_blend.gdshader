shader_type canvas_item;
render_mode blend_add;

uniform float intensity : hint_range(0.0, 2.0) = 1.0;

void fragment() {
	COLOR = texture(TEXTURE, UV) * COLOR;
	COLOR.rgb *= intensity;
}
