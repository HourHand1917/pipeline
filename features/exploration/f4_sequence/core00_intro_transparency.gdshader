shader_type canvas_item;
render_mode unshaded;

// The authored movie uses a flat light-grey backing instead of an alpha
// channel.  Keep this configurable so a later export can be tuned in the
// Inspector without re-encoding the movie.
uniform vec3 key_color : source_color = vec3(0.839216, 0.839216, 0.839216);
uniform float key_similarity : hint_range(0.0, 0.5, 0.001) = 0.008;
uniform float key_softness : hint_range(0.001, 0.25, 0.001) = 0.018;

// Optional crop retained as an Inspector tuning hook.  The subtitle-free OGV
// needs the complete frame because both hands travel through its upper half.
uniform float keep_from_y : hint_range(0.0, 1.0, 0.005) = 0.0;
uniform float edge_softness : hint_range(0.001, 0.1, 0.002) = 0.012;

void fragment() {
	vec4 video = texture(TEXTURE, UV);
	float colour_delta = distance(video.rgb, key_color);
	float chroma_alpha = smoothstep(
		key_similarity,
		key_similarity + key_softness,
		colour_delta
	);
	float region_alpha = keep_from_y <= 0.0
		? 1.0
		: smoothstep(keep_from_y, keep_from_y + edge_softness, UV.y);
	COLOR = vec4(video.rgb, video.a * chroma_alpha * region_alpha);
}
