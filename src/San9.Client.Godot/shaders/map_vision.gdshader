shader_type canvas_item;
render_mode unshaded, blend_add;

uniform float facing = 0.0;
uniform float vision_angle = 3.141592654;

void fragment() {
    // Undo the map's 2:1 projection before applying the world-space direction.
    vec2 projected = (UV - vec2(0.5)) * 2.0;
    vec2 p = vec2(projected.x + projected.y, projected.y - projected.x) * 0.707106781;
    float radius = length(p);
    float radial_aa = max(fwidth(radius), 0.0001);
    float coverage = 1.0 - smoothstep(0.90, 1.0 + radial_aa, radius);
    if (vision_angle < TAU) {
        vec2 forward = vec2(sin(facing), -cos(facing));
        float edge = dot(p, forward) - radius * cos(vision_angle * 0.5);
        float angular_aa = max(fwidth(edge), 0.0001);
        coverage *= smoothstep(-angular_aa * 0.5, angular_aa * 0.5, edge);
    }

    // Blue additive light sampled radially from the source art, interpolated without its palette bands.
    vec3 palette[10] = vec3[10](
        vec3(0.0313725, 0.1568627, 0.3137255), vec3(0.0313725, 0.1567496, 0.3099548),
        vec3(0.0313725, 0.1546766, 0.3050710), vec3(0.0313725, 0.1496840, 0.3028196),
        vec3(0.0312717, 0.1444045, 0.2974592), vec3(0.0298588, 0.1372955, 0.2872193),
        vec3(0.0266439, 0.1275168, 0.2707900), vec3(0.0206243, 0.1143930, 0.2427125),
        vec3(0.0158113, 0.0985985, 0.2145743), vec3(0.0108022, 0.0614180, 0.1463235));
    float sample_position = clamp(radius * 10.0 - 0.5, 0.0, 9.0);
    int first = int(floor(sample_position));
    int second = min(first + 1, 9);
    vec3 tint = mix(palette[first], palette[second], fract(sample_position));
    if (vision_angle <= 0.0) { coverage = 0.0; }
    COLOR = vec4(tint, coverage);
}
