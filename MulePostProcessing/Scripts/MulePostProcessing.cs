using Godot;
using Godot.NativeInterop;
using System;
using System.Collections.Generic;

public partial class MulePostProcessing : Node
{
    [Export] public TextureRect rect;
	//[Export] public Material UberMaterial;
	[Export] public RDShaderFile ComputeShader;

	private RenderingDevice rd;
	private RDShaderSpirV shaderBytecode;

	private Rid shader;
	private Rid pipeline;
	private Rid outputTex;

	private Vector2 resolution;

	public override void _Ready()
	{
        rd = RenderingServer.CreateLocalRenderingDevice(); // Doesn't show up in RenderDoc!!
		//rd = RenderingServer.GetRenderingDevice();

		shaderBytecode = ComputeShader.GetSpirV();
		shader = rd.ShaderCreateFromSpirV(shaderBytecode);
		pipeline = rd.ComputePipelineCreate(shader);
	}

	public override void _Process(double delta)
	{
        resolution = GetViewport().GetVisibleRect().Size;

		RenderingServer.CallOnRenderThread(new Callable(this, MethodName.render));
    }

	private void render()
	{
        var pushConst = new List<byte>();
        pushConst.AddRange(BitConverter.GetBytes(resolution.X));
        pushConst.AddRange(BitConverter.GetBytes(resolution.Y));
        pushConst.AddRange(BitConverter.GetBytes(0f)); // unused
        pushConst.AddRange(BitConverter.GetBytes(0f)); // unused

        var xGroup = (uint)((resolution.X - 1) / 8 + 1);
        var yGroup = (uint)((resolution.Y - 1) / 8 + 1);

        var computeList = rd.ComputeListBegin();
        rd.ComputeListBindComputePipeline(computeList, pipeline);
        rd.ComputeListBindUniformSet(computeList, GetViewport().GetViewportRid(), 0);   // Viewport RID
        rd.ComputeListBindUniformSet(computeList, outputTex, 1);                        // Output RID
        rd.ComputeListSetPushConstant(computeList, pushConst.ToArray(), (uint)(pushConst.Count));
        rd.ComputeListDispatch(computeList, xGroup, yGroup, 1);
        rd.ComputeListEnd();
        rd.Submit();

        rd.Sync(); // Wait for compute shader to finish

        rect.Texture = GetTexture2DFromRid(outputTex);
    }

    private Texture2D GetTexture2DFromRid(Rid rid)
    {
        // Sync GPU operations
        rd.Sync();

        // Get texture data
        byte[] textureData = rd.TextureGetData(rid, 0);

        // Create an Image from the data
        Image image = Image.CreateFromData((int)resolution.X, (int)resolution.Y, false, Image.Format.Rgbaf, textureData);

        // Create a Texture2D from the Image
        ImageTexture texture = ImageTexture.CreateFromImage(image);

        return texture;
    }
}
