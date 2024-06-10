﻿using System;
using Veldrid;
using Veldrid.StartupUtilities;


namespace Prowl.Runtime
{

    public static class Graphics
    {
        public static GraphicsDevice Device { get; internal set; }
        
        public static Swapchain MainSwapchain => Device.MainSwapchain;
        public static Framebuffer Framebuffer => Device.SwapchainFramebuffer;
        public static ResourceFactory ResourceFactory => ResourceFactory;


        public static Vector2 Resolution;
        public static Matrix4x4 MatView;
        public static Matrix4x4 MatProjection;
        public static Matrix4x4 MatProjectionInverse;
        public static Matrix4x4 OldMatView;
        public static Matrix4x4 OldMatProjection;

        public static Matrix4x4 MatDepthProjection;
        public static Matrix4x4 MatDepthView;

        public static Vector2 Jitter { get; set; }
        public static Vector2 PreviousJitter { get; set; }
        public static bool UseJitter;

        private static Material defaultMat;
        internal static Vector2Int FrameBufferSize;

        public static bool VSync {
            get { return Device.SyncToVerticalBlank; }
            set { Device.SyncToVerticalBlank = value; }
        }

        public static void Initialize(bool VSync = true, GraphicsBackend preferredBackend = GraphicsBackend.OpenGL)
        {
            GraphicsDeviceOptions deviceOptions = new()
            {
                SyncToVerticalBlank = VSync,
                PreferStandardClipSpaceYDirection = true,
                PreferDepthRangeZeroToOne = true,
                ResourceBindingModel = ResourceBindingModel.Default,
                HasMainSwapchain = true,
            };

            Device = VeldridStartup.CreateGraphicsDevice(Screen.InternalWindow, deviceOptions, preferredBackend);
        }

        public static void StartFrame()
        {
            RenderTexture.UpdatePool();

            Clear();
            Viewport((int)Framebuffer.Width, (int)Framebuffer.Height);
            // Set default states
            Device.SetState(new(), true);
        }

        public static void EndFrame()
        {

        }

        public static void DrawMeshNow(Mesh mesh, Matrix4x4 transform, Material material, Matrix4x4? oldTransform = null)
        {
            if (Camera.Current == null) throw new Exception("DrawMeshNow must be called during a rendering context like OnRenderObject()!");

            if (Device.CurrentProgram == null) throw new Exception("Non Program Assigned, Use Material.SetPass first before calling DrawMeshNow!");

            oldTransform ??= transform;

            if (UseJitter)
            {
                material.SetVector("Jitter", Jitter);
                material.SetVector("PreviousJitter", PreviousJitter);
            }
            else
            {
                material.SetVector("Jitter", Vector2.zero);
                material.SetVector("PreviousJitter", Vector2.zero);
            }

            material.SetVector("Resolution", Resolution);
            material.SetFloat("Time", (float)Time.time);
            material.SetInt("Frame", (int)Time.frameCount);
            material.SetVector("Camera_WorldPosition", Camera.Current.GameObject.Transform.position);

            // Upload view and projection matrices(if locations available)
            material.SetMatrix("matView", MatView);

            material.SetMatrix("matProjection", MatProjection);
            material.SetMatrix("matProjectionInverse", MatProjectionInverse);
            // Model transformation matrix is sent to shader
            material.SetMatrix("matModel", transform);

            Matrix4x4 matMVP = Matrix4x4.Identity;
            matMVP = Matrix4x4.Multiply(matMVP, transform);
            matMVP = Matrix4x4.Multiply(matMVP, MatView);
            matMVP = Matrix4x4.Multiply(matMVP, MatProjection);

            Matrix4x4 oldMatMVP = Matrix4x4.Identity;
            oldMatMVP = Matrix4x4.Multiply(oldMatMVP, oldTransform.Value);
            oldMatMVP = Matrix4x4.Multiply(oldMatMVP, OldMatView);
            oldMatMVP = Matrix4x4.Multiply(oldMatMVP, OldMatProjection);

            // Send combined model-view-projection matrix to shader
            //material.SetMatrix("mvp", matModelViewProjection);
            material.SetMatrix("mvp", matMVP);
            Matrix4x4.Invert(matMVP, out var mvpInverse);
            material.SetMatrix("mvpInverse", mvpInverse);
            material.SetMatrix("mvpOld", oldMatMVP);

            // Mesh data can vary between meshes, so we need to let the shaders know which attributes are in use
            material.SetKeyword("HAS_NORMALS", mesh.HasNormals);
            material.SetKeyword("HAS_TANGENTS", mesh.HasTangents);
            material.SetKeyword("HAS_UV", mesh.HasUV);
            material.SetKeyword("HAS_UV2", mesh.HasUV2);
            material.SetKeyword("HAS_COLORS", mesh.HasColors || mesh.HasColors32);

            material.SetKeyword("HAS_BONEINDICES", mesh.HasBoneIndices);
            material.SetKeyword("HAS_BONEWEIGHTS", mesh.HasBoneWeights);

            // All material uniforms have been assigned, its time to properly set them
            MaterialPropertyBlock.Apply(material.PropertyBlock, Graphics.Device.CurrentProgram);

            DrawMeshNowDirect(mesh);
        }

        public static void DrawMeshNowDirect(Mesh mesh)
        {
            if (Camera.Current == null) throw new Exception("DrawMeshNow must be called during a rendering context like OnRenderObject()!");
            if (Graphics.Device.CurrentProgram == null) throw new Exception("Non Program Assigned, Use Material.SetPass first before calling DrawMeshNow!");

            mesh.Upload();

            unsafe
            {
                Device.BindVertexArray(mesh.VertexArrayObject);
                Device.DrawIndexed(Topology.Triangles, (uint)mesh.IndexCount, mesh.IndexFormat == IndexFormat.UInt32, null);
                Device.BindVertexArray(null);
            }
        }

        /// <summary>
        /// Draws material with a FullScreen Quad
        /// </summary>
        public static void Blit(Material mat, int pass = 0)
        {
            mat.SetPass(pass);
            DrawMeshNow(Mesh.GetFullscreenQuad(), Matrix4x4.Identity, mat);
        }

        /// <summary>
        /// Draws material with a FullScreen Quad onto a RenderTexture
        /// </summary>
        public static void Blit(RenderTexture? renderTexture, Material mat, int pass = 0, bool clear = true)
        {
            renderTexture?.Begin();
            if (clear)
                Clear(0, 0, 0, 0);
            mat.SetPass(pass);
            DrawMeshNow(Mesh.GetFullscreenQuad(), Matrix4x4.Identity, mat);
            renderTexture?.End();

        }

        /// <summary>
        /// Draws texture into a RenderTexture Additively
        /// </summary>
        public static void Blit(RenderTexture? renderTexture, Texture2D texture, bool clear = true)
        {
            defaultMat ??= new Material(Shader.Find("Defaults/Basic.shader"));
            defaultMat.SetTexture("texture0", texture);
            defaultMat.SetPass(0);

            renderTexture?.Begin();
            if (clear) Clear(0, 0, 0, 0);
            DrawMeshNow(Mesh.GetFullscreenQuad(), Matrix4x4.Identity, defaultMat);
            renderTexture?.End();
        }

        internal static void Dispose()
        {
            Device.Dispose();
        }

        internal static void BlitDepth(RenderTexture source, RenderTexture? destination)
        {
            Device.BindFramebuffer(source.frameBuffer, FBOTarget.Read);
            if(destination != null)
                Device.BindFramebuffer(destination?.frameBuffer, FBOTarget.Draw);
            Device.BlitFramebuffer(0, 0, source.Width, source.Height,
                                        0, 0, destination?.Width ?? (int)Graphics.Resolution.x, destination?.Height ?? (int)Graphics.Resolution.y,
                                        ClearFlags.Depth, BlitFilter.Nearest
                                        );
            Device.UnbindFramebuffer();


        }


        public static void CopyTexture(Texture source, Texture destination)
        {
            CommandList commandList = ResourceFactory.CreateCommandList();

            commandList.Begin();
            commandList.CopyTexture(source.InternalTexture, destination.InternalTexture);
            commandList.End();

            Device.SubmitCommands(commandList);
        }


        public static void CopyTexture(Texture source, Texture destination, uint mipLevel, uint arrayLayer)
        {
            CommandList commandList = ResourceFactory.CreateCommandList();

            commandList.Begin();
            commandList.CopyTexture(source.InternalTexture, destination.InternalTexture, mipLevel, arrayLayer);
            commandList.End();

            Device.SubmitCommands(commandList);
        }
    }
}
