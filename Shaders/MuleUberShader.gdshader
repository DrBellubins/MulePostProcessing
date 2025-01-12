shader_type canvas_item;

uniform sampler3D lut;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;
uniform sampler2D blur_texture : hint_screen_texture, filter_linear_mipmap;

// sRGB => XYZ => D65_2_D60 => AP1 => RRT_SAT
const mat3 ACESInputMat = mat3(vec3(0.59719, 0.35458, 0.04823), vec3(0.07600, 0.90834, 0.01566), vec3(0.02840, 0.13383, 0.83777));
const mat3 ACESOutputMat = mat3(vec3(1.60475, -0.53108, -0.07367), vec3(-0.10208,  1.10813, -0.00605), vec3(-0.00327, -0.07276,  1.07602));


vec3 RRTAndODTFit(vec3 v)
{
    vec3 a = v * (v + 0.0245786f) - 0.000090537f;
    vec3 b = v * (0.983729f * v + 0.4329510f) + 0.238081f;
    return a / b;
}

vec3 ACESFitted(vec3 color)
{
    color = color * ACESInputMat;

    // Apply RRT and ODT
    color = RRTAndODTFit(color);

    color = color * ACESOutputMat;

    // Clamp to [0, 1]
    color = clamp(color, 0.0, 1.0);

    return color;
}

// Cabbibo's HSV
vec3 hsv(float h, float s, float v)
{
  return mix( vec3( 1.0 ), clamp( ( abs( fract(
    h + vec3( 3.0, 2.0, 1.0 ) / 3.0 ) * 6.0 - 3.0 ) - 1.0 ), 0.0, 1.0 ), s ) * v;
}

vec3 apply_color_correction(vec3 color)
{
	return textureLod(lut, color, 0.0).rgb;
}

float random(vec2 coords)
{
   return fract(sin(dot(coords.xy, vec2(12.9898,78.233))) * 43758.5453);
}

void fragment()
{
	vec2 uv = SCREEN_UV;
	uv *=  1.0 - uv.yx;

	vec4 color = texture(screen_texture, SCREEN_UV);
	vec4 lensFlare = texture(blur_texture, 1.0 - SCREEN_UV, 4.0);

	lensFlare.rgb = pow(lensFlare.rgb, vec3(4.0)) / 4.0;

	float vig = uv.x * uv.y * 15.0;
	vig = pow(vig, 4.0);

	lensFlare *= vig;
	color.rgb += lensFlare.rgb;

	color.rgb = clamp(ACESFitted(color.rgb * 1.4) * 1.4, 0.0, 1.0);
	color.rgb = apply_color_correction(color.rgb);

	COLOR = color;
}
