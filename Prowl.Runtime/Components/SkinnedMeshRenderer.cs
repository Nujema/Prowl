﻿// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System.Collections.Generic;

using Prowl.Runtime.Rendering;
using Prowl.Runtime.Rendering.Pipelines;

using Prowl.Icons;

namespace Prowl.Runtime;


[ExecuteAlways, AddComponentMenu($"{FontAwesome6.Tv}  Rendering/{FontAwesome6.Shapes}  Skinned Mesh Renderer")]
public class SkinnedMeshRenderer : MonoBehaviour, ISerializable, IRenderable
{
    public AssetRef<Mesh> Mesh;
    public AssetRef<Material> Material;

    public PropertyState Properties;

    [HideInInspector]
    public Transform[] Bones = [];

    private System.Numerics.Matrix4x4[] boneTransforms;


    void GetBoneMatrices()
    {
        boneTransforms = new System.Numerics.Matrix4x4[Bones.Length];
        for (int i = 0; i < Bones.Length; i++)
        {
            Transform t = Bones[i];

            if (t == null)
                boneTransforms[i] = System.Numerics.Matrix4x4.Identity;
            else
                boneTransforms[i] = (t.localToWorldMatrix * GameObject.Transform.worldToLocalMatrix).ToFloat();
        }
    }


    public override void Update()
    {
        if (!Mesh.IsAvailable) return;
        if (!Material.IsAvailable) return;

        GetBoneMatrices();

        Material.Res!.EnableKeyword("SKINNED");
        Material.Res!.SetInt("ObjectID", GameObject.InstanceID);
        Material.Res!.SetMatrices("bindPoses", Mesh.Res.bindPoses);
        Material.Res!.SetMatrices("boneTransforms", boneTransforms);
        for (int i = 0; i < Material.Res!.PassCount; i++)
        {
            Material.Res!.SetPass(i);
            Graphics.DrawMeshNow(Mesh.Res!, mat, Material.Res!, prevMat);
        }
        Material.Res!.DisableKeyword("SKINNED");

        prevMats[camID] = mat;
    }


    public SerializedProperty Serialize(Serializer.SerializationContext ctx)
    {
        SerializedProperty compoundTag = SerializedProperty.NewCompound();
        compoundTag.Add("Mesh", Serializer.Serialize(Mesh, ctx));
        compoundTag.Add("Material", Serializer.Serialize(Material, ctx));
        compoundTag.Add("Bones", Serializer.Serialize(Bones, ctx));

        return compoundTag;
    }


    public void Deserialize(SerializedProperty value, Serializer.SerializationContext ctx)
    {
        Mesh = Serializer.Deserialize<AssetRef<Mesh>>(value["Mesh"], ctx);
        Material = Serializer.Deserialize<AssetRef<Material>>(value["Material"], ctx);
        Bones = Serializer.Deserialize<Transform[]>(value["Bones"], ctx);
    }


    public Material GetMaterial()
    {
        return Material.Res;
    }


    public void GetRenderingData(out PropertyState properties, out IGeometryDrawData drawData, out Matrix4x4 model)
    {

    }


    public void GetCullingData(out bool isRenderable, out Bounds bounds)
    {

    }
}
