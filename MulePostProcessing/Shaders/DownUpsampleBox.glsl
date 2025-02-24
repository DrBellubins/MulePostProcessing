#[compute]
#version 450

// Invocations in the (x, y, z) dimension.
layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

// Our textures.
layout(r32f, set = 0, binding = 0) uniform restrict readonly image2D inputTex;
layout(r32f, set = 1, binding = 0) uniform restrict writeonly image2D outputTex;

// Our push PushConstant.
layout(push_constant, std430) uniform Params
{
	vec2 texture_size;
	float dung;
} params;

void main()
{
	ivec2 size = ivec2(params.texture_size.x - 1, params.texture_size.y - 1);

	ivec2 uv = ivec2(gl_GlobalInvocationID.xy);

	// Just in case the texture size is not divisable by 8.
	if ((uv.x > size.x) || (uv.y > size.y)) { return; }

	vec3 Input = imageLoad(inputTex, uv).rgb;
	
	vec4 result = vec4(Input.r, Input.g, Input.b, 1.0);
	
	imageStore(outputTex, uv, result);
}