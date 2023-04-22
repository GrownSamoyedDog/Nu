﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2023.

namespace OpenGL
open System
open System.IO
open System.Numerics
open System.Runtime.InteropServices
open FSharp.NativeInterop
open Prime
open Nu

[<RequireQualifiedAccess>]
module PhysicallyBased =

    /// Describes the configurable properties of a physically-based material.
    type [<StructuralEquality; NoComparison; Struct>] PhysicallyBasedMaterialProperties =
        { Albedo : Color
          Metallic : single
          Roughness : single
          AmbientOcclusion : single
          Emission : single
          Height : single
          InvertRoughness : bool }

    /// Describes a physically-based material.
    type [<StructuralEquality; NoComparison; Struct>] PhysicallyBasedMaterial =
        { MaterialProperties : PhysicallyBasedMaterialProperties
          AlbedoMetadata : Texture.TextureMetadata
          AlbedoTexture : uint
          MetallicTexture : uint
          RoughnessTexture : uint
          AmbientOcclusionTexture : uint
          EmissionTexture : uint
          NormalTexture : uint
          HeightTexture : uint
          TextureMinFilterOpt : OpenGL.TextureMinFilter option
          TextureMagFilterOpt : OpenGL.TextureMagFilter option
          TwoSided : bool }

    /// Describes some physically-based geometry that's loaded into VRAM.
    type PhysicallyBasedGeometry =
        { Bounds : Box3
          PrimitiveType : PrimitiveType
          ElementCount : int
          Vertices : Vector3 array
          Indices : int array
          VertexBuffer : uint
          ModelBuffer : uint
          TexCoordsOffsetBuffer : uint
          AlbedoBuffer : uint
          MaterialBuffer : uint
          HeightBuffer : uint
          InvertRoughnessBuffer : uint
          IndexBuffer : uint
          PhysicallyBasedVao : uint }

    /// Describes a renderable physically-based surface.
    type [<CustomEquality; NoComparison>] PhysicallyBasedSurface =
        { mutable HashCode : int
          SurfaceNames : string array
          SurfaceMatrixIsIdentity : bool // OPTIMIZATION: avoid matrix multiply when unnecessary.
          SurfaceMatrix : Matrix4x4
          SurfaceBounds : Box3
          SurfaceMaterial : PhysicallyBasedMaterial
          PhysicallyBasedGeometry : PhysicallyBasedGeometry }

        static member inline hash surface =
            (int surface.SurfaceMaterial.AlbedoTexture) ^^^
            (int surface.SurfaceMaterial.MetallicTexture <<< 2) ^^^
            (int surface.SurfaceMaterial.RoughnessTexture <<< 4) ^^^
            (int surface.SurfaceMaterial.AmbientOcclusionTexture <<< 6) ^^^
            (int surface.SurfaceMaterial.EmissionTexture <<< 7) ^^^
            (int surface.SurfaceMaterial.NormalTexture <<< 10) ^^^
            (int surface.SurfaceMaterial.HeightTexture <<< 12) ^^^
            (hash surface.SurfaceMaterial.TextureMinFilterOpt <<< 14) ^^^
            (hash surface.SurfaceMaterial.TextureMagFilterOpt <<< 16) ^^^
            (hash surface.SurfaceMaterial.TwoSided <<< 18) ^^^
            (int surface.PhysicallyBasedGeometry.PrimitiveType <<< 20) ^^^
            (int surface.PhysicallyBasedGeometry.PhysicallyBasedVao <<< 22)

        static member inline equals left right =
            (match (left.SurfaceMaterial.TextureMinFilterOpt, right.SurfaceMaterial.TextureMinFilterOpt) with // TODO: implement voptEq.
             | (Some leftFilter, Some rightFilter) -> leftFilter = rightFilter
             | (None, None) -> true
             | (_, _) -> false) &&
            (match (left.SurfaceMaterial.TextureMagFilterOpt, right.SurfaceMaterial.TextureMagFilterOpt) with
             | (Some leftFilter, Some rightFilter) -> leftFilter = rightFilter
             | (None, None) -> true
             | (_, _) -> false) &&
            left.SurfaceMaterial.AlbedoTexture = right.SurfaceMaterial.AlbedoTexture &&
            left.SurfaceMaterial.MetallicTexture = right.SurfaceMaterial.MetallicTexture &&
            left.SurfaceMaterial.RoughnessTexture = right.SurfaceMaterial.RoughnessTexture &&
            left.SurfaceMaterial.AmbientOcclusionTexture = right.SurfaceMaterial.AmbientOcclusionTexture &&
            left.SurfaceMaterial.EmissionTexture = right.SurfaceMaterial.EmissionTexture &&
            left.SurfaceMaterial.NormalTexture = right.SurfaceMaterial.NormalTexture &&
            left.SurfaceMaterial.HeightTexture = right.SurfaceMaterial.HeightTexture &&
            left.SurfaceMaterial.TwoSided = right.SurfaceMaterial.TwoSided &&
            left.PhysicallyBasedGeometry.PrimitiveType = right.PhysicallyBasedGeometry.PrimitiveType &&
            left.PhysicallyBasedGeometry.PhysicallyBasedVao = right.PhysicallyBasedGeometry.PhysicallyBasedVao

        static member internal make names (surfaceMatrix : Matrix4x4) bounds material geometry =
            let mutable result =
                { HashCode = 0
                  SurfaceNames = names
                  SurfaceMatrixIsIdentity = surfaceMatrix.IsIdentity
                  SurfaceMatrix = surfaceMatrix
                  SurfaceBounds = bounds
                  SurfaceMaterial = material
                  PhysicallyBasedGeometry = geometry }
            result.HashCode <- PhysicallyBasedSurface.hash result
            result

        member this.Equals that =
            PhysicallyBasedSurface.equals this that

        override this.Equals (thatObj : obj) =
            match thatObj with
            | :? PhysicallyBasedSurface as that -> PhysicallyBasedSurface.equals this that
            | _ -> false

        override this.GetHashCode () =
            this.HashCode

    /// A light inside a physically-based static model.
    type PhysicallyBasedLight =
        { LightNames : string array
          LightMatrixIsIdentity : bool
          LightMatrix : Matrix4x4
          LightColor : Color
          LightBrightness : single
          LightAttenuationLinear : single
          LightAttenuationQuadratic : single
          PhysicallyBasedLightType : LightType }

    /// A part of a physically-based hierarchy.
    type PhysicallyBasedPart =
        | PhysicallyBasedNode of string array
        | PhysicallyBasedLight of PhysicallyBasedLight
        | PhysicallyBasedSurface of PhysicallyBasedSurface

    /// A physically-based static model.
    type PhysicallyBasedStaticModel =
        { Bounds : Box3
          Lights : PhysicallyBasedLight array
          Surfaces : PhysicallyBasedSurface array
          PhysicallyBasedStaticHierarchy : PhysicallyBasedPart array TreeNode }

    /// Describes a physically-based shader that's loaded into GPU.
    type PhysicallyBasedShader =
        { ViewUniform : int
          ProjectionUniform : int
          EyeCenterUniform : int
          LightAmbientColorUniform : int
          LightAmbientBrightnessUniform : int
          AlbedoTextureUniform : int
          MetallicTextureUniform : int
          RoughnessTextureUniform : int
          AmbientOcclusionTextureUniform : int
          EmissionTextureUniform : int
          NormalTextureUniform : int
          HeightTextureUniform : int
          IrradianceMapUniform : int
          EnvironmentFilterMapUniform : int
          BrdfTextureUniform : int
          LightOriginsUniform : int
          LightDirectionsUniform : int
          LightColorsUniform : int
          LightBrightnessesUniform : int
          LightAttenuationLinearsUniform : int
          LightAttenuationQuadraticsUniform : int
          LightDirectionalsUniform : int
          LightConeInnersUniform : int
          LightConeOutersUniform : int
          PhysicallyBasedShader : uint }

    /// Describes a second pass of a deferred physically-based shader that's loaded into GPU.
    type PhysicallyBasedDeferred2Shader =
        { ViewUniform : int
          ProjectionUniform : int
          EyeCenterUniform : int
          LightAmbientColorUniform : int
          LightAmbientBrightnessUniform : int
          PositionTextureUniform : int
          AlbedoTextureUniform : int
          MaterialTextureUniform : int
          NormalAndDepthTextureUniform : int
          IrradianceMapUniform : int
          EnvironmentFilterMapUniform : int
          BrdfTextureUniform : int
          LightOriginsUniform : int
          LightDirectionsUniform : int
          LightColorsUniform : int
          LightBrightnessesUniform : int
          LightAttenuationLinearsUniform : int
          LightAttenuationQuadraticsUniform : int
          LightDirectionalsUniform : int
          LightConeInnersUniform : int
          LightConeOutersUniform : int
          PhysicallyBasedDeferred2Shader : uint }

    /// Attempt to create physically-based from an assimp mesh.
    let TryCreatePhysicallyBasedMesh (mesh : Assimp.Mesh) =

        // ensure required data is available
        if  mesh.HasVertices &&
            mesh.HasNormals &&
            mesh.HasTextureCoords 0 then

            // attempt to populate geometry data
            if mesh.Vertices.Count = mesh.Normals.Count && mesh.Vertices.Count = mesh.TextureCoordinateChannels.[0].Count then

                // populate vertex data and bounds
                let vertexData = Array.zeroCreate<single> (mesh.Vertices.Count * 8)
                let mutable positionMin = v3Zero
                let mutable positionMax = v3Zero
                for i in 0 .. dec mesh.Vertices.Count do
                    let v = i * 8
                    let position = mesh.Vertices.[i]
                    let texCoords = mesh.TextureCoordinateChannels.[0].[i]
                    let normal = mesh.Normals.[i]
                    vertexData.[v] <- position.X
                    vertexData.[v+1] <- position.Y
                    vertexData.[v+2] <- position.Z
                    vertexData.[v+3] <- texCoords.X
                    vertexData.[v+4] <- 1.0f - texCoords.Y
                    vertexData.[v+5] <- normal.X
                    vertexData.[v+6] <- normal.Y
                    vertexData.[v+7] <- normal.Z
                    positionMin.X <- min positionMin.X position.X
                    positionMin.Y <- min positionMin.Y position.Y
                    positionMin.Z <- min positionMin.Z position.Z
                    positionMax.X <- max positionMax.X position.X
                    positionMax.Y <- max positionMax.Y position.Y
                    positionMax.Z <- max positionMax.Z position.Z
                let bounds = box3 positionMin (positionMax - positionMin)

                // populate triangle index data
                let indexList = SegmentedList.make ()
                for face in mesh.Faces do
                    let indices = face.Indices
                    if indices.Count = 3 then
                        SegmentedList.add indices.[0] indexList
                        SegmentedList.add indices.[1] indexList
                        SegmentedList.add indices.[2] indexList
                let indexData = Seq.toArray indexList

                // fin
                Right (vertexData, indexData, bounds)
                    
            // error
            else Left ("Vertex / normal / tex coords count mismatch.")

        // error
        else Left "Mesh is missing vertices, normals, or texCoords."

    /// Create a mesh for a physically-based quad.
    let CreatePhysicallyBasedQuadMesh () =

        // make vertex data
        let vertexData =
            [|
                (*   positions   *)         (* tex coords *)    (*   normals   *)
                -1.0f; -1.0f; +0.0f;        0.0f; 0.0f;         0.0f;  0.0f; 1.0f;  // bottom-left
                +1.0f; -1.0f; +0.0f;        1.0f; 0.0f;         0.0f;  0.0f; 1.0f;  // bottom-right
                +1.0f; +1.0f; +0.0f;        1.0f; 1.0f;         0.0f;  0.0f; 1.0f;  // top-right
                +1.0f; +1.0f; +0.0f;        1.0f; 1.0f;         0.0f;  0.0f; 1.0f;  // top-right
                -1.0f; +1.0f; +0.0f;        0.0f; 1.0f;         0.0f;  0.0f; 1.0f;  // top-left
                -1.0f; -1.0f; +0.0f;        0.0f; 0.0f;         0.0f;  0.0f; 1.0f;  // bottom-left
            |]

        // make index data trivially
        let indexData = Array.init 6 id

        // make bounds trivially
        let bounds = box3 (v3 -1.0f -1.0f 0.0f) (v3 2.0f 2.0f 0.0f)

        // fin
        (vertexData, indexData, bounds)

    /// Create a mesh for a physically-based particle.
    let CreatePhysicallyBasedParticleMesh () =

        // make vertex data
        let vertexData =
            [|
                (*   positions   *)         (* tex coords *)    (*   normals   *)
                -0.5f; -0.5f; +0.0f;        0.0f; 0.0f;         0.0f;  0.0f; 1.0f;  // bottom-left
                +0.5f; -0.5f; +0.0f;        0.0f; 0.0f;         0.0f;  0.0f; 1.0f;  // bottom-right
                +0.5f; +0.5f; +0.0f;        0.0f; 0.0f;         0.0f;  0.0f; 1.0f;  // top-right
                +0.5f; +0.5f; +0.0f;        0.0f; 0.0f;         0.0f;  0.0f; 1.0f;  // top-right
                -0.5f; +0.5f; +0.0f;        0.0f; 0.0f;         0.0f;  0.0f; 1.0f;  // top-left
                -0.5f; -0.5f; +0.0f;        0.0f; 0.0f;         0.0f;  0.0f; 1.0f;  // bottom-left
            |]

        // make index data trivially
        let indexData = Array.init 6 id

        // make bounds trivially
        let bounds = box3 (v3 -0.5f -0.5f 0.0f) (v3 1.0f 1.0f 0.0f)

        // fin
        (vertexData, indexData, bounds)

    /// Create a mesh for a physically-based billboard.
    let CreatePhysicallyBasedBillboardMesh () =

        // make vertex data
        let vertexData =
            [|
                (*   positions   *)         (* tex coords *)    (*   normals   *)
                -0.5f; -0.5f; +0.0f;        0.0f; 0.0f;         0.0f;  0.0f; 1.0f;  // bottom-left
                +0.5f; -0.5f; +0.0f;        0.0f; 0.0f;         0.0f;  0.0f; 1.0f;  // bottom-right
                +0.5f; +0.5f; +0.0f;        0.0f; 0.0f;         0.0f;  0.0f; 1.0f;  // top-right
                -0.5f; -0.5f; +0.0f;        0.0f; 0.0f;         0.0f;  0.0f; 1.0f;  // top-right
                +0.5f; +0.5f; +0.0f;        0.0f; 0.0f;         0.0f;  0.0f; 1.0f;  // top-left
                -0.5f; +0.5f; +0.0f;        0.0f; 0.0f;         0.0f;  0.0f; 1.0f;  // bottom-left
            |]

        // make index data trivially
        let indexData = Array.init 6 id

        // make bounds trivially
        let bounds = box3 (v3 -0.5f -0.5f 0.0f) (v3 1.0f 1.0f 0.0f)

        // fin
        (vertexData, indexData, bounds)

    /// Create a mesh for a physically-based cube.
    let CreatePhysicallyBasedCubeMesh () =

        // make vertex data
        let vertexData =
            [|
                (*   positions   *)         (* tex coords *)    (*    normals    *)

                // back face
                -0.5f; -0.5f; -0.5f;        0.0f; 0.0f;          0.0f;  0.0f; -1.0f; // bottom-left
                +0.5f; +0.5f; -0.5f;        1.0f; 1.0f;          0.0f;  0.0f; -1.0f; // top-right
                +0.5f; -0.5f; -0.5f;        1.0f; 0.0f;          0.0f;  0.0f; -1.0f; // bottom-right         
                +0.5f; +0.5f; -0.5f;        1.0f; 1.0f;          0.0f;  0.0f; -1.0f; // top-right
                -0.5f; -0.5f; -0.5f;        0.0f; 0.0f;          0.0f;  0.0f; -1.0f; // bottom-left
                -0.5f; +0.5f; -0.5f;        0.0f; 1.0f;          0.0f;  0.0f; -1.0f; // top-left

                // front face
                -0.5f; -0.5f; +0.5f;        0.0f; 0.0f;          0.0f;  0.0f; +1.0f; // bottom-left
                +0.5f; -0.5f; +0.5f;        1.0f; 0.0f;          0.0f;  0.0f; +1.0f; // bottom-right
                +0.5f; +0.5f; +0.5f;        1.0f; 1.0f;          0.0f;  0.0f; +1.0f; // top-right
                +0.5f; +0.5f; +0.5f;        1.0f; 1.0f;          0.0f;  0.0f; +1.0f; // top-right
                -0.5f; +0.5f; +0.5f;        0.0f; 1.0f;          0.0f;  0.0f; +1.0f; // top-left
                -0.5f; -0.5f; +0.5f;        0.0f; 0.0f;          0.0f;  0.0f; +1.0f; // bottom-left

                // left face
                -0.5f; +0.5f; +0.5f;        1.0f; 0.0f;         -1.0f;  0.0f;  0.0f; // top-right
                -0.5f; +0.5f; -0.5f;        1.0f; 1.0f;         -1.0f;  0.0f;  0.0f; // top-left
                -0.5f; -0.5f; -0.5f;        0.0f; 1.0f;         -1.0f;  0.0f;  0.0f; // bottom-left
                -0.5f; -0.5f; -0.5f;        0.0f; 1.0f;         -1.0f;  0.0f;  0.0f; // bottom-left
                -0.5f; -0.5f; +0.5f;        0.0f; 0.0f;         -1.0f;  0.0f;  0.0f; // bottom-right
                -0.5f; +0.5f; +0.5f;        1.0f; 0.0f;         -1.0f;  0.0f;  0.0f; // top-right

                // right face
                +0.5f; +0.5f; +0.5f;        1.0f; 0.0f;         +1.0f;  0.0f;  0.0f; // top-left
                +0.5f; -0.5f; -0.5f;        0.0f; 1.0f;         +1.0f;  0.0f;  0.0f; // bottom-right
                +0.5f; +0.5f; -0.5f;        1.0f; 1.0f;         +1.0f;  0.0f;  0.0f; // top-right         
                +0.5f; -0.5f; -0.5f;        0.0f; 1.0f;         +1.0f;  0.0f;  0.0f; // bottom-right
                +0.5f; +0.5f; +0.5f;        1.0f; 0.0f;         +1.0f;  0.0f;  0.0f; // top-left
                +0.5f; -0.5f; +0.5f;        0.0f; 0.0f;         +1.0f;  0.0f;  0.0f; // bottom-left     

                // bottom face
                -0.5f; -0.5f; -0.5f;        0.0f; 1.0f;          0.0f; -1.0f;  0.0f; // top-right
                +0.5f; -0.5f; -0.5f;        1.0f; 1.0f;          0.0f; -1.0f;  0.0f; // top-left
                +0.5f; -0.5f; +0.5f;        1.0f; 0.0f;          0.0f; -1.0f;  0.0f; // bottom-left
                +0.5f; -0.5f; +0.5f;        1.0f; 0.0f;          0.0f; -1.0f;  0.0f; // bottom-left
                -0.5f; -0.5f; +0.5f;        0.0f; 0.0f;          0.0f; -1.0f;  0.0f; // bottom-right
                -0.5f; -0.5f; -0.5f;        0.0f; 1.0f;          0.0f; -1.0f;  0.0f; // top-right

                // top face
                -0.5f; +0.5f; -0.5f;        0.0f; 1.0f;          0.0f; +1.0f;  0.0f; // top-left
                +0.5f; +0.5f ;+0.5f;        1.0f; 0.0f;          0.0f; +1.0f;  0.0f; // bottom-right
                +0.5f; +0.5f; -0.5f;        1.0f; 1.0f;          0.0f; +1.0f;  0.0f; // top-right     
                +0.5f; +0.5f; +0.5f;        1.0f; 0.0f;          0.0f; +1.0f;  0.0f; // bottom-right
                -0.5f; +0.5f; -0.5f;        0.0f; 1.0f;          0.0f; +1.0f;  0.0f; // top-left
                -0.5f; +0.5f; +0.5f;        0.0f; 0.0f;          0.0f; +1.0f;  0.0f  // bottom-left     
            |]

        // make index data trivially
        let indexData = Array.init 36 id

        // make bounds trivially
        let bounds = box3 (v3Dup -0.5f) v3One

        // fin
        (vertexData, indexData, bounds)

    /// Create physically-based geometry from a mesh.
    let CreatePhysicallyBasedGeometry (renderable, vertexData : single Memory, indexData : int Memory, bounds) =

        // make buffers
        let (vertices, indices, vertexBuffer, modelBuffer, texCoordsOffsetBuffer, albedoBuffer, materialBuffer, heightBuffer, invertRoughnessBuffer, indexBuffer, vao) =

            // make renderable
            if renderable then

                // initialize vao
                let vao = Gl.GenVertexArray ()
                Gl.BindVertexArray vao
                Hl.Assert ()

                // create vertex buffer
                let vertexBuffer = Gl.GenBuffer ()
                let texCoordsOffset =   (3 (*position*)) * sizeof<single>
                let normalOffset =      (3 (*position*) + 2 (*tex coords*)) * sizeof<single>
                let vertexSize =        (3 (*position*) + 2 (*tex coords*) + 3 (*normal*)) * sizeof<single>
                Gl.BindBuffer (BufferTarget.ArrayBuffer, vertexBuffer)
                use vertexDataHnd = vertexData.Pin () in
                    let vertexDataNInt = vertexDataHnd.Pointer |> NativePtr.ofVoidPtr<single> |> NativePtr.toNativeInt
                    Gl.BufferData (BufferTarget.ArrayBuffer, uint (vertexData.Length * sizeof<single>), vertexDataNInt, BufferUsage.StaticDraw)
                Gl.EnableVertexAttribArray 0u
                Gl.VertexAttribPointer (0u, 3, VertexAttribType.Float, false, vertexSize, nativeint 0)
                Gl.EnableVertexAttribArray 1u
                Gl.VertexAttribPointer (1u, 2, VertexAttribType.Float, false, vertexSize, nativeint texCoordsOffset)
                Gl.EnableVertexAttribArray 2u
                Gl.VertexAttribPointer (2u, 3, VertexAttribType.Float, false, vertexSize, nativeint normalOffset)
                Hl.Assert ()

                // create model buffer
                let modelBuffer = Gl.GenBuffer ()
                Gl.BindBuffer (BufferTarget.ArrayBuffer, modelBuffer)
                let modelDataPtr = GCHandle.Alloc (m4Identity.ToArray (), GCHandleType.Pinned)
                try Gl.BufferData (BufferTarget.ArrayBuffer, uint (16 * sizeof<single>), modelDataPtr.AddrOfPinnedObject (), BufferUsage.StreamDraw)
                finally modelDataPtr.Free ()
                Gl.EnableVertexAttribArray 3u
                Gl.VertexAttribPointer (3u, 4, VertexAttribType.Float, false, 16 * sizeof<single>, nativeint 0)
                Gl.VertexAttribDivisor (3u, 1u)
                Gl.EnableVertexAttribArray 4u
                Gl.VertexAttribPointer (4u, 4, VertexAttribType.Float, false, 16 * sizeof<single>, nativeint (4 * sizeof<single>))
                Gl.VertexAttribDivisor (4u, 1u)
                Gl.EnableVertexAttribArray 5u
                Gl.VertexAttribPointer (5u, 4, VertexAttribType.Float, false, 16 * sizeof<single>, nativeint (8 * sizeof<single>))
                Gl.VertexAttribDivisor (5u, 1u)
                Gl.EnableVertexAttribArray 6u
                Gl.VertexAttribPointer (6u, 4, VertexAttribType.Float, false, 16 * sizeof<single>, nativeint (12 * sizeof<single>))
                Gl.VertexAttribDivisor (6u, 1u)
                Hl.Assert ()

                // create texCoordsOffset buffer
                let texCoordsOffsetBuffer = Gl.GenBuffer ()
                Gl.BindBuffer (BufferTarget.ArrayBuffer, texCoordsOffsetBuffer)
                let texCoordsOffsetDataPtr = GCHandle.Alloc ([|0.0f; 0.0f; 0.0f; 0.0f|], GCHandleType.Pinned)
                try Gl.BufferData (BufferTarget.ArrayBuffer, uint (4 * sizeof<single>), texCoordsOffsetDataPtr.AddrOfPinnedObject (), BufferUsage.StreamDraw)
                finally texCoordsOffsetDataPtr.Free ()
                Gl.EnableVertexAttribArray 7u
                Gl.VertexAttribPointer (7u, 4, VertexAttribType.Float, false, 4 * sizeof<single>, nativeint 0)
                Gl.VertexAttribDivisor (7u, 1u)
                Hl.Assert ()

                // create albedo buffer
                let albedoBuffer = Gl.GenBuffer ()
                Gl.BindBuffer (BufferTarget.ArrayBuffer, albedoBuffer)
                let albedoDataPtr = GCHandle.Alloc ([|1.0f; 1.0f; 1.0f; 1.0f|], GCHandleType.Pinned)
                try Gl.BufferData (BufferTarget.ArrayBuffer, uint (4 * sizeof<single>), albedoDataPtr.AddrOfPinnedObject (), BufferUsage.StreamDraw)
                finally albedoDataPtr.Free ()
                Gl.EnableVertexAttribArray 8u
                Gl.VertexAttribPointer (8u, 4, VertexAttribType.Float, false, 4 * sizeof<single>, nativeint 0)
                Gl.VertexAttribDivisor (8u, 1u)
                Hl.Assert ()

                // create material buffer (used for metallic, roughness, ambient occlusion, and emission in that order)
                let materialBuffer = Gl.GenBuffer ()
                Gl.BindBuffer (BufferTarget.ArrayBuffer, materialBuffer)
                let materialDataPtr = GCHandle.Alloc ([|1.0f; 1.0f; 1.0f; 1.0f|], GCHandleType.Pinned)
                try Gl.BufferData (BufferTarget.ArrayBuffer, uint (4 * sizeof<single>), materialDataPtr.AddrOfPinnedObject (), BufferUsage.StreamDraw)
                finally materialDataPtr.Free ()
                Gl.EnableVertexAttribArray 9u
                Gl.VertexAttribPointer (9u, 4, VertexAttribType.Float, false, 4 * sizeof<single>, nativeint 0)
                Gl.VertexAttribDivisor (9u, 1u)
                Hl.Assert ()

                // create height buffer
                let heightBuffer = Gl.GenBuffer ()
                Gl.BindBuffer (BufferTarget.ArrayBuffer, heightBuffer)
                let heightDataPtr = GCHandle.Alloc ([|1.0f|], GCHandleType.Pinned)
                try Gl.BufferData (BufferTarget.ArrayBuffer, uint (sizeof<single>), heightDataPtr.AddrOfPinnedObject (), BufferUsage.StreamDraw)
                finally heightDataPtr.Free ()
                Gl.EnableVertexAttribArray 10u
                Gl.VertexAttribPointer (10u, 1, VertexAttribType.Float, false, sizeof<single>, nativeint 0)
                Gl.VertexAttribDivisor (10u, 1u)
                Hl.Assert ()

                // create invert roughness buffer
                let invertRoughnessBuffer = Gl.GenBuffer ()
                Gl.BindBuffer (BufferTarget.ArrayBuffer, invertRoughnessBuffer)
                let invertRoughnessDataPtr = GCHandle.Alloc ([|0|], GCHandleType.Pinned)
                try Gl.BufferData (BufferTarget.ArrayBuffer, uint (sizeof<int>), invertRoughnessDataPtr.AddrOfPinnedObject (), BufferUsage.StreamDraw)
                finally invertRoughnessDataPtr.Free ()
                Gl.EnableVertexAttribArray 11u
                Gl.VertexAttribIPointer (11u, 1, VertexAttribType.Int, sizeof<int>, nativeint 0)
                Gl.VertexAttribDivisor (11u, 1u)
                Hl.Assert ()

                // create index buffer
                let indexBuffer = Gl.GenBuffer ()
                Gl.BindBuffer (BufferTarget.ElementArrayBuffer, indexBuffer)
                let indexDataSize = uint (indexData.Length * sizeof<uint>)
                use indexDataHnd = indexData.Pin () in
                    let indexDataNInt = indexDataHnd.Pointer |> NativePtr.ofVoidPtr<uint> |> NativePtr.toNativeInt
                    Gl.BufferData (BufferTarget.ElementArrayBuffer, indexDataSize, indexDataNInt, BufferUsage.StaticDraw)
                Hl.Assert ()

                // finalize vao
                Gl.BindVertexArray 0u
                Hl.Assert ()

                // create indices
                let indices = indexData.ToArray ()

                // fin
                ([||], indices, vertexBuffer, modelBuffer, texCoordsOffsetBuffer, albedoBuffer, materialBuffer, heightBuffer, invertRoughnessBuffer, indexBuffer, vao)

            // fake buffers
            else

                // compute vertices
                let vertices = Array.zeroCreate (vertexData.Length / 8)
                let vertexData = vertexData.Span
                for i in 0 .. dec vertices.Length do
                    let j = i * 8
                    let vertex = v3 vertexData.[j] vertexData.[j+1] vertexData.[j+2]
                    vertices.[i] <- vertex

                // create indices
                let indices = indexData.ToArray ()

                // fin
                (vertices, indices, 0u, 0u, 0u, 0u, 0u, 0u, 0u, 0u, 0u)

        // make physically-based geometry
        let geometry =
            { Bounds = bounds
              PrimitiveType = PrimitiveType.Triangles
              ElementCount = indexData.Length
              Vertices = vertices
              Indices = indices
              VertexBuffer = vertexBuffer
              ModelBuffer = modelBuffer
              TexCoordsOffsetBuffer = texCoordsOffsetBuffer
              AlbedoBuffer = albedoBuffer
              MaterialBuffer = materialBuffer
              HeightBuffer = heightBuffer
              InvertRoughnessBuffer = invertRoughnessBuffer
              IndexBuffer = indexBuffer
              PhysicallyBasedVao = vao }

        // fin
        geometry

    /// Attempt to create physically-based geometry from an assimp mesh.
    let TryCreatePhysicallyBasedGeometry (renderable, mesh : Assimp.Mesh) =
        let meshOpt =
#if DEBUG_RENDERING_CUBE
            ignore<Assimp.Mesh> mesh
            Right (CreatePhysicallyBasedCubeMesh ())
#else
            TryCreatePhysicallyBasedMesh mesh
#endif
        match meshOpt with
        | Right (vertexData, indexData, bounds) -> Right (CreatePhysicallyBasedGeometry (renderable, vertexData.AsMemory (), indexData.AsMemory (), bounds))
        | Left error -> Left error

    /// Create physically-based quad.
    let CreatePhysicallyBasedQuad renderable =
        let (vertexData, indexData, bounds) = CreatePhysicallyBasedQuadMesh ()
        CreatePhysicallyBasedGeometry (renderable, vertexData.AsMemory (), indexData.AsMemory (), bounds)

    /// Create physically-based billboard.
    let CreatePhysicallyBasedBillboard renderable =
        let (vertexData, indexData, bounds) = CreatePhysicallyBasedBillboardMesh ()
        CreatePhysicallyBasedGeometry (renderable, vertexData.AsMemory (), indexData.AsMemory (), bounds)

    /// Create physically-based cube.
    let CreatePhysicallyBasedCube renderable =
        let (vertexData, indexData, bounds) = CreatePhysicallyBasedCubeMesh ()
        CreatePhysicallyBasedGeometry (renderable, vertexData.AsMemory (), indexData.AsMemory (), bounds)

    /// Create physically-based material from an assimp mesh. falling back on default in case of missing textures.
    /// Uses file name-based inferences to look for non-albedo files as well as determining if roughness should be
    /// inverted to smoothness (such as when a model is imported from an fbx exported from a Unity scene).
    let CreatePhysicallyBasedMaterial (renderable, dirPath, defaultMaterial, minFilterOpt, magFilterOpt, textureMemo, material : Assimp.Material) =

        // attempt to load albedo info
        let albedo =
            if material.HasColorDiffuse
            then color material.ColorDiffuse.R material.ColorDiffuse.G material.ColorDiffuse.B material.ColorDiffuse.A
            else Constants.Render.AlbedoDefault
        let mutable (_, albedoTextureSlot) = material.GetMaterialTexture (Assimp.TextureType.Diffuse, 0)
        if isNull albedoTextureSlot.FilePath then albedoTextureSlot.FilePath <- "" // ensure not null
        let (albedoMetadata, albedoTexture) =
            if renderable then
                match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + albedoTextureSlot.FilePath, textureMemo) with
                | Right (textureMetadata, texture) -> (textureMetadata, texture)
                | Left _ -> (defaultMaterial.AlbedoMetadata, defaultMaterial.AlbedoTexture)
            else (defaultMaterial.AlbedoMetadata, defaultMaterial.AlbedoTexture)

        // infer possible alternate texture names
        let albedoTextureDirName =      match Path.GetDirectoryName albedoTextureSlot.FilePath with null -> "" | dirName -> dirName
        let albedoTextureFileName =     Path.GetFileName albedoTextureSlot.FilePath
        let has_bc =                    albedoTextureFileName.Contains "_bc"
        let has_d =                     albedoTextureFileName.Contains "_d"
        let hasBaseColor =              albedoTextureFileName.Contains "BaseColor"
        let hasAlbedo =                 albedoTextureFileName.Contains "Albedo"
        let mTextureFilePath =          if has_bc         then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("_bc", "_m")                 elif has_d      then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("_d", "_m") else ""
        let m_gTextureFilePath =        if has_bc         then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("_bc", "_m_g")               elif has_d      then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("_d", "_m_g") else ""
        let m_ao_gTextureFilePath =     if has_bc         then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("_bc", "_m_ao_g")            elif has_d      then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("_d", "_m_ao_g") else ""
        let gTextureFilePath =          if has_bc         then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("_bc", "_g")                 elif has_d      then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("_d", "_g") else ""
        let sTextureFilePath =          if has_bc         then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("_bc", "_s")                 elif has_d      then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("_d", "_s") else ""
        let aoTextureFilePath =         if has_bc         then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("_bc", "_ao")                elif has_d      then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("_d", "_ao") else ""
        let eTextureFilePath =          if has_bc         then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("_bc", "_e")                 elif has_d      then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("_d", "_e") else ""
        let nTextureFilePath =          if has_bc         then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("_bc", "_n")                 elif has_d      then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("_d", "_n") else ""
        let hTextureFilePath =          if has_bc         then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("_bc", "_h")                 elif has_d      then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("_d", "_h") else ""
        let metallicTextureFilePath =   if hasBaseColor   then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("BaseColor", "Metallic")     elif hasAlbedo  then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("Albedo", "Metallic") else ""
        let roughnessTextureFilePath =  if hasBaseColor   then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("BaseColor", "Roughness")    elif hasAlbedo  then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("Albedo", "Roughness") else ""
        let aoTextureFilePath' =        if hasBaseColor   then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("BaseColor", "AO")           elif hasAlbedo  then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("Albedo", "AO") else ""
        let normalTextureFilePath =     if hasBaseColor   then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("BaseColor", "Normal")       elif hasAlbedo  then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("Albedo", "Normal") else ""
        let emissionTextureFilePath =   if hasBaseColor   then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("BaseColor", "Emission")     elif hasAlbedo  then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("Albedo", "Emission") else ""
        let heightTextureFilePath =     if hasBaseColor   then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("BaseColor", "Height")       elif hasAlbedo  then albedoTextureDirName + "/" + albedoTextureFileName.Replace ("Albedo", "Height") else ""

        // attempt to load metallic info
        let metallic = Constants.Render.MetallicDefault
        let mutable (_, metallicTextureSlot) = material.GetMaterialTexture (Assimp.TextureType.Metalness, 0)
        if isNull metallicTextureSlot.FilePath then metallicTextureSlot.FilePath <- "" // ensure not null
        let metallicTexture =
            if renderable then
                match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + metallicTextureSlot.FilePath, textureMemo) with
                | Right (_, texture) -> texture
                | Left _ ->
                    match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + mTextureFilePath, textureMemo) with
                    | Right (_, texture) -> texture
                    | Left _ ->
                        match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + m_gTextureFilePath, textureMemo) with
                        | Right (_, texture) -> texture
                        | Left _ ->
                            match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + m_ao_gTextureFilePath, textureMemo) with
                            | Right (_, texture) -> texture
                            | Left _ ->
                                match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + metallicTextureFilePath, textureMemo) with
                                | Right (_, texture) -> texture
                                | Left _ -> defaultMaterial.MetallicTexture
            else defaultMaterial.MetallicTexture

        // attempt to load roughness info
        let invertRoughness = has_bc || has_d // NOTE: assume texture from Unity export if it has this weird naming.
        let roughness = Constants.Render.RoughnessDefault
        let mutable (_, roughnessTextureSlot) = material.GetMaterialTexture (Assimp.TextureType.Roughness, 0)
        if isNull roughnessTextureSlot.FilePath then roughnessTextureSlot.FilePath <- "" // ensure not null
        let roughnessTexture =
            if renderable then
                match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + roughnessTextureSlot.FilePath, textureMemo) with
                | Right (_, texture) -> texture
                | Left _ ->
                    match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + gTextureFilePath, textureMemo) with
                    | Right (_, texture) -> texture
                    | Left _ ->
                        match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + sTextureFilePath, textureMemo) with
                        | Right (_, texture) -> texture
                        | Left _ ->
                            match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + m_gTextureFilePath, textureMemo) with
                            | Right (_, texture) -> texture
                            | Left _ ->
                                match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + m_ao_gTextureFilePath, textureMemo) with
                                | Right (_, texture) -> texture
                                | Left _ ->
                                    match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + roughnessTextureFilePath, textureMemo) with
                                    | Right (_, texture) -> texture
                                    | Left _ -> defaultMaterial.RoughnessTexture
            else defaultMaterial.RoughnessTexture

        // attempt to load ambient occlusion info
        let ambientOcclusion = Constants.Render.AmbientOcclusionDefault
        let mutable (_, ambientOcclusionTextureSlot) = material.GetMaterialTexture (Assimp.TextureType.Ambient, 0)
        if isNull ambientOcclusionTextureSlot.FilePath then ambientOcclusionTextureSlot.FilePath <- "" // ensure not null
        let ambientOcclusionTexture =
            if renderable then
                match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + ambientOcclusionTextureSlot.FilePath, textureMemo) with
                | Right (_, texture) -> texture
                | Left _ ->
                    match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + aoTextureFilePath, textureMemo) with
                    | Right (_, texture) -> texture
                    | Left _ ->
                        match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + m_ao_gTextureFilePath, textureMemo) with
                        | Right (_, texture) -> texture
                        | Left _ ->
                            match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + aoTextureFilePath', textureMemo) with
                            | Right (_, texture) -> texture
                            | Left _ -> defaultMaterial.AmbientOcclusionTexture
            else defaultMaterial.AmbientOcclusionTexture

        // attempt to load emission info
        let emission = Constants.Render.EmissionDefault
        let mutable (_, emissionTextureSlot) = material.GetMaterialTexture (Assimp.TextureType.Emissive, 0)
        if isNull emissionTextureSlot.FilePath then emissionTextureSlot.FilePath <- "" // ensure not null
        let emissionTexture =
            if renderable then
                match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + emissionTextureSlot.FilePath, textureMemo) with
                | Right (_, texture) -> texture
                | Left _ ->
                    match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + eTextureFilePath, textureMemo) with
                    | Right (_, texture) -> texture
                    | Left _ ->
                        match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + emissionTextureFilePath, textureMemo) with
                        | Right (_, texture) -> texture
                        | Left _ -> defaultMaterial.EmissionTexture
            else defaultMaterial.EmissionTexture

        // attempt to load normal info
        let mutable (_, normalTextureSlot) = material.GetMaterialTexture (Assimp.TextureType.Normals, 0)
        if isNull normalTextureSlot.FilePath then normalTextureSlot.FilePath <- "" // ensure not null
        let normalTexture =
            if renderable then
                match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + normalTextureSlot.FilePath, textureMemo) with
                | Right (_, texture) -> texture
                | Left _ ->
                    match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + nTextureFilePath, textureMemo) with
                    | Right (_, texture) -> texture
                    | Left _ ->
                        match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + normalTextureFilePath, textureMemo) with
                        | Right (_, texture) -> texture
                        | Left _ -> defaultMaterial.NormalTexture
            else defaultMaterial.NormalTexture

        // attempt to load height info
        let height = Constants.Render.HeightDefault
        let mutable (_, heightTextureSlot) = material.GetMaterialTexture (Assimp.TextureType.Height, 0)
        if isNull heightTextureSlot.FilePath then heightTextureSlot.FilePath <- "" // ensure not null
        let heightTexture =
            if renderable then
                match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + heightTextureSlot.FilePath, textureMemo) with
                | Right (_, texture) -> texture
                | Left _ ->
                    match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + hTextureFilePath, textureMemo) with
                    | Right (_, texture) -> texture
                    | Left _ ->
                        match Texture.TryCreateTextureMemoizedFiltered (dirPath + "/" + heightTextureFilePath, textureMemo) with
                        | Right (_, texture) -> texture
                        | Left _ -> defaultMaterial.HeightTexture
            else defaultMaterial.HeightTexture

        // make properties
        let properties =
            { Albedo = color albedo.R albedo.G albedo.B albedo.A
              Metallic = metallic
              Roughness = roughness
              AmbientOcclusion = ambientOcclusion
              Emission = emission
              Height = height
              InvertRoughness = invertRoughness }

        // fin
        { MaterialProperties = properties
          AlbedoMetadata = albedoMetadata
          AlbedoTexture = albedoTexture
          MetallicTexture = metallicTexture
          RoughnessTexture = roughnessTexture
          AmbientOcclusionTexture = ambientOcclusionTexture
          EmissionTexture = emissionTexture
          NormalTexture = normalTexture
          HeightTexture = heightTexture
          TextureMinFilterOpt = minFilterOpt
          TextureMagFilterOpt = magFilterOpt
          TwoSided = material.IsTwoSided }

    /// Create a physically-based surface.
    let CreatePhysicallyBasedSurface (surfaceNames, surfaceMatrix, surfaceBounds, physicallyBasedMaterial, physicallyBasedGeometry) =
        PhysicallyBasedSurface.make surfaceNames surfaceMatrix surfaceBounds physicallyBasedMaterial physicallyBasedGeometry

    /// Attempt to create physically-based material from an assimp scene.
    let TryCreatePhysicallyBasedMaterials (renderable, dirPath, defaultMaterial, textureMemo, scene : Assimp.Scene) =
        let mutable errorOpt = None
        let materials = Array.zeroCreate scene.Materials.Count
        for i in 0 .. dec scene.Materials.Count do
            if Option.isNone errorOpt then
                let material = CreatePhysicallyBasedMaterial (renderable, dirPath, defaultMaterial, None, None, textureMemo, scene.Materials.[i])
                materials.[i] <- material
        match errorOpt with
        | Some error -> Left error
        | None -> Right materials

    /// Attempt to create physically-based geometries from an assimp scene.
    let TryCreatePhysicallyBasedGeometries (renderable, filePath, scene : Assimp.Scene) =
        let mutable errorOpt = None
        let geometries = SegmentedList.make ()
        for mesh in scene.Meshes do
            if Option.isNone errorOpt then
                match TryCreatePhysicallyBasedGeometry (renderable, mesh) with
                | Right geometry -> SegmentedList.add geometry geometries
                | Left error -> errorOpt <- Some ("Could not load geometry for mesh in file name '" + filePath + "' due to: " + error)
        match errorOpt with
        | Some error -> Left error
        | None -> Right geometries

    /// Attempt to create physically-based model from a model file with assimp.
    let TryCreatePhysicallyBasedStaticModel (renderable, filePath, defaultMaterial, textureMemo, assimp : Assimp.AssimpContext) =

        // attempt to import from assimp scene
        try let scene = assimp.ImportFile (filePath, Constants.Assimp.PostProcessSteps)
            let dirPath = Path.GetDirectoryName filePath
            match TryCreatePhysicallyBasedMaterials (renderable, dirPath, defaultMaterial, textureMemo, scene) with
            | Right materials ->
                match TryCreatePhysicallyBasedGeometries (renderable, filePath, scene) with
                | Right geometries ->

                    // collect light nodes
                    let lightNodes =
                        seq {
                            for i in 0 .. dec scene.LightCount do
                                let light = scene.Lights.[i]
                                let node = scene.RootNode.FindNode light.Name
                                yield (light, node) } |>
                        Seq.toArray

                    // construct bounds and hierarchy
                    // TODO: sanitize incoming names. Corrupted or incompatible names cause subtle hierarchy bugs.
                    let lights = SegmentedList.make ()
                    let surfaces = SegmentedList.make ()
                    let mutable bounds = box3Zero
                    let hierarchy =
                        scene.RootNode.Map ([||], m4Identity, fun node names surfaceMatrix ->
                            seq {

                                // collect node
                                yield PhysicallyBasedNode names

                                // collect light
                                // NOTE: this is an n^2 algorithm to deal with nodes having no light information
                                for i in 0 .. dec lightNodes.Length do
                                    let (light, lightNode) = lightNodes.[i]
                                    if lightNode = node then
                                        let names = Array.append names [|"Light" + if i > 0 then string i else ""|]
                                        let lightMatrix = node.ImportMatrix node.TransformWorld
                                        let color = color light.ColorDiffuse.R light.ColorDiffuse.G light.ColorDiffuse.B 1.0f
                                        match light.LightType with
                                        | _ -> // just use point light for all lights right now
                                            let lightType =
                                                match light.LightType with
                                                | Assimp.LightSourceType.Point -> SpotLight (light.AngleInnerCone, light.AngleOuterCone)
                                                | _ -> PointLight
                                            let physicallyBasedLight =
                                                { LightNames = names
                                                  LightMatrixIsIdentity = lightMatrix.IsIdentity
                                                  LightMatrix = lightMatrix
                                                  LightColor = color
                                                  LightBrightness = Constants.Render.BrightnessDefault // TODO: figure out if we can populate this properly.
                                                  LightAttenuationLinear = if light.AttenuationLinear > 0.0f then light.AttenuationLinear else Constants.Render.AttenuationLinearDefault
                                                  LightAttenuationQuadratic = if light.AttenuationQuadratic > 0.0f then light.AttenuationQuadratic else Constants.Render.AttenuationQuadraticDefault
                                                  PhysicallyBasedLightType = lightType }
                                            SegmentedList.add physicallyBasedLight lights
                                            yield PhysicallyBasedLight physicallyBasedLight

                                // collect surfaces
                                for i in 0 .. dec node.MeshIndices.Count do
                                    let meshIndex = node.MeshIndices.[i]
                                    let names = Array.append names [|"Geometry" + if i > 0 then string i else ""|]
                                    let materialIndex = scene.Meshes.[meshIndex].MaterialIndex
                                    let material = materials.[materialIndex]
                                    let geometry = geometries.[meshIndex]
                                    let surface = PhysicallyBasedSurface.make names surfaceMatrix geometry.Bounds material geometry
                                    bounds <- bounds.Combine (geometry.Bounds.Transform surfaceMatrix)
                                    SegmentedList.add surface surfaces
                                    yield PhysicallyBasedSurface surface } |>

                            Seq.toArray |>
                            TreeNode)

                    // fin
                    Right
                        { Bounds = bounds
                          Lights = Array.ofSeq lights
                          Surfaces = Array.ofSeq surfaces
                          PhysicallyBasedStaticHierarchy = hierarchy }

                // error
                | Left error -> Left error
            | Left error -> Left ("Could not load materials for static model in file name '" + filePath + "' due to: " + error)
        with exn -> Left ("Could not load static model '" + filePath + "' due to: " + scstring exn)

    /// Create a physically-based shader.
    let CreatePhysicallyBasedShader (shaderFilePath : string) =

        // create shader
        let shader = Shader.CreateShaderFromFilePath shaderFilePath

        // retrieve uniforms
        let viewUniform = Gl.GetUniformLocation (shader, "view")
        let projectionUniform = Gl.GetUniformLocation (shader, "projection")
        let eyeCenterUniform = Gl.GetUniformLocation (shader, "eyeCenter")
        let lightAmbientColorUniform = Gl.GetUniformLocation (shader, "lightAmbientColor")
        let lightAmbientBrightnessUniform = Gl.GetUniformLocation (shader, "lightAmbientBrightness")
        let albedoTextureUniform = Gl.GetUniformLocation (shader, "albedoTexture")
        let metallicTextureUniform = Gl.GetUniformLocation (shader, "metallicTexture")
        let roughnessTextureUniform = Gl.GetUniformLocation (shader, "roughnessTexture")
        let ambientOcclusionTextureUniform = Gl.GetUniformLocation (shader, "ambientOcclusionTexture")
        let emissionTextureUniform = Gl.GetUniformLocation (shader, "emissionTexture")
        let normalTextureUniform = Gl.GetUniformLocation (shader, "normalTexture")
        let heightTextureUniform = Gl.GetUniformLocation (shader, "heightTexture")
        let irradianceMapUniform = Gl.GetUniformLocation (shader, "irradianceMap")
        let environmentFilterMapUniform = Gl.GetUniformLocation (shader, "environmentFilterMap")
        let brdfTextureUniform = Gl.GetUniformLocation (shader, "brdfTexture")
        let lightOriginsUniform = Gl.GetUniformLocation (shader, "lightOrigins")
        let lightDirectionsUniform = Gl.GetUniformLocation (shader, "lightDirections")
        let lightColorsUniform = Gl.GetUniformLocation (shader, "lightColors")
        let lightBrightnessesUniform = Gl.GetUniformLocation (shader, "lightBrightnesses")
        let lightAttenuationLinearsUniform = Gl.GetUniformLocation (shader, "lightAttenuationLinears")
        let lightAttenuationQuadraticsUniform = Gl.GetUniformLocation (shader, "lightAttenuationQuadratics")
        let lightDirectionalsUniform = Gl.GetUniformLocation (shader, "lightDirectionals")
        let lightConeInnersUniform = Gl.GetUniformLocation (shader, "lightConeInners")
        let lightConeOutersUniform = Gl.GetUniformLocation (shader, "lightConeOuters")

        // make shader record
        { ViewUniform = viewUniform
          ProjectionUniform = projectionUniform
          EyeCenterUniform = eyeCenterUniform
          LightAmbientColorUniform = lightAmbientColorUniform
          LightAmbientBrightnessUniform = lightAmbientBrightnessUniform
          AlbedoTextureUniform = albedoTextureUniform
          MetallicTextureUniform = metallicTextureUniform
          RoughnessTextureUniform = roughnessTextureUniform
          AmbientOcclusionTextureUniform = ambientOcclusionTextureUniform
          EmissionTextureUniform = emissionTextureUniform
          NormalTextureUniform = normalTextureUniform
          HeightTextureUniform = heightTextureUniform
          IrradianceMapUniform = irradianceMapUniform
          EnvironmentFilterMapUniform = environmentFilterMapUniform
          BrdfTextureUniform = brdfTextureUniform
          LightOriginsUniform = lightOriginsUniform
          LightDirectionsUniform = lightDirectionsUniform
          LightColorsUniform = lightColorsUniform
          LightBrightnessesUniform = lightBrightnessesUniform
          LightAttenuationLinearsUniform = lightAttenuationLinearsUniform
          LightAttenuationQuadraticsUniform = lightAttenuationQuadraticsUniform
          LightDirectionalsUniform = lightDirectionalsUniform
          LightConeInnersUniform = lightConeInnersUniform
          LightConeOutersUniform = lightConeOutersUniform
          PhysicallyBasedShader = shader }

    /// Create a physically-based shader for the second step of deferred rendering.
    let CreatePhysicallyBasedDeferred2Shader (shaderFilePath : string) =

        // create shader
        let shader = Shader.CreateShaderFromFilePath shaderFilePath

        // retrieve uniforms
        let viewUniform = Gl.GetUniformLocation (shader, "view")
        let projectionUniform = Gl.GetUniformLocation (shader, "projection")
        let eyeCenterUniform = Gl.GetUniformLocation (shader, "eyeCenter")
        let lightAmbientColorUniform = Gl.GetUniformLocation (shader, "lightAmbientColor")
        let lightAmbientBrightnessUniform = Gl.GetUniformLocation (shader, "lightAmbientBrightness")
        let positionTextureUniform = Gl.GetUniformLocation (shader, "positionTexture")
        let albedoTextureUniform = Gl.GetUniformLocation (shader, "albedoTexture")
        let materialTextureUniform = Gl.GetUniformLocation (shader, "materialTexture")
        let normalAndDepthTextureUniform = Gl.GetUniformLocation (shader, "normalAndDepthTexture")
        let irradianceMapUniform = Gl.GetUniformLocation (shader, "irradianceMap")
        let environmentFilterMapUniform = Gl.GetUniformLocation (shader, "environmentFilterMap")
        let brdfTextureUniform = Gl.GetUniformLocation (shader, "brdfTexture")
        let lightOriginsUniform = Gl.GetUniformLocation (shader, "lightOrigins")
        let lightDirectionsUniform = Gl.GetUniformLocation (shader, "lightDirections")
        let lightColorsUniform = Gl.GetUniformLocation (shader, "lightColors")
        let lightBrightnessesUniform = Gl.GetUniformLocation (shader, "lightBrightnesses")
        let lightAttenuationLinearsUniform = Gl.GetUniformLocation (shader, "lightAttenuationLinears")
        let lightAttenuationQuadraticsUniform = Gl.GetUniformLocation (shader, "lightAttenuationQuadratics")
        let lightDirectionalsUniform = Gl.GetUniformLocation (shader, "lightDirectionals")
        let lightConeInnersUniform = Gl.GetUniformLocation (shader, "lightConeInners")
        let lightConeOutersUniform = Gl.GetUniformLocation (shader, "lightConeOuters")

        // make shader record
        { ViewUniform = viewUniform
          ProjectionUniform = projectionUniform
          EyeCenterUniform = eyeCenterUniform
          LightAmbientColorUniform = lightAmbientColorUniform
          LightAmbientBrightnessUniform = lightAmbientBrightnessUniform
          PositionTextureUniform = positionTextureUniform
          AlbedoTextureUniform = albedoTextureUniform
          MaterialTextureUniform = materialTextureUniform
          NormalAndDepthTextureUniform = normalAndDepthTextureUniform
          IrradianceMapUniform = irradianceMapUniform
          EnvironmentFilterMapUniform = environmentFilterMapUniform
          BrdfTextureUniform = brdfTextureUniform
          LightOriginsUniform = lightOriginsUniform
          LightDirectionsUniform = lightDirectionsUniform
          LightColorsUniform = lightColorsUniform
          LightBrightnessesUniform = lightBrightnessesUniform
          LightAttenuationLinearsUniform = lightAttenuationLinearsUniform
          LightAttenuationQuadraticsUniform = lightAttenuationQuadraticsUniform
          LightDirectionalsUniform = lightDirectionalsUniform
          LightConeInnersUniform = lightConeInnersUniform
          LightConeOutersUniform = lightConeOutersUniform
          PhysicallyBasedDeferred2Shader = shader }

    /// Create the first and second shaders for physically-based deferred rendering.
    let CreatePhysicallyBasedDeferredShaders (shaderFilePath, shader2FilePath) =
        let shader = CreatePhysicallyBasedShader shaderFilePath // deferred shader 1 uses the same API as physically based shader
        let shader2 = CreatePhysicallyBasedDeferred2Shader shader2FilePath
        (shader, shader2)

    /// Draw a batch of physically-based surfaces.
    let DrawPhysicallyBasedSurfaces
        (view : single array,
         projection : single array,
         eyeCenter : Vector3,
         surfacesCount : int,
         modelsFields : single array,
         texCoordsOffsetsFields : single array,
         albedosFields : single array,
         materialsFields : single array,
         heightsFields : single array,
         invertRoughnessesFields : int array,
         blending,
         lightAmbientColor : single array,
         lightAmbientBrightness : single,
         irradianceMap : uint,
         environmentFilterMap : uint,
         brdfTexture : uint,
         lightOrigins : single array,
         lightDirections : single array,
         lightColors : single array,
         lightBrightnesses : single array,
         lightAttenuationLinears : single array,
         lightAttenuationQuadratics : single array,
         lightDirectionals : int array,
         lightConeInners : single array,
         lightConeOuters : single array,
         material : PhysicallyBasedMaterial,
         geometry : PhysicallyBasedGeometry,
         shader : PhysicallyBasedShader) =

        // setup state
        Gl.DepthFunc DepthFunction.Lequal
        Gl.Enable EnableCap.DepthTest
        if blending then
            Gl.BlendEquation BlendEquationMode.FuncAdd
            Gl.BlendFunc (BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha)
            Gl.Enable EnableCap.Blend
        if not material.TwoSided then Gl.Enable EnableCap.CullFace
        Hl.Assert ()

        // setup shader
        Gl.UseProgram shader.PhysicallyBasedShader
        Gl.UniformMatrix4 (shader.ViewUniform, false, view)
        Gl.UniformMatrix4 (shader.ProjectionUniform, false, projection)
        Gl.Uniform3 (shader.EyeCenterUniform, eyeCenter.X, eyeCenter.Y, eyeCenter.Z)
        Gl.Uniform3 (shader.LightAmbientColorUniform, lightAmbientColor)
        Gl.Uniform1 (shader.LightAmbientBrightnessUniform, lightAmbientBrightness)
        Gl.Uniform1 (shader.AlbedoTextureUniform, 0)
        Gl.Uniform1 (shader.MetallicTextureUniform, 1)
        Gl.Uniform1 (shader.RoughnessTextureUniform, 2)
        Gl.Uniform1 (shader.AmbientOcclusionTextureUniform, 3)
        Gl.Uniform1 (shader.EmissionTextureUniform, 4)
        Gl.Uniform1 (shader.NormalTextureUniform, 5)
        Gl.Uniform1 (shader.HeightTextureUniform, 6)
        Gl.Uniform1 (shader.IrradianceMapUniform, 7)
        Gl.Uniform1 (shader.EnvironmentFilterMapUniform, 8)
        Gl.Uniform1 (shader.BrdfTextureUniform, 9)
        Gl.Uniform3 (shader.LightOriginsUniform, lightOrigins)
        Gl.Uniform3 (shader.LightDirectionsUniform, lightDirections)
        Gl.Uniform3 (shader.LightColorsUniform, lightColors)
        Gl.Uniform1 (shader.LightBrightnessesUniform, lightBrightnesses)
        Gl.Uniform1 (shader.LightAttenuationLinearsUniform, lightAttenuationLinears)
        Gl.Uniform1 (shader.LightAttenuationQuadraticsUniform, lightAttenuationQuadratics)
        Gl.Uniform1 (shader.LightDirectionalsUniform, lightDirectionals)
        Gl.Uniform1 (shader.LightConeInnersUniform, lightConeInners)
        Gl.Uniform1 (shader.LightConeOutersUniform, lightConeOuters)
        Hl.Assert ()

        // setup textures
        Gl.ActiveTexture TextureUnit.Texture0
        Gl.BindTexture (TextureTarget.Texture2d, material.AlbedoTexture)
        Gl.ActiveTexture TextureUnit.Texture1
        Gl.BindTexture (TextureTarget.Texture2d, material.MetallicTexture)
        Gl.ActiveTexture TextureUnit.Texture2
        Gl.BindTexture (TextureTarget.Texture2d, material.RoughnessTexture)
        Gl.ActiveTexture TextureUnit.Texture3
        Gl.BindTexture (TextureTarget.Texture2d, material.AmbientOcclusionTexture)
        Gl.ActiveTexture TextureUnit.Texture4
        Gl.BindTexture (TextureTarget.Texture2d, material.EmissionTexture)
        Gl.ActiveTexture TextureUnit.Texture5
        Gl.BindTexture (TextureTarget.Texture2d, material.NormalTexture)
        Gl.ActiveTexture TextureUnit.Texture6
        Gl.BindTexture (TextureTarget.Texture2d, material.HeightTexture)
        Gl.ActiveTexture TextureUnit.Texture7
        Gl.BindTexture (TextureTarget.TextureCubeMap, irradianceMap)
        Gl.ActiveTexture TextureUnit.Texture8
        Gl.BindTexture (TextureTarget.TextureCubeMap, environmentFilterMap)
        Gl.ActiveTexture TextureUnit.Texture9
        Gl.BindTexture (TextureTarget.Texture2d, brdfTexture)
        Hl.Assert ()

        // setup texture filters
        for i in 0 .. dec 5 do
            Gl.ActiveTexture (LanguagePrimitives.EnumOfValue (int TextureUnit.Texture0 + i))
            match material.TextureMinFilterOpt with
            | Some minFilter -> Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, int minFilter)
            | None -> ()
            match material.TextureMagFilterOpt with
            | Some magFilter -> Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, int magFilter)
            | None -> ()
        Hl.Assert ()

        // update models buffer
        let modelsFieldsPtr = GCHandle.Alloc (modelsFields, GCHandleType.Pinned)
        try Gl.BindBuffer (BufferTarget.ArrayBuffer, geometry.ModelBuffer)
            Gl.BufferData (BufferTarget.ArrayBuffer, uint (surfacesCount * 16 * sizeof<single>), modelsFieldsPtr.AddrOfPinnedObject (), BufferUsage.StreamDraw)
        finally modelsFieldsPtr.Free ()
        Gl.BindBuffer (BufferTarget.ArrayBuffer, 0u)
        Hl.Assert ()

        // update texCoordsOffsets buffer
        let texCoordsOffsetsFieldsPtr = GCHandle.Alloc (texCoordsOffsetsFields, GCHandleType.Pinned)
        try Gl.BindBuffer (BufferTarget.ArrayBuffer, geometry.TexCoordsOffsetBuffer)
            Gl.BufferData (BufferTarget.ArrayBuffer, uint (surfacesCount * 4 * sizeof<single>), texCoordsOffsetsFieldsPtr.AddrOfPinnedObject (), BufferUsage.StreamDraw)
        finally texCoordsOffsetsFieldsPtr.Free ()
        Gl.BindBuffer (BufferTarget.ArrayBuffer, 0u)
        Hl.Assert ()

        // update albedos buffer
        let albedosFieldsPtr = GCHandle.Alloc (albedosFields, GCHandleType.Pinned)
        try Gl.BindBuffer (BufferTarget.ArrayBuffer, geometry.AlbedoBuffer)
            Gl.BufferData (BufferTarget.ArrayBuffer, uint (surfacesCount * 4 * sizeof<single>), albedosFieldsPtr.AddrOfPinnedObject (), BufferUsage.StreamDraw)
        finally albedosFieldsPtr.Free ()
        Gl.BindBuffer (BufferTarget.ArrayBuffer, 0u)
        Hl.Assert ()

        // update materials buffer
        let materialsFieldsPtr = GCHandle.Alloc (materialsFields, GCHandleType.Pinned)
        try Gl.BindBuffer (BufferTarget.ArrayBuffer, geometry.MaterialBuffer)
            Gl.BufferData (BufferTarget.ArrayBuffer, uint (surfacesCount * 4 * sizeof<single>), materialsFieldsPtr.AddrOfPinnedObject (), BufferUsage.StreamDraw)
        finally materialsFieldsPtr.Free ()
        Gl.BindBuffer (BufferTarget.ArrayBuffer, 0u)
        Hl.Assert ()

        // update heights buffer
        let heightsFieldsPtr = GCHandle.Alloc (heightsFields, GCHandleType.Pinned)
        try Gl.BindBuffer (BufferTarget.ArrayBuffer, geometry.HeightBuffer)
            Gl.BufferData (BufferTarget.ArrayBuffer, uint (surfacesCount * sizeof<single>), heightsFieldsPtr.AddrOfPinnedObject (), BufferUsage.StreamDraw)
        finally heightsFieldsPtr.Free ()
        Gl.BindBuffer (BufferTarget.ArrayBuffer, 0u)
        Hl.Assert ()

        // update invert roughnesses buffer
        let invertRoughnessesFieldsPtr = GCHandle.Alloc (invertRoughnessesFields, GCHandleType.Pinned)
        try Gl.BindBuffer (BufferTarget.ArrayBuffer, geometry.InvertRoughnessBuffer)
            Gl.BufferData (BufferTarget.ArrayBuffer, uint (surfacesCount * sizeof<int>), invertRoughnessesFieldsPtr.AddrOfPinnedObject (), BufferUsage.StreamDraw)
        finally invertRoughnessesFieldsPtr.Free ()
        Gl.BindBuffer (BufferTarget.ArrayBuffer, 0u)
        Hl.Assert ()

        // setup geometry
        Gl.BindVertexArray geometry.PhysicallyBasedVao
        Gl.BindBuffer (BufferTarget.ArrayBuffer, geometry.VertexBuffer)
        Gl.BindBuffer (BufferTarget.ElementArrayBuffer, geometry.IndexBuffer)
        Hl.Assert ()

        // draw geometry
        Gl.DrawElementsInstanced (geometry.PrimitiveType, geometry.ElementCount, DrawElementsType.UnsignedInt, nativeint 0, surfacesCount)
        Hl.Assert ()

        // teardown geometry
        Gl.BindVertexArray 0u
        Hl.Assert ()

        // teardown texture filters
        for i in 0 .. dec 5 do
            Gl.ActiveTexture (LanguagePrimitives.EnumOfValue (int TextureUnit.Texture0 + i))
            if material.TextureMinFilterOpt.IsSome then
                Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, int TextureMinFilter.LinearMipmapLinear)
            if material.TextureMagFilterOpt.IsSome then
                Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, int TextureMagFilter.Linear)
            Gl.BindTexture (TextureTarget.Texture2d, 0u)
            Hl.Assert ()

        // teardown textures
        Gl.ActiveTexture TextureUnit.Texture0
        Gl.BindTexture (TextureTarget.Texture2d, 0u)
        Gl.ActiveTexture TextureUnit.Texture1
        Gl.BindTexture (TextureTarget.Texture2d, 0u)
        Gl.ActiveTexture TextureUnit.Texture2
        Gl.BindTexture (TextureTarget.Texture2d, 0u)
        Gl.ActiveTexture TextureUnit.Texture3
        Gl.BindTexture (TextureTarget.Texture2d, 0u)
        Gl.ActiveTexture TextureUnit.Texture4
        Gl.BindTexture (TextureTarget.Texture2d, 0u)
        Gl.ActiveTexture TextureUnit.Texture5
        Gl.BindTexture (TextureTarget.TextureCubeMap, 0u)
        Gl.ActiveTexture TextureUnit.Texture6
        Gl.BindTexture (TextureTarget.TextureCubeMap, 0u)
        Gl.ActiveTexture TextureUnit.Texture7
        Gl.BindTexture (TextureTarget.Texture2d, 0u)
        Hl.Assert ()

        // teardown shader
        Gl.UseProgram 0u
        Hl.Assert ()

        // teardown state
        if not material.TwoSided then Gl.Disable EnableCap.CullFace
        if blending then
            Gl.Disable EnableCap.Blend
            Gl.BlendFunc (BlendingFactor.One, BlendingFactor.Zero)
            Gl.BlendEquation BlendEquationMode.FuncAdd
        Gl.Disable EnableCap.DepthTest
        Gl.DepthFunc DepthFunction.Less

    /// Draw a the second pass of a deferred physically-based surface.
    let DrawPhysicallyBasedDeferred2Surface
        (view : single array,
         projection : single array,
         eyeCenter : Vector3,
         lightAmbientColor : single array,
         lightAmbientBrightness : single,
         positionTexture : uint,
         albedoTexture : uint,
         materialTexture : uint,
         normalAndDepthTexture : uint,
         irradianceMap : uint,
         environmentFilterMap : uint,
         brdfTexture : uint,
         lightOrigins : single array,
         lightDirections : single array,
         lightColors : single array,
         lightBrightnesses : single array,
         lightAttenuationLinears : single array,
         lightAttenuationQuadratics : single array,
         lightDirectionals : int array,
         lightConeInners : single array,
         lightConeOuters : single array,
         geometry : PhysicallyBasedGeometry,
         shader : PhysicallyBasedDeferred2Shader) =

        // setup shader
        Gl.UseProgram shader.PhysicallyBasedDeferred2Shader
        Gl.UniformMatrix4 (shader.ViewUniform, false, view)
        Gl.UniformMatrix4 (shader.ProjectionUniform, false, projection)
        Gl.Uniform3 (shader.EyeCenterUniform, eyeCenter.X, eyeCenter.Y, eyeCenter.Z)
        Gl.Uniform3 (shader.LightAmbientColorUniform, lightAmbientColor)
        Gl.Uniform1 (shader.LightAmbientBrightnessUniform, lightAmbientBrightness)
        Gl.Uniform1 (shader.PositionTextureUniform, 0)
        Gl.Uniform1 (shader.AlbedoTextureUniform, 1)
        Gl.Uniform1 (shader.MaterialTextureUniform, 2)
        Gl.Uniform1 (shader.NormalAndDepthTextureUniform, 3)
        Gl.Uniform1 (shader.IrradianceMapUniform, 4)
        Gl.Uniform1 (shader.EnvironmentFilterMapUniform, 5)
        Gl.Uniform1 (shader.BrdfTextureUniform, 6)
        Gl.Uniform3 (shader.LightOriginsUniform, lightOrigins)
        Gl.Uniform3 (shader.LightDirectionsUniform, lightDirections)
        Gl.Uniform3 (shader.LightColorsUniform, lightColors)
        Gl.Uniform1 (shader.LightBrightnessesUniform, lightBrightnesses)
        Gl.Uniform1 (shader.LightAttenuationLinearsUniform, lightAttenuationLinears)
        Gl.Uniform1 (shader.LightAttenuationQuadraticsUniform, lightAttenuationQuadratics)
        Gl.Uniform1 (shader.LightDirectionalsUniform, lightDirectionals)
        Gl.Uniform1 (shader.LightConeInnersUniform, lightConeInners)
        Gl.Uniform1 (shader.LightConeOutersUniform, lightConeOuters)
        Hl.Assert ()

        // setup textures
        Gl.ActiveTexture TextureUnit.Texture0
        Gl.BindTexture (TextureTarget.Texture2d, positionTexture)
        Gl.ActiveTexture TextureUnit.Texture1
        Gl.BindTexture (TextureTarget.Texture2d, albedoTexture)
        Gl.ActiveTexture TextureUnit.Texture2
        Gl.BindTexture (TextureTarget.Texture2d, materialTexture)
        Gl.ActiveTexture TextureUnit.Texture3
        Gl.BindTexture (TextureTarget.Texture2d, normalAndDepthTexture)
        Gl.ActiveTexture TextureUnit.Texture4
        Gl.BindTexture (TextureTarget.TextureCubeMap, irradianceMap)
        Gl.ActiveTexture TextureUnit.Texture5
        Gl.BindTexture (TextureTarget.TextureCubeMap, environmentFilterMap)
        Gl.ActiveTexture TextureUnit.Texture6
        Gl.BindTexture (TextureTarget.Texture2d, brdfTexture)
        Hl.Assert ()

        // setup geometry
        Gl.BindVertexArray geometry.PhysicallyBasedVao
        Gl.BindBuffer (BufferTarget.ArrayBuffer, geometry.VertexBuffer)
        Gl.BindBuffer (BufferTarget.ElementArrayBuffer, geometry.IndexBuffer)
        Hl.Assert ()

        // draw geometry
        Gl.DrawElements (geometry.PrimitiveType, geometry.ElementCount, DrawElementsType.UnsignedInt, nativeint 0)
        Hl.Assert ()

        // teardown geometry
        Gl.BindVertexArray 0u
        Hl.Assert ()

        // teardown textures
        Gl.ActiveTexture TextureUnit.Texture0
        Gl.BindTexture (TextureTarget.Texture2d, 0u)
        Gl.ActiveTexture TextureUnit.Texture1
        Gl.BindTexture (TextureTarget.Texture2d, 0u)
        Gl.ActiveTexture TextureUnit.Texture2
        Gl.BindTexture (TextureTarget.Texture2d, 0u)
        Gl.ActiveTexture TextureUnit.Texture3
        Gl.BindTexture (TextureTarget.Texture2d, 0u)
        Gl.ActiveTexture TextureUnit.Texture4
        Gl.BindTexture (TextureTarget.TextureCubeMap, 0u)
        Gl.ActiveTexture TextureUnit.Texture5
        Gl.BindTexture (TextureTarget.TextureCubeMap, 0u)
        Gl.ActiveTexture TextureUnit.Texture6
        Gl.BindTexture (TextureTarget.Texture2d, 0u)
        Hl.Assert ()

        // teardown shader
        Gl.UseProgram 0u

    /// Destroy physically-based geometry resources.
    let DestroyPhysicallyBasedGeometry geometry =
        Gl.BindVertexArray geometry.PhysicallyBasedVao
        Gl.DeleteBuffers [|geometry.VertexBuffer|]
        Gl.DeleteBuffers [|geometry.ModelBuffer|]
        Gl.DeleteBuffers [|geometry.TexCoordsOffsetBuffer|]
        Gl.DeleteBuffers [|geometry.AlbedoBuffer|]
        Gl.DeleteBuffers [|geometry.MaterialBuffer|]
        Gl.DeleteBuffers [|geometry.InvertRoughnessBuffer|]
        Gl.DeleteBuffers [|geometry.IndexBuffer|]
        Gl.BindVertexArray 0u
        Gl.DeleteVertexArrays [|geometry.PhysicallyBasedVao|]

    /// Destroy physically-based static model resources.
    let DestroyPhysicallyBasedStaticModel staticModel =
        for surface in staticModel.Surfaces do
            DestroyPhysicallyBasedGeometry surface.PhysicallyBasedGeometry