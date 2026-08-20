shader_type canvas_item;
render_mode blend_add;

// 四条边均匀隐没：按到最近一条边的距离渐隐（矩形渐隐，四边一致）
uniform float edge_softness : hint_range(0.0, 0.5) = 0.05;

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	float dLeft = UV.x;
	float dRight = 1.0 - UV.x;
	float dTop = UV.y;
	float dBottom = 1.0 - UV.y;
	float dEdge = min(min(dLeft, dRight), min(dTop, dBottom));
	float falloff = smoothstep(0.0, edge_softness, dEdge);
	COLOR = tex * COLOR;
	COLOR.rgb *= falloff;
}
