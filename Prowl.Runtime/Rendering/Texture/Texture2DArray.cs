// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;

using Veldrid;
using Prowl.Echo;

namespace Prowl.Runtime.Rendering;

/// <summary>
/// A <see cref="Texture"/> comprised of an array of images with two dimensions and support for multisampling.
/// </summary>
public sealed class Texture2DArray : Texture
{
    /// <summary>The width of this <see cref="Texture2DArray"/>.</summary>
    public uint Width => InternalTexture.Width;

    /// <summary>The height of this <see cref="Texture2DArray"/>.</summary>
    public uint Height => InternalTexture.Height;

    /// <summary>The quantity or length of this <see cref="Texture2DArray"/>.</summary>
    public uint Layers => InternalTexture.ArrayLayers;

    internal Texture2DArray() : base() { }

    /// <summary>
    /// Creates a <see cref="Texture2DArray"/> with the desired parameters but no image data.
    /// </summary>
    /// <param name="width">The width of the <see cref="Texture2DArray"/>.</param>
    /// <param name="height">The height of the <see cref="Texture2DArray"/>.</param>
    /// <param name="layers">The height of the <see cref="Texture2DArray"/>.</param>
    /// <param name="mipLevels">How many mip levels this <see cref="Texture2DArray"/> has.</param>
    /// <param name="format">The pixel format for this <see cref="Texture2DArray"/>.</param>
    /// <param name="usage">The usage modes of this <see cref="Texture2DArray"/>.</param>
    /// <param name="sampleCount">The multisampled count of the texture. Values above Count1 will require the texture to be resolved with <see cref="CommandBuffer.ResolveMultisampledTexture(RenderTexture, RenderTexture)"/> </param>
    public Texture2DArray(
        uint width, uint height,
        uint layers, uint mipLevels = 1,
        PixelFormat format = PixelFormat.R8_G8_B8_A8_UNorm,
        TextureUsage usage = TextureUsage.Sampled,
        TextureSampleCount sampleCount = TextureSampleCount.Count1
    ) : base(new()
    {
        Width = width,
        Height = height,
        MipLevels = mipLevels,
        ArrayLayers = layers,
        Format = format,
        Usage = usage,
        Type = TextureType.Texture2D,
        SampleCount = sampleCount,
    })
    { }

    /// <summary>
    /// Sets the data of an area of the <see cref="Texture2DArray"/>.
    /// </summary>
    /// <param name="ptr">The pointer from which the pixel data will be read.</param>
    /// <param name="rectX">The X coordinate of the first pixel to write.</param>
    /// <param name="rectY">The Y coordinate of the first pixel to write.</param>
    /// <param name="rectWidth">The width of the rectangle of pixels to write.</param>
    /// <param name="rectHeight">The height of the rectangle of pixels to write.</param>
    /// <param name="layerIndex">The layer index of the array to write to.</param>
    /// <param name="mipLevel">The mip level to write to.</param>
    public unsafe void SetDataPtr(void* ptr, uint rectX, uint rectY, uint rectWidth, uint rectHeight, uint layerIndex, uint mipLevel = 0) =>
        InternalSetDataPtr(ptr, new Vector3Int((int)rectX, (int)rectY, 0), new Vector3Int((int)rectWidth, (int)rectHeight, 0), layerIndex, mipLevel);

    /// <summary>
    /// Sets the data of an area of the <see cref="Texture2DArray"/>.
    /// </summary>
    /// <typeparam name="T">A struct with the same format as this <see cref="Texture2DArray"/>'s pixels.</typeparam>
    /// <param name="data">A <see cref="Span{T}"/> containing the new pixel data.</param>
    /// <param name="rectX">The X coordinate of the first pixel to write.</param>
    /// <param name="rectY">The Y coordinate of the first pixel to write.</param>
    /// <param name="rectWidth">The width of the rectangle of pixels to write.</param>
    /// <param name="rectHeight">The height of the rectangle of pixels to write.</param>
    /// <param name="layerIndex">The layer index of the array to write to.</param>
    /// <param name="mipLevel">The mip level to write to.</param>
    public unsafe void SetData<T>(Span<T> data, uint rectX, uint rectY, uint rectWidth, uint rectHeight, uint layerIndex, uint mipLevel = 0) where T : unmanaged =>
        InternalSetData(data, new Vector3Int((int)rectX, (int)rectY, 0), new Vector3Int((int)rectWidth, (int)rectHeight, 0), layerIndex, mipLevel);

    /// <summary>
    /// Sets the data of an entire layer of the <see cref="Texture2DArray"/>.
    /// </summary>
    /// <typeparam name="T">A struct with the same format as this <see cref="Texture2DArray"/>'s pixels.</typeparam>
    /// <param name="data">A <see cref="Span{T}"/> containing the new pixel data.</param>
    /// <param name="layerIndex">The layer index of the array to write to.</param>
    /// <param name="mipLevel">The mip level to copy.</param>
    public void SetData<T>(Span<T> data, uint layerIndex, uint mipLevel = 0) where T : unmanaged =>
        SetData(data, 0, 0, Width, Height, layerIndex, mipLevel);

    /// <summary>
    /// Copies the data of a portion of a <see cref="Texture2DArray"/>.
    /// </summary>
    /// <param name="data">The pointer to the copied .</param>
    /// <param name="mipLevel">The mip level to copy.</param>
    /// <param name="layer">The array layer to copy.</param>
    /// <param name="dataSize">The number of bytes to copy from the source.</param>
    public unsafe void CopyDataPtr(void* data, uint dataSize, uint layer, uint mipLevel = 0) =>
        InternalCopyDataPtr(data, dataSize, out _, out _, layer, mipLevel);

    /// <summary>
    /// Copies the data of a portion of a <see cref="Texture2DArray"/> into a CPU-accessible region.
    /// </summary>
    /// <typeparam name="T">A struct with the same format as this <see cref="Texture2DArray"/>'s pixels.</typeparam>
    /// <param name="data">A <see cref="Span{T}"/> in which to write the pixel data.</param>
    /// <param name="mipLevel">The mip level to copy.</param>
    /// <param name="layer">The array layer to copy.</param>
    public unsafe void CopyData<T>(Span<T> data, uint layer, uint mipLevel = 0) where T : unmanaged =>
        InternalCopyData(data, layer, mipLevel);

    /// <summary>
    /// Gets the pixel at a position in a <see cref="Texture2DArray"/>.
    /// </summary>
    /// <typeparam name="T">A struct with the same format as this <see cref="Texture2DArray"/>'s pixels.</typeparam>
    /// <param name="x">The X coordinate of the pixel to get.</param>
    /// <param name="y">The Y coordinate of the pixel to get.</param>
    /// <param name="mipLevel">The mip level to get.</param>
    /// /// <param name="layer">The array layer to get.</param>
    public unsafe T GetPixel<T>(uint x, uint y, uint layer, uint mipLevel = 0) where T : unmanaged =>
        InternalCopyPixel<T>(new Vector3Int((int)x, (int)y, 0), layer, mipLevel);

    /// <summary>
    /// Recreates and resizes the <see cref="Texture2DArray"/>.
    /// </summary>
    public void RecreateTexture(uint width, uint height, uint layers)
    {
        RecreateInternalTexture(new()
        {
            Width = width,
            Height = height,
            MipLevels = MipLevels,
            ArrayLayers = layers,
            Format = Format,
            Usage = Usage,
            Type = Type,
        });
    }

    public uint GetSingleTextureMemoryUsage()
    {
        return Width * Height * TextureUtility.PixelFormatBytes(Format);
    }

    public override void Serialize(ref EchoObject compoundTag, SerializationContext ctx)
    {
        SerializeHeader(compoundTag);

        compoundTag.Add("Width", new((int)Width));
        compoundTag.Add("Height", new((int)Height));
        compoundTag.Add("Layers", new((int)Layers));
        compoundTag.Add("MipLevels", new(MipLevels));
        compoundTag.Add("IsMipMapped", new(IsMipmapped));
        compoundTag.Add("ImageFormat", new((int)Format));
        compoundTag.Add("Usage", new((int)Usage));

        EchoObject dataTag = EchoObject.NewList();

        for (uint i = 0; i < Layers; i++)
        {
            Span<byte> memory = new byte[GetSingleTextureMemoryUsage()];
            CopyData(memory, i);
            dataTag.ListAdd(new(memory.ToArray()));
        }

        compoundTag.Add("Data", dataTag);
    }

    public override void Deserialize(EchoObject value, SerializationContext ctx)
    {
        DeserializeHeader(value);

        uint width = (uint)value["Width"].IntValue;
        uint height = (uint)value["Height"].IntValue;
        uint layers = (uint)value["Layers"].IntValue;
        uint mips = (uint)value["MipLevels"].IntValue;
        bool isMipMapped = value["IsMipMapped"].BoolValue;
        PixelFormat imageFormat = (PixelFormat)value["ImageFormat"].IntValue;
        TextureUsage usage = (TextureUsage)value["Usage"].IntValue;

        var param = new[] { typeof(uint), typeof(uint), typeof(uint), typeof(uint), typeof(PixelFormat), typeof(TextureUsage) };
        var values = new object[] { width, height, layers, mips, imageFormat, usage };

        typeof(Texture2DArray).GetConstructor(param).Invoke(this, values);

        var dataTag = value.Get("Data");

        for (uint i = 0; i < layers; i++)
        {
            Span<byte> memory = dataTag[(int)i].ByteArrayValue;
            SetData(memory, i);
        }

        if (isMipMapped)
            GenerateMipmaps();
    }
}
