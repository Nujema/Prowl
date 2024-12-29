﻿// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Prowl.Echo;

using Veldrid;

namespace Prowl.Runtime.Rendering;

public struct RenderTextureDescription
{
    public uint width;
    public uint height;

    public PixelFormat[] colorBufferFormats;
    public PixelFormat? depthBufferFormat;

    public bool sampled;
    public bool enableRandomWrite;

    public TextureSampleCount sampleCount;

    public RenderTextureDescription(
        uint width, uint height,
        PixelFormat? depthFormat,
        PixelFormat[] colorFormats,
        bool sampled = true, bool randomWrite = false,
        TextureSampleCount sampleCount = TextureSampleCount.Count1)
    {
        this.width = width;
        this.height = height;
        depthBufferFormat = depthFormat;
        colorBufferFormats = colorFormats;
        this.sampled = sampled;
        enableRandomWrite = randomWrite;
        this.sampleCount = sampleCount;
    }

    public RenderTextureDescription(RenderTexture texture)
    {
        width = texture.Width;
        height = texture.Height;
        depthBufferFormat = texture.DepthBuffer?.Format;
        colorBufferFormats = texture.ColorBuffers.Select(x => x.Format).ToArray();
        sampled = texture.Sampled;
        enableRandomWrite = texture.RandomWriteEnabled;
        sampleCount = texture.SampleCount;
    }

    public readonly bool Equals(RenderTextureDescription other)
    {
        if (width != other.width || height != other.height)
            return false;

        if (sampled != other.sampled || enableRandomWrite != other.enableRandomWrite)
            return false;

        if (sampleCount != other.sampleCount)
            return false;

        if (depthBufferFormat.HasValue != other.depthBufferFormat.HasValue)
            return false;

        if (depthBufferFormat.HasValue && !depthBufferFormat.Equals(other.depthBufferFormat))
            return false;

        // Dont equals directly since that would be a reference equality check
        if (colorBufferFormats == null && other.colorBufferFormats == null)
            return true;

        if (colorBufferFormats == null || other.colorBufferFormats == null)
            return false;

        if (colorBufferFormats.Length != other.colorBufferFormats.Length)
            return false;

        // Compare each format in the array
        return colorBufferFormats.SequenceEqual(other.colorBufferFormats);
    }

    public override readonly bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is RenderTextureDescription other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(width);
        hash.Add(height);

        if (depthBufferFormat.HasValue)
            hash.Add(depthBufferFormat.Value);

        if (colorBufferFormats != null)
        {
            foreach (PixelFormat format in colorBufferFormats)
                hash.Add(format);
        }

        hash.Add(sampled);
        hash.Add(enableRandomWrite);
        hash.Add(sampleCount);

        return hash.ToHashCode();
    }

    public static bool operator ==(RenderTextureDescription left, RenderTextureDescription right) => left.Equals(right);
    public static bool operator !=(RenderTextureDescription left, RenderTextureDescription right) => !(left == right);
}

public sealed class RenderTexture : EngineObject, ISerializable
{
    // Since Veldrid does not provide any methods to check how many color attachments a framebuffer supports, we can cap it ourselves to a reasonable value.
    const int colorAttachmentLimit = 8;

    public Framebuffer Framebuffer { get; private set; }

    public Texture2D[] ColorBuffers { get; private set; }

    public Texture2D DepthBuffer { get; private set; }

    public uint Width { get; private set; }
    public uint Height { get; private set; }

    public bool Sampled { get; private set; }
    public bool RandomWriteEnabled { get; private set; }

    public TextureSampleCount SampleCount { get; private set; }


    public RenderTexture(RenderTextureDescription description) : this(
        description.width,
        description.height,
        description.colorBufferFormats,
        description.depthBufferFormat,
        description.sampled,
        description.enableRandomWrite,
        description.sampleCount
    )
    { }

    /// <summary>
    /// Creates a new RenderTexture object with the best supported depth format and a full-channel 8-bit unsigned normalized color format
    /// </summary>
    /// <param name="width">The width of the <see cref="RenderTexture"/> and its internal buffers.</param>
    /// <param name="height">The height of the <see cref="RenderTexture"/> and its internal buffers.</param>
    /// <param name="sampled">Whether or not the texture is sampleable as a uniform.</param>
    /// <param name="enableRandomWrite">Enable random reads/writes to the <see cref="RenderTexture"/> internal buffers. This is useful within compute shaders which draw to the texture.</param>
    /// <param name="sampleCount">The multisampled count of the texture. Values above Count1 will require the texture to be resolved with <see cref="CommandBuffer.ResolveMultisampledTexture(RenderTexture, RenderTexture)"/> </param>
    public RenderTexture(
        uint width, uint height,
        bool sampled = false,
        bool enableRandomWrite = false,
        TextureSampleCount sampleCount = TextureSampleCount.Count1
    ) : this(width, height, [PixelFormat.R8_G8_B8_A8_UNorm], TextureUtility.GetBestSupportedDepthFormat(), sampled, enableRandomWrite, sampleCount)
    { }

    /// <summary>
    /// Creates a new RenderTexture object with the best supported depth format
    /// </summary>
    /// <param name="width">The width of the <see cref="RenderTexture"/> and its internal buffers.</param>
    /// <param name="height">The height of the <see cref="RenderTexture"/> and its internal buffers.</param>
    /// <param name="colorFormats">The format of the color buffer(s) in the <see cref="RenderTexture"/>. Passing null or empty will omit the creation of a color buffer.</param>
    /// <param name="sampled">Whether or not the texture is sampleable as a uniform.</param>
    /// <param name="enableRandomWrite">Enable random reads/writes to the <see cref="RenderTexture"/> internal buffers. This is useful within compute shaders which draw to the texture.</param>
    /// <param name="sampleCount">The multisampled count of the texture. Values above Count1 will require the texture to be resolved with <see cref="CommandBuffer.ResolveMultisampledTexture(RenderTexture, RenderTexture)"/> </param>
    public RenderTexture(
        uint width, uint height,
        PixelFormat[] colorFormats = null,
        bool sampled = false,
        bool enableRandomWrite = false,
        TextureSampleCount sampleCount = TextureSampleCount.Count1
    ) : this(width, height, colorFormats, TextureUtility.GetBestSupportedDepthFormat(), sampled, enableRandomWrite, sampleCount)
    { }

    /// <summary>
    /// Creates a new RenderTexture object
    /// </summary>
    /// <param name="width">The width of the <see cref="RenderTexture"/> and its internal buffers.</param>
    /// <param name="height">The height of the <see cref="RenderTexture"/> and its internal buffers.</param>
    /// <param name="colorFormats">The format of the color buffer(s) in the <see cref="RenderTexture"/>. Passing null or empty will omit the creation of a color buffer.</param>
    /// <param name="depthFormat">The format of the depth stencil buffer in the <see cref="RenderTexture"/>. Passing null or empty will omit the creation of the depth stencil buffer.</param>
    /// <param name="enableRandomWrite">Enable random reads/writes to the <see cref="RenderTexture"/> internal buffers. This is useful within compute shaders which draw to the texture.</param>
    /// <param name="sampled">Whether or not the texture is sampleable as a uniform.</param>
    /// <param name="sampleCount">The multisampled count of the texture. Values above Count1 will require the texture to be resolved with <see cref="CommandBuffer.ResolveMultisampledTexture(RenderTexture, RenderTexture)"/> </param>
    public RenderTexture(
        uint width, uint height,
        PixelFormat[] colorFormats = null,
        PixelFormat? depthFormat = null,
        bool sampled = false,
        bool enableRandomWrite = false,
        TextureSampleCount sampleCount = TextureSampleCount.Count1
    ) : base("RenderTexture")
    {
        if (colorFormats != null && colorFormats.Length > colorAttachmentLimit)
            throw new Exception($"Invalid number of color buffers! [0-{colorAttachmentLimit}]");

        Width = width;
        Height = height;
        Sampled = sampled;
        RandomWriteEnabled = enableRandomWrite;
        SampleCount = sampleCount;

        if (depthFormat != null)
        {
            TextureUsage depthUsage = TextureUsage.DepthStencil; // sampled ? TextureUsage.Sampled | TextureUsage.DepthStencil : TextureUsage.DepthStencil;
            DepthBuffer = new Texture2D(Width, Height, 1, depthFormat.Value, depthUsage)
            {
                Name = $"RT Depth Buffer"
            };
        }

        ColorBuffers = new Texture2D[colorFormats?.Length ?? 0];
        if (colorFormats != null)
        {
            TextureUsage sampleType = enableRandomWrite ? TextureUsage.Storage : TextureUsage.Sampled;
            TextureUsage colorUsage = sampled ? sampleType | TextureUsage.RenderTarget : TextureUsage.RenderTarget;

            for (int i = 0; i < ColorBuffers.Length; i++)
            {
                ColorBuffers[i] = new Texture2D(Width, Height, 1, colorFormats[i], colorUsage, sampleCount)
                {
                    Name = $"RT Color Buffer {i}"
                };
            }
        }

        FramebufferDescription description = new FramebufferDescription(DepthBuffer?.InternalTexture, ColorBuffers.Select(x => x.InternalTexture).ToArray());

        Framebuffer = Graphics.Factory.CreateFramebuffer(description);
    }

    public override void OnDispose()
    {
        DepthBuffer?.DestroyImmediate();

        if (ColorBuffers != null)
            foreach (Texture2D tex in ColorBuffers)
                tex?.DestroyImmediate();

        Framebuffer?.Dispose();

        DepthBuffer = null;
        ColorBuffers = null;
        Framebuffer = null;
    }

    public void Serialize(ref EchoObject compoundTag, SerializationContext ctx)
    {
        SerializeHeader(compoundTag);

        compoundTag.Add("Width", new(Width));
        compoundTag.Add("Height", new(Height));
        compoundTag.Add("EnableRandomWrite", new(RandomWriteEnabled));

        compoundTag.Add("DepthBufferFormat", new(DepthBuffer != null ? (int)DepthBuffer.Format : -1));

        EchoObject colorBuffersTag = EchoObject.NewList();

        if (ColorBuffers != null)
        {
            foreach (var colorBuffer in ColorBuffers)
                colorBuffersTag.ListAdd(new((int)colorBuffer.Format));
        }

        compoundTag.Add("ColorBufferFormats", colorBuffersTag);
    }

    public void Deserialize(EchoObject value, SerializationContext ctx)
    {
        DeserializeHeader(value);

        uint width = (uint)value["Width"].IntValue;
        uint height = (uint)value["Height"].IntValue;
        bool randomWrite = value["EnableRandomWrite"].BoolValue;

        int depthFormatInt = value["DepthBufferFormat"].IntValue;
        PixelFormat? depthBufferFormat = depthFormatInt < 0 ? null : (PixelFormat)depthFormatInt;

        var colorBuffersTag = value.Get("ColorBufferFormats");
        PixelFormat[] colorBufferFormats = new PixelFormat[colorBuffersTag.Count];

        for (int i = 0; i < colorBuffersTag.Count; i++)
        {
            int colorFormatInt = colorBuffersTag[i].IntValue;
            colorBufferFormats[i] = (PixelFormat)colorFormatInt;
        }

        var param = new[] { typeof(uint), typeof(uint), typeof(PixelFormat?), typeof(PixelFormat[]), typeof(bool) };
        var values = new object[] { width, height, depthBufferFormat, colorBufferFormats, randomWrite };
        typeof(RenderTexture).GetConstructor(param).Invoke(this, values);
    }

    struct TextureFormatComparer : IEqualityComparer<Texture2D>
    {
        public readonly bool Equals(Texture2D? x, Texture2D? y)
        {
            return x.Format == y.Format;
        }

        public readonly int GetHashCode([DisallowNull] Texture2D obj)
        {
            return obj.Format.GetHashCode();
        }
    }

    public bool FormatEquals(RenderTexture other, bool compareMS = true)
    {
        if (Width != other.Width || Height != other.Height)
            return false;

        if (DepthBuffer.Format != other.DepthBuffer.Format)
            return false;

        if (!ColorBuffers.SequenceEqual(other.ColorBuffers, new TextureFormatComparer()))
            return false;

        if (Sampled != other.Sampled)
            return false;

        if (RandomWriteEnabled != other.RandomWriteEnabled)
            return false;

        if (compareMS && SampleCount != other.SampleCount)
            return false;

        return true;
    }

    // Cast to Framebuffer
    public static implicit operator Framebuffer(RenderTexture rt) => rt.Framebuffer;

    // Cast to Texture2D
    public static implicit operator Texture2D(RenderTexture rt) => rt.ColorBuffers[0];

    #region Pool

    private static readonly Dictionary<RenderTextureDescription, List<(RenderTexture, int)>> s_pool = [];


    private const int TextureAliveTime = 10;


    public static RenderTexture GetTemporaryRT(
        uint width, uint height,
        bool sampled = true, bool randomWrite = false,
        TextureSampleCount samples = TextureSampleCount.Count1)
    {
        return GetTemporaryRT(new RenderTextureDescription(width, height, TextureUtility.GetBestSupportedDepthFormat(), [PixelFormat.R8_G8_B8_A8_UNorm], sampled, randomWrite, samples));
    }


    public static RenderTexture GetTemporaryRT(
        uint width, uint height,
        PixelFormat[] colorFormats,
        bool sampled = true, bool randomWrite = false,
        TextureSampleCount samples = TextureSampleCount.Count1)
    {
        return GetTemporaryRT(new RenderTextureDescription(width, height, TextureUtility.GetBestSupportedDepthFormat(), colorFormats, sampled, randomWrite, samples));
    }


    public static RenderTexture GetTemporaryRT(
        uint width, uint height,
        PixelFormat? depthFormat,
        PixelFormat[] colorFormats,
        bool sampled = true, bool randomWrite = false,
        TextureSampleCount samples = TextureSampleCount.Count1)
    {
        return GetTemporaryRT(new RenderTextureDescription(width, height, depthFormat, colorFormats, sampled, randomWrite, samples));
    }


    public static RenderTexture GetTemporaryRT(RenderTextureDescription description)
    {
        if (!s_pool.TryGetValue(description, out List<(RenderTexture, int)>? list))
            return new RenderTexture(description);

        if (list.Count < 1)
            return new RenderTexture(description);

        (RenderTexture renderTexture, int _) = list[^1];

        if (renderTexture.IsDestroyed)
            throw new Exception("RenderTexture is destroyed inside pool list");

        list.RemoveAt(list.Count - 1);

        // Remove empty lists to prevent Dictionary bloat
        if (list.Count == 0)
            s_pool.Remove(description);

        return renderTexture;
    }


    public static void ReleaseTemporaryRT(RenderTexture renderTexture)
    {
        ArgumentNullException.ThrowIfNull(renderTexture);
        ArgumentNullException.ThrowIfNull(renderTexture.Framebuffer, "RenderTexture.FrameBuffer");

        var key = new RenderTextureDescription(renderTexture);

        if (!s_pool.TryGetValue(key, out List<(RenderTexture, int)>? list))
        {
            list = [];
            s_pool[key] = list;
        }

        list.Add((renderTexture, TextureAliveTime));
    }


    public static void UpdatePool()
    {
        foreach (var kvp in s_pool.ToList())
        {
            RenderTextureDescription key = kvp.Key;
            List<(RenderTexture, int)> list = kvp.Value;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                (RenderTexture tex, int time) = list[i];

                if (time > 0 && tex != null)
                {
                    time--;
                    list[i] = (tex, time);

                    continue;
                }

                list.RemoveAt(i);

                if (tex != null)
                    tex.DestroyLater();
            }

            if (list.Count < 1)
                s_pool.Remove(key);
        }
    }

    #endregion
}
