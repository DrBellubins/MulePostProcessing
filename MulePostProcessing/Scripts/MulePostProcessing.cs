using Godot;
using Godot.NativeInterop;
using System;
using System.Collections.Generic;

public partial class MulePostProcessing : Node
{
	[Export] public Material UberMaterial;
	[Export] public RDShaderFile ComputeShader;

	private RenderingDevice rd;
	private RDShaderSpirV shaderBytecode;

	private Rid shader;
	private Rid pipeline;
	private Rid outputTex;

	private Vector2 resolution;

	public override void _Ready()
	{
		rd = RenderingServer.GetRenderingDevice();

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

        var xGroup = (uint)((resolution.X - 1) / 8 + 1);
        var yGroup = (uint)((resolution.Y - 1) / 8 + 1);

        var computeList = rd.ComputeListBegin();
        rd.ComputeListBindComputePipeline(computeList, pipeline);
        rd.ComputeListBindUniformSet(computeList, GetViewport().GetViewportRid(), 0);
        rd.ComputeListSetPushConstant(computeList, pushConst.ToArray(), (uint)(pushConst.Count));
        rd.ComputeListDispatch(computeList, xGroup, yGroup, 1);
        rd.ComputeListEnd();
    }
}
