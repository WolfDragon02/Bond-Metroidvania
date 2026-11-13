shader_type canvas_item;

// 0 shows the sprite normally, 1 paints it solid flash_color.
// EnemyController sets this when the enemy gets hit.
uniform float flash : hint_range(0.0, 1.0) = 0.0;
uniform vec4 flash_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

void fragment() {
	COLOR.rgb = mix(COLOR.rgb, flash_color.rgb, flash);
}
