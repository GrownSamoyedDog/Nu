﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2023.

namespace OpenGL
open System
open System.Numerics
open Prime
open Nu

[<RequireQualifiedAccess>]
module SpriteBatch =

    type [<StructuralEquality; NoComparison; Struct>] private SpriteBatchState =
        { Absolute : bool
          BlendingFactorSrc : BlendingFactor
          BlendingFactorDst : BlendingFactor
          BlendingEquation : BlendEquationMode
          Texture : uint }

        static member inline changed state state2 =
            state.Absolute <> state2.Absolute ||
            state.BlendingFactorSrc <> state2.BlendingFactorSrc ||
            state.BlendingFactorDst <> state2.BlendingFactorDst ||
            state.BlendingEquation <> state2.BlendingEquation ||
            state.Texture <> state2.Texture

        static member make absolute bfs bfd beq texture =
            { Absolute = absolute; BlendingFactorSrc = bfs; BlendingFactorDst = bfd; BlendingEquation = beq; Texture = texture }

        static member defaultState =
            SpriteBatchState.make false BlendingFactor.SrcAlpha BlendingFactor.OneMinusSrcAlpha BlendEquationMode.FuncAdd 0u

    type [<ReferenceEquality>] SpriteBatchEnv =
        private
            { mutable SpriteIndex : int
              mutable ViewProjectionAbsolute : Matrix4x4
              mutable ViewProjectionRelative : Matrix4x4
              PerimetersUniform : int
              TexCoordsesUniform : int
              PivotsUniform : int
              RotationsUniform : int
              ColorsUniform : int
              ViewProjectionUniform : int
              TexUniform : int
              Shader : uint
              Perimeters : single array
              Pivots : single array
              Rotations : single array
              TexCoordses : single array
              Colors : single array
              Vao : uint
              mutable State : SpriteBatchState }

    /// Create a batched sprite shader.
    let private CreateSpriteBatchShader () =

        // vertex shader code
        let samplerVertexShaderStr =
            [Constants.Render.GlslVersionPragma
             ""
             "const int Verts = 6;"
             ""
             "const vec4 Filters[Verts] ="
             "  vec4[Verts]("
             "      vec4(1,1,0,0),"
             "      vec4(1,1,1,0),"
             "      vec4(1,1,1,1),"
             "      vec4(1,1,1,1),"
             "      vec4(1,1,0,1),"
             "      vec4(1,1,0,0));"
             ""
             "uniform vec4 perimeters[" + string Constants.Render.SpriteBatchSize + "];"
             "uniform vec2 pivots[" + string Constants.Render.SpriteBatchSize + "];"
             "uniform float rotations[" + string Constants.Render.SpriteBatchSize + "];"
             "uniform vec4 texCoordses[" + string Constants.Render.SpriteBatchSize + "];"
             "uniform vec4 colors[" + string Constants.Render.SpriteBatchSize + "];"
             "uniform mat4 viewProjection;"
             "out vec2 texCoords;"
             "out vec4 color;"
             ""
             "vec2 rotate(vec2 v, float a)"
             "{"
             "  float s = sin(a);"
             "  float c = cos(a);"
             "  mat2 m = mat2(c, -s, s, c);"
             "  return m * v;"
             "}"
             ""
             "void main()"
             "{"
             "  // compute ids"
             "  int spriteId = gl_VertexID / Verts;"
             "  int vertexId = gl_VertexID % Verts;"
             ""
             "  // compute position"
             "  vec4 filt = Filters[vertexId];"
             "  vec4 perimeter = perimeters[spriteId] * filt;"
             "  vec2 position = vec2(perimeter.x + perimeter.z, perimeter.y + perimeter.w);"
             "  vec2 pivot = pivots[spriteId];"
             "  vec2 positionRotated = rotate(position - pivot, rotations[spriteId]) + pivot;"
             "  gl_Position = viewProjection * vec4(positionRotated.x, positionRotated.y, 0, 1);"
             ""
             "  // compute tex coords"
             "  vec4 texCoords4 = texCoordses[spriteId] * filt;"
             "  texCoords = vec2(texCoords4.x + texCoords4.z, texCoords4.y + texCoords4.w);"
             ""
             "  // compute color"
             "  color = colors[spriteId];"
             "}"] |> String.join "\n"

        // fragment shader code
        let samplerFragmentShaderStr =
            [Constants.Render.GlslVersionPragma
             "uniform sampler2D tex;"
             "in vec2 texCoords;"
             "in vec4 color;"
             "out vec4 frag;"
             "void main()"
             "{"
             "  frag = color * texture(tex, texCoords);"
             "}"] |> String.join "\n"

        // create shader
        let shader = Shader.CreateShaderFromStrs (samplerVertexShaderStr, samplerFragmentShaderStr)
        Hl.Assert ()

        // grab uniform locations
        let perimetersUniform = Gl.GetUniformLocation (shader, "perimeters")
        let pivotsUniform = Gl.GetUniformLocation (shader, "pivots")
        let rotationsUniform = Gl.GetUniformLocation (shader, "rotations")
        let texCoordsesUniform = Gl.GetUniformLocation (shader, "texCoordses")
        let colorsUniform = Gl.GetUniformLocation (shader, "colors")
        let viewProjectionUniform = Gl.GetUniformLocation (shader, "viewProjection")
        let texUniform = Gl.GetUniformLocation (shader, "tex")
        Hl.Assert ()

        // fin
        (perimetersUniform, pivotsUniform, rotationsUniform, texCoordsesUniform, colorsUniform, viewProjectionUniform, texUniform, shader)

    let private BeginSpriteBatch state env =
        env.State <- state

    let private EndSpriteBatch env =

        // ensure something to draw
        if env.SpriteIndex > 0 then

            // setup state
            Gl.BlendEquation env.State.BlendingEquation
            Gl.BlendFunc (env.State.BlendingFactorSrc, env.State.BlendingFactorDst)
            Gl.Enable EnableCap.Blend
            Gl.Enable EnableCap.CullFace
            Hl.Assert ()

            // setup vao
            Gl.BindVertexArray env.Vao
            Hl.Assert ()

            // setup shader
            Gl.UseProgram env.Shader
            Gl.Uniform4 (env.PerimetersUniform, env.Perimeters)
            Gl.Uniform4 (env.TexCoordsesUniform, env.TexCoordses)
            Gl.Uniform2 (env.PivotsUniform, env.Pivots)
            Gl.Uniform1 (env.RotationsUniform, env.Rotations)
            Gl.Uniform4 (env.ColorsUniform, env.Colors)
            Gl.UniformMatrix4 (env.ViewProjectionUniform, false, if env.State.Absolute then env.ViewProjectionAbsolute.ToArray () else env.ViewProjectionRelative.ToArray ())
            Gl.Uniform1 (env.TexUniform, 0)
            Gl.ActiveTexture TextureUnit.Texture0
            Gl.BindTexture (TextureTarget.Texture2d, env.State.Texture)
            Hl.Assert ()

            // draw geometry
            Gl.DrawArrays (PrimitiveType.Triangles, 0, 6 * env.SpriteIndex)
            Hl.Assert ()

            // teardown shader
            Gl.BindTexture (TextureTarget.Texture2d, 0u)
            Gl.UseProgram 0u
            Hl.Assert ()
        
            // teardown vao
            Gl.BindVertexArray 0u
            Hl.Assert ()

            // teardown state
            Gl.Disable EnableCap.CullFace
            Gl.Disable EnableCap.Blend
            Gl.BlendFunc (BlendingFactor.One, BlendingFactor.Zero)
            Gl.BlendEquation BlendEquationMode.FuncAdd

            // next batch
            env.SpriteIndex <- 0

    let private RestartSpriteBatch state env =
        Hl.Assert (EndSpriteBatch env)
        BeginSpriteBatch state env

    let BeginSpriteBatchFrame (viewProjectionAbsolute : Matrix4x4 inref, viewProjectionRelative : Matrix4x4 inref, env) =
        env.ViewProjectionAbsolute <- viewProjectionAbsolute
        env.ViewProjectionRelative <- viewProjectionRelative
        BeginSpriteBatch SpriteBatchState.defaultState env

    let EndSpriteBatchFrame env =
        EndSpriteBatch env

    let InterruptSpriteBatchFrame fn env =
        let state = env.State
        Hl.Assert (EndSpriteBatch env)
        Hl.Assert (fn ())
        BeginSpriteBatch state env

    let
#if !DEBUG
        inline
#endif
        private PopulateSpriteBatchVertex (perimeter : Box2) (pivot : Vector2) (rotation : single) (texCoords : Box2) (color : Color) env =
        let perimeterOffset = env.SpriteIndex * 4
        env.Perimeters.[perimeterOffset] <- perimeter.Min.X
        env.Perimeters.[perimeterOffset + 1] <- perimeter.Min.Y
        env.Perimeters.[perimeterOffset + 2] <- perimeter.Size.X
        env.Perimeters.[perimeterOffset + 3] <- perimeter.Size.Y
        let pivotOffset = env.SpriteIndex * 2
        env.Pivots.[pivotOffset] <- pivot.X
        env.Pivots.[pivotOffset + 1] <- pivot.Y
        let rotationOffset = env.SpriteIndex
        env.Rotations.[rotationOffset] <- rotation
        let texCoordsOffset = env.SpriteIndex * 4
        env.TexCoordses.[texCoordsOffset] <- texCoords.Min.X
        env.TexCoordses.[texCoordsOffset + 1] <- texCoords.Min.Y
        env.TexCoordses.[texCoordsOffset + 2] <- texCoords.Size.X
        env.TexCoordses.[texCoordsOffset + 3] <- texCoords.Size.Y
        let colorOffset = env.SpriteIndex * 4
        env.Colors.[colorOffset] <- color.R
        env.Colors.[colorOffset + 1] <- color.G
        env.Colors.[colorOffset + 2] <- color.B
        env.Colors.[colorOffset + 3] <- color.A

    let SubmitSpriteBatchSprite (absolute, min : Vector2, size : Vector2, pivot : Vector2, rotation, texCoords : Box2 inref, color : Color inref, bfs, bfd, beq, texture, env) =

        // adjust to potential sprite batch state changes
        let state = SpriteBatchState.make absolute bfs bfd beq texture
        if SpriteBatchState.changed state env.State || env.SpriteIndex = Constants.Render.SpriteBatchSize then
            RestartSpriteBatch state env
            Hl.Assert ()

        // populate vertices
        let perimeter = box2 min size
        PopulateSpriteBatchVertex perimeter pivot rotation texCoords color env

        // advance sprite index
        env.SpriteIndex <- inc env.SpriteIndex

    let CreateSpriteBatchEnv () =

        // create vao
        let vao = Hl.AllocVertexArray ()
        Hl.Assert ()

        // create shader
        let (perimetersUniform, pivotsUniform, rotationsUniform, texCoordsesUniform, colorsUniform, viewProjectionUniform, texUniform, shader) = CreateSpriteBatchShader ()
        Hl.Assert ()

        // create env
        { SpriteIndex = 0; ViewProjectionAbsolute = m4Identity; ViewProjectionRelative = m4Identity
          PerimetersUniform = perimetersUniform; PivotsUniform = pivotsUniform; RotationsUniform = rotationsUniform
          TexCoordsesUniform = texCoordsesUniform; ColorsUniform = colorsUniform; ViewProjectionUniform = viewProjectionUniform
          TexUniform = texUniform; Shader = shader
          Perimeters = Array.zeroCreate (Constants.Render.SpriteBatchSize * 4)
          Pivots = Array.zeroCreate (Constants.Render.SpriteBatchSize * 2)
          Rotations = Array.zeroCreate (Constants.Render.SpriteBatchSize)
          TexCoordses = Array.zeroCreate (Constants.Render.SpriteBatchSize * 4)
          Colors = Array.zeroCreate (Constants.Render.SpriteBatchSize * 4)
          Vao = vao
          State = SpriteBatchState.defaultState }

    let DestroySpriteBatchEnv env =
        env.SpriteIndex <- 0