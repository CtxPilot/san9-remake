shader_type canvas_item;

uniform sampler2D emblem : source_color, filter_linear_mipmap, repeat_disable;
uniform float clock_seconds = 0.0;
uniform float wind_strength : hint_range(0.0, 2.0) = 1.0;
uniform float flutter_period = 0.568;
uniform float light_response = 0.06;
uniform vec2 projection_scale = vec2(1.0);
uniform vec2 attachment = vec2(31.0, 23.5);
uniform float yaw_radians = 0.0;
uniform bool show_emblem = true;
uniform bool diagnostic_grid = false;
uniform vec4 faction_color : source_color = vec4(0.0, 0.0, 0.75, 1.0);
uniform vec4 trim_color : source_color = vec4(0.7843, 0.62745, 0.03137, 1.0);
uniform vec4 art_region = vec4(0.0, 0.0, 1.0, 1.0);
uniform vec3 lift_cos = vec3(0.0);
uniform vec3 lift_sin = vec3(0.0);
uniform vec3 lift_cos2 = vec3(0.0);
uniform vec3 stretch_cos = vec3(0.0);
uniform vec3 stretch_sin = vec3(0.0);
uniform vec3 stretch_cos2 = vec3(0.0);
uniform vec3 depth_cos = vec3(0.0);
uniform vec3 depth_sin = vec3(0.0);
uniform vec3 depth_cos2 = vec3(0.0);
uniform vec3 twist_cos = vec3(0.0);
uniform vec3 twist_sin = vec3(0.0);
uniform vec3 twist_cos2 = vec3(0.0);
uniform vec3 length_cos = vec3(0.0);
uniform vec3 length_sin = vec3(0.0);
uniform vec3 length_cos2 = vec3(0.0);
varying float cloth_light;

float displacement(vec3 polynomial, vec3 temporal, vec3 c, vec3 s, vec3 c2) {
    return dot(polynomial, c) * temporal.x + dot(polynomial, s) * temporal.y + dot(polynomial, c2) * temporal.z;
}

void vertex() {
    float u = clamp((30.0 - VERTEX.x) / 26.0, 0.0, 1.0);
    float v = clamp((VERTEX.y - 19.5) / 11.5, -1.0, 1.0);
    float phase = 6.283185307 * fract(clock_seconds / flutter_period);
    vec3 polynomial = vec3(u, u * u, u * u * u);
    vec3 temporal = vec3(cos(phase), sin(phase), cos(2.0 * phase));
    // One source-calibrated field for every view, material and letter. No time
    // constant: the selected master is the rest shape; the hoist stays pinned.
    float along = wind_strength * displacement(polynomial, temporal, length_cos, length_sin, length_cos2);
    float lift = displacement(polynomial, temporal, lift_cos, lift_sin, lift_cos2);
    float stretch = displacement(polynomial, temporal, stretch_cos, stretch_sin, stretch_cos2);
    float depth = wind_strength * (displacement(polynomial, temporal, depth_cos, depth_sin, depth_cos2)
        + v * displacement(polynomial, temporal, twist_cos, twist_sin, twist_cos2));
    float vertical = wind_strength * (lift + v * stretch);
    vec2 local = VERTEX - vec2(30.0, 20.5);
    float c = cos(yaw_radians);
    float s = sin(yaw_radians);
    vec2 rest = projection_scale * vec2(c * local.x, local.y + 0.45 * s * local.x);
    vec2 motion = vec2(projection_scale.x * c * along + s * depth,
        projection_scale.y * (vertical + 0.45 * s * along) - 0.45 * c * depth);
    VERTEX = attachment + rest + motion;
    cloth_light = 1.0 + light_response * clamp(depth / 4.0, -1.0, 1.0);
}

void fragment() {
    vec2 q = (UV * vec2(32.0, 40.0) - vec2(3.0, 8.0)) / vec2(28.0, 23.0);
    vec4 cloth = texture(TEXTURE, art_region.xy + q * art_region.zw);
    cloth.a *= step(0.0, q.x) * step(q.x, 1.0) * step(0.0, q.y) * step(q.y, 1.0);
    float gold = smoothstep(0.025, 0.18, cloth.r - cloth.b) * smoothstep(0.015, 0.10, cloth.g - cloth.b);
    float shade = dot(cloth.rgb, vec3(0.2126, 0.7152, 0.0722)) / 0.48;
    vec3 fabric = faction_color.rgb * shade;
    float border_shade = dot(cloth.rgb, vec3(0.2126, 0.7152, 0.0722)) / 0.62;
    vec3 border = trim_color.rgb * border_shade;
    vec3 result = mix(fabric, border, gold);
    vec2 decal_uv = (UV * vec2(32.0, 40.0) - vec2(8.0, 8.5)) / vec2(20.0, 24.0);
    // Both faces keep the separately authored lettering readable.
    if (cos(yaw_radians) < 0.0) {
        decal_uv.x = 1.0 - decal_uv.x;
    }
    if (show_emblem) {
        vec4 mark = texture(emblem, decal_uv);
        float inside = step(0.0, decal_uv.x) * step(decal_uv.x, 1.0) * step(0.0, decal_uv.y) * step(decal_uv.y, 1.0);
        result = mix(result, mark.rgb, mark.a * inside);
    }
    if (diagnostic_grid) {
        vec2 line = abs(fract(UV * vec2(16.0, 20.0) + 0.5) - 0.5) / max(fwidth(UV * vec2(16.0, 20.0)), vec2(0.001));
        result = mix(vec3(0.2, 0.85, 0.80), result, clamp(min(line.x, line.y), 0.0, 1.0));
    }
    COLOR = vec4(result * cloth_light, cloth.a);
}
