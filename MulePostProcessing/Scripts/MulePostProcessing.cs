using Godot;
using Godot.NativeInterop;
using System;
using System.Collections.Generic;

public partial class MulePostProcessing : Node
{
    [Export] public TextureRect rect;
    [Export] public RDShaderFile ComputeShader;

    private RenderingDevice rd;
    private RDShaderSpirV shaderBytecode;

    private Rid shader;
    private Rid pipeline;
    private Rid outputTex;        // Output texture RID
    private Rid inputUniformSet;  // Uniform set for inputTex (viewport)
    private Rid outputUniformSet; // Uniform set for outputTex

    private Vector2 resolution;

    public override void _Ready()
    {
        //rd = RenderingServer.CreateLocalRenderingDevice();
        rd = RenderingServer.GetRenderingDevice();

        shaderBytecode = ComputeShader.GetSpirV();
        shader = rd.ShaderCreateFromSpirV(shaderBytecode);
        pipeline = rd.ComputePipelineCreate(shader);

        resolution = GetViewport().GetVisibleRect().Size;
        InitializeTextures();
    }

    private void InitializeTextures()
    {
        // Output texture (still created manually)
        var texFormat = new RDTextureFormat
        {
            Width = (uint)resolution.X,
            Height = (uint)resolution.Y,
            Format = RenderingDevice.DataFormat.R8G8B8A8Unorm, // Match rgba8
            UsageBits = RenderingDevice.TextureUsageBits.StorageBit | RenderingDevice.TextureUsageBits.CanUpdateBit
        };
        outputTex = rd.TextureCreate(texFormat, new RDTextureView());

        // Create uniform set for outputTex
        var outputUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.Image,
            Binding = 0,
            //Ids = new Godot.Collections.Array<Rid> { outputTex }
        };

        outputUniform.AddId(outputTex);

        outputUniformSet = rd.UniformSetCreate(new Godot.Collections.Array<RDUniform> { outputUniform }, shader, 1);
    }

    public override void _Process(double delta)
    {
        resolution = GetViewport().GetVisibleRect().Size;
        RenderingServer.CallOnRenderThread(new Callable(this, MethodName.Render));
    }

    private void Render()
    {
        // Get the viewport texture RID every frame (it may change)
        Rid viewportTexture = GetViewport().GetTexture().GetRid();
        GD.Print("Input Uniform Set RID valid: ", !viewportTexture.IsValid);

        // Create uniform set for inputTex (viewport texture)
        var inputUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.Image,
            Binding = 0,
            //Ids = new Godot.Collections.Array<Rid> { viewportTexture }
        };

        inputUniform.AddId(viewportTexture);

        inputUniformSet = rd.UniformSetCreate(new Godot.Collections.Array<RDUniform> { inputUniform }, shader, 0);
        GD.Print("Input Uniform Set RID valid: ", !inputUniformSet.IsValid);

        var pushConst = new List<byte>();
        pushConst.AddRange(BitConverter.GetBytes(resolution.X));
        pushConst.AddRange(BitConverter.GetBytes(resolution.Y));
        pushConst.AddRange(BitConverter.GetBytes(0f)); // dung parameter

        var xGroup = (uint)((resolution.X - 1) / 8 + 1);
        var yGroup = (uint)((resolution.Y - 1) / 8 + 1);

        var computeList = rd.ComputeListBegin();
        rd.ComputeListBindComputePipeline(computeList, pipeline);
        rd.ComputeListBindUniformSet(computeList, inputUniformSet, 0);  // Bind viewport texture
        rd.ComputeListBindUniformSet(computeList, outputUniformSet, 1); // Bind output texture
        rd.ComputeListSetPushConstant(computeList, pushConst.ToArray(), (uint)pushConst.Count);
        rd.ComputeListDispatch(computeList, xGroup, yGroup, 1);
        rd.ComputeListEnd();
        rd.Submit();

        rd.Sync(); // Wait for compute shader to finish

        rect.Texture = GetTexture2DFromRid(outputTex);
    }

    private Texture2D GetTexture2DFromRid(Rid rid)
    {
        rd.Sync();
        byte[] textureData = rd.TextureGetData(rid, 0);
        Image image = Image.CreateFromData((int)resolution.X, (int)resolution.Y, false, Image.Format.Rgba8, textureData); // Match rgba8
        ImageTexture texture = ImageTexture.CreateFromImage(image);
        return texture;
    }

    public override void _ExitTree()
    {
        rd.FreeRid(outputTex);
        rd.FreeRid(inputUniformSet);
        rd.FreeRid(outputUniformSet);
        rd.FreeRid(shader);
        rd.FreeRid(pipeline);
        rd.Free();
    }
}