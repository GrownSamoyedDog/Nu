﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2023.

namespace OpenGL
open System
open Prime
open Nu

[<RequireQualifiedAccess>]
module Framebuffer =

    /// Attempt to create a texture 2d framebuffer.
    let TryCreateTextureFramebuffer () =

        // create frame buffer object
        let framebuffer = Gl.GenFramebuffer ()
        Gl.BindFramebuffer (FramebufferTarget.Framebuffer, framebuffer)
        Hl.Assert ()

        // create texture 2d buffer
        let texture = Gl.GenTexture ()
        Gl.BindTexture (TextureTarget.Texture2d, texture)
        Gl.TexImage2D (TextureTarget.Texture2d, 0, InternalFormat.Rgba16f, Constants.Render.ResolutionX, Constants.Render.ResolutionY, 0, PixelFormat.Rgba, PixelType.Float, nativeint 0)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, int TextureMinFilter.Nearest)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, int TextureMagFilter.Nearest)
        Gl.FramebufferTexture2D (FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2d, texture, 0)
        Hl.Assert ()

        // create depth and stencil buffers
        let depthStencilBuffer = Gl.GenRenderbuffer ()
        Gl.BindRenderbuffer (RenderbufferTarget.Renderbuffer, depthStencilBuffer)
        Gl.RenderbufferStorage (RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, Constants.Render.ResolutionX, Constants.Render.ResolutionY)
        Gl.FramebufferRenderbuffer (FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, depthStencilBuffer)
        Hl.Assert ()

        // ensure framebuffer is complete
        if Gl.CheckFramebufferStatus FramebufferTarget.Framebuffer = FramebufferStatus.FramebufferComplete
        then Right (texture, framebuffer)
        else Left ("Could not create complete texture 2d framebuffer.")

    /// Attempt to create an hdr framebuffer.
    let TryCreateHdrFramebuffer () =

        // create frame buffer object
        let framebuffer = Gl.GenFramebuffer ()
        Gl.BindFramebuffer (FramebufferTarget.Framebuffer, framebuffer)
        Hl.Assert ()

        // create position buffer
        let position = Gl.GenTexture ()
        Gl.BindTexture (TextureTarget.Texture2d, position)
        Gl.TexImage2D (TextureTarget.Texture2d, 0, InternalFormat.Rgba16f, Constants.Render.ResolutionX, Constants.Render.ResolutionY, 0, PixelFormat.Rgba, PixelType.Float, nativeint 0)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, int TextureMinFilter.Nearest)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, int TextureMagFilter.Nearest)
        Gl.FramebufferTexture2D (FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2d, position, 0)
        Hl.Assert ()

        // create depth and stencil buffers
        let depthStencilBuffer = Gl.GenRenderbuffer ()
        Gl.BindRenderbuffer (RenderbufferTarget.Renderbuffer, depthStencilBuffer)
        Gl.RenderbufferStorage (RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, Constants.Render.ResolutionX, Constants.Render.ResolutionY)
        Gl.FramebufferRenderbuffer (FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, depthStencilBuffer)
        Hl.Assert ()

        // ensure framebuffer is complete
        if Gl.CheckFramebufferStatus FramebufferTarget.Framebuffer = FramebufferStatus.FramebufferComplete
        then Right (position, framebuffer)
        else Left ("Could not create complete HDR framebuffer.")

    /// Create a geometry buffers.
    let TryCreateGeometryBuffers () =

        // create frame buffer object
        let framebuffer = Gl.GenFramebuffer ()
        Gl.BindFramebuffer (FramebufferTarget.Framebuffer, framebuffer)
        Hl.Assert ()

        // create position buffer
        let position = Gl.GenTexture ()
        Gl.BindTexture (TextureTarget.Texture2d, position)
        Gl.TexImage2D (TextureTarget.Texture2d, 0, InternalFormat.Rgba32f, Constants.Render.ResolutionX, Constants.Render.ResolutionY, 0, PixelFormat.Rgba, PixelType.Float, nativeint 0)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, int TextureMinFilter.Nearest)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, int TextureMagFilter.Nearest)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureWrapS, int TextureWrapMode.ClampToEdge)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureWrapT, int TextureWrapMode.ClampToEdge)
        Gl.FramebufferTexture2D (FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2d, position, 0)
        Hl.Assert ()

        // create albedo buffer
        let albedo = Gl.GenTexture ()
        Gl.BindTexture (TextureTarget.Texture2d, albedo)
        Gl.TexImage2D (TextureTarget.Texture2d, 0, InternalFormat.Rgba16f, Constants.Render.ResolutionX, Constants.Render.ResolutionY, 0, PixelFormat.Rgba, PixelType.Float, nativeint 0)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, int TextureMinFilter.Nearest)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, int TextureMagFilter.Nearest)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureWrapS, int TextureWrapMode.ClampToEdge)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureWrapT, int TextureWrapMode.ClampToEdge)
        Gl.FramebufferTexture2D (FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2d, albedo, 0)
        Hl.Assert ()

        // create material buffer
        let material = Gl.GenTexture ()
        Gl.BindTexture (TextureTarget.Texture2d, material)
        Gl.TexImage2D (TextureTarget.Texture2d, 0, InternalFormat.Rgba16f, Constants.Render.ResolutionX, Constants.Render.ResolutionY, 0, PixelFormat.Rgba, PixelType.Float, nativeint 0)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, int TextureMinFilter.Nearest)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, int TextureMagFilter.Nearest)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureWrapS, int TextureWrapMode.ClampToEdge)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureWrapT, int TextureWrapMode.ClampToEdge)
        Gl.FramebufferTexture2D (FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, TextureTarget.Texture2d, material, 0)
        Hl.Assert ()

        // create normal and depth buffer
        let normalAndDepth = Gl.GenTexture ()
        Gl.BindTexture (TextureTarget.Texture2d, normalAndDepth)
        Gl.TexImage2D (TextureTarget.Texture2d, 0, InternalFormat.Rgba16f, Constants.Render.ResolutionX, Constants.Render.ResolutionY, 0, PixelFormat.Rgba, PixelType.Float, nativeint 0)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, int TextureMinFilter.Nearest)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, int TextureMagFilter.Nearest)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureWrapS, int TextureWrapMode.ClampToEdge)
        Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureWrapT, int TextureWrapMode.ClampToEdge)
        Gl.FramebufferTexture2D (FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment3, TextureTarget.Texture2d, normalAndDepth, 0)
        Hl.Assert ()

        // associate draw buffers
        Gl.DrawBuffers
            [|int FramebufferAttachment.ColorAttachment0
              int FramebufferAttachment.ColorAttachment1
              int FramebufferAttachment.ColorAttachment2
              int FramebufferAttachment.ColorAttachment3|]

        // create render buffer with depth and stencil
        let renderbuffer = Gl.GenRenderbuffer ()
        Gl.BindRenderbuffer (RenderbufferTarget.Renderbuffer, renderbuffer)
        Gl.RenderbufferStorage (RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, Constants.Render.ResolutionX, Constants.Render.ResolutionY)
        Gl.FramebufferRenderbuffer (FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, renderbuffer)
        Hl.Assert ()

        // ensure framebuffer is complete
        if Gl.CheckFramebufferStatus FramebufferTarget.Framebuffer = FramebufferStatus.FramebufferComplete
        then Right (position, albedo, material, normalAndDepth, renderbuffer, framebuffer)
        else Left "Could not create complete geometry framebuffer."