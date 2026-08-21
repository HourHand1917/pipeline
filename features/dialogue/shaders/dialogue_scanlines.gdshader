shader_type canvas_item;
render_mode unshaded;

uniform vec4 dark_band : source_color = vec4(0.01, 0.10, 0.15, 0.13);
uniform vec4 cyan_band : source_color = vec4(0.04, 0.32, 0.43, 0.16);
uniform float broad_band_count : hint_range(4.0, 24.0, 1.0) = 11.0;
uniform float fine_line_count : hint_range(24.0, 160.0, 1.0) = 82.0;
uniform float grain_amount : hint_range(0.0, 0.12, 0.005) = 0.035;


float hash_21(vec2 value) {
	return fract(sin(dot(value, vec2(12.9898, 78.233))) * 43758.5453);
}


void fragment() {
	float broad = step(0.52, fract(UV.y * broad_band_count));
	float fine = smoothstep(0.82, 1.0, fract(UV.y * fine_line_count));
	float grain = hash_21(floor(UV * vec2(112.0, 56.0))) - 0.5;
	vec3 tint = mix(dark_band.rgb, cyan_band.rgb, broad);
	tint += vec3(0.03, 0.28, 0.34) * grain * grain_amount;
	float alpha = mix(dark_band.a, cyan_band.a, broad) + fine * 0.045;
	COLOR = vec4(tint, clamp(alpha, 0.0, 0.30));
}
