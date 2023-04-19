﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2023.

namespace OpenGL
open System
open System.Collections.Generic
open System.IO
open System.Runtime.InteropServices
open SDL2
open Prime
open Nu

// NOTE: on Nu's texturing nomenclature -
// Texture | Texture2d =    2d texture
// Texture3d =              3d texture
// (Cube|_)Map =            cube map
[<RequireQualifiedAccess>]
module Texture =

    /// A texture's metadata.
    type TextureMetadata =
        { TextureWidth : int
          TextureHeight : int
          TextureTexelWidth : single
          TextureTexelHeight : single
          TextureInternalFormat : InternalFormat }

        /// Unpopulated texture data.
        static member empty =
            { TextureWidth = 0
              TextureHeight = 0
              TextureTexelWidth = 0.0f
              TextureTexelHeight = 0.0f
              TextureInternalFormat = Unchecked.defaultof<_> }

    /// Memoizes texture loads.
    type [<ReferenceEquality>] TextureMemo =
        private
            { Textures : Dictionary<string, TextureMetadata * uint> }

        /// Make a texture memoizer.
        static member make () =
            { Textures = Dictionary HashIdentity.Structural }

    /// Vertically flip an SDL surface.
    let FlipSurface (surface : SDL.SDL_Surface inref) =
        let rowTop = Array.zeroCreate<byte> surface.pitch
        let rowBottom = Array.zeroCreate<byte> surface.pitch
        for i in 0 .. dec (surface.h / 2) do
            let offsetTop = i * surface.pitch
            let pixelsTop = surface.pixels + nativeint offsetTop
            let offsetBottom = (dec surface.h - i) * surface.pitch
            let pixelsBottom = surface.pixels + nativeint offsetBottom
            Marshal.Copy (pixelsTop, rowTop, 0, surface.pitch)
            Marshal.Copy (pixelsBottom, rowBottom, 0, surface.pitch)
            Marshal.Copy (rowTop, 0, pixelsBottom, surface.pitch)
            Marshal.Copy (rowBottom, 0, pixelsTop, surface.pitch)

    /// Attempt to create uploadable image data from the given file path.
    /// Don't forget to dispose the last field when finished with the image data.
    let TryCreateImageData (filePath : string) =
        if File.Exists filePath then
            let platform = Environment.OSVersion.Platform
            let fileExtension = Path.GetExtension(filePath).ToLowerInvariant()
            if  (platform = PlatformID.Win32NT || platform = PlatformID.Win32Windows) &&
                fileExtension <> ".tga" (* NOTE: System.Drawing.Bitmap does not seem to support .tga loading. *) then
                // NOTE: System.Drawing.Bitmap is, AFAIK, only available on non-Windows platforms, so we use a fast path here.
                try let bitmap = new Drawing.Bitmap (filePath)
                    let data = bitmap.LockBits (Drawing.Rectangle (0, 0, bitmap.Width, bitmap.Height), Drawing.Imaging.ImageLockMode.ReadOnly, Drawing.Imaging.PixelFormat.Format32bppRgb)
                    let metadata =
                        { TextureWidth = bitmap.Width
                          TextureHeight = bitmap.Height
                          TextureTexelWidth = 1.0f / single bitmap.Width
                          TextureTexelHeight = 1.0f / single bitmap.Height
                          TextureInternalFormat = InternalFormat.Rgba8 }
                    Some (metadata, data.Scan0, { new IDisposable with member this.Dispose () = bitmap.UnlockBits data; bitmap.Dispose () }) // NOTE: calling UnlockBits explicitly since I can't fiture out if Dispose does.
                with _ -> None
            else
                // NOTE: System.Drawing.Bitmap is not, AFAIK, available on non-Windows platforms, so we use a slower path here.
                let format = SDL.SDL_PIXELFORMAT_ARGB8888 // seems to be the right format on Ubuntu...
                let unconvertedPtr = SDL_image.IMG_Load filePath
                if unconvertedPtr <> nativeint 0 then
                    let unconverted = Marshal.PtrToStructure<SDL.SDL_Surface> unconvertedPtr
                    let metadata =
                        { TextureWidth = unconverted.w
                          TextureHeight = unconverted.h
                          TextureTexelWidth = 1.0f / single unconverted.w
                          TextureTexelHeight = 1.0f / single unconverted.h
                          TextureInternalFormat = InternalFormat.Rgba8 }
                    let unconvertedFormat = Marshal.PtrToStructure<SDL.SDL_PixelFormat> unconverted.format
                    if unconvertedFormat.format <> format then
                        let convertedPtr = SDL.SDL_ConvertSurfaceFormat (unconvertedPtr, format, 0u)
                        let converted = Marshal.PtrToStructure<SDL.SDL_Surface> convertedPtr
                        SDL.SDL_FreeSurface unconvertedPtr // no longer need this
                        Some (metadata, converted.pixels, { new IDisposable with member this.Dispose () = SDL.SDL_FreeSurface convertedPtr })
                    else Some (metadata, unconverted.pixels, { new IDisposable with member this.Dispose () = SDL.SDL_FreeSurface unconvertedPtr })
                else None
        else None

    let private TryCreateTextureInternal (textureOpt, generateMipmaps, filePath : string) =

        // attmept to load image
        match TryCreateImageData filePath with
        | Some (metadata, imageData, disposer) ->

            // upload the image data to gl
            use _ = disposer
            let texture = match textureOpt with Some texture -> texture | None -> Gl.GenTexture ()
            Gl.BindTexture (TextureTarget.Texture2d, texture)
            Gl.TexImage2D (TextureTarget.Texture2d, 0, metadata.TextureInternalFormat, metadata.TextureWidth, metadata.TextureHeight, 0, PixelFormat.Bgra, PixelType.UnsignedByte, imageData)
            if generateMipmaps then Gl.GenerateMipmap TextureTarget.Texture2d
            Hl.Assert ()
            Gl.PrioritizeTextures ([|texture|], [|1.0f|]);
            ignore<ErrorCode> (Gl.GetError ()) // ignore errors from prioritize texture
            Gl.BindTexture (TextureTarget.Texture2d, 0u)
            Right (metadata, texture)

        // failure
        | None -> Left ("Missing file or unloadable image data '" + filePath + "'.")

    /// Attempt to create a texture from a file.
    let TryCreateTexture (minFilter, magFilter, generateMipmaps, filePath) =
        match TryCreateTextureInternal (None, generateMipmaps, filePath) with
        | Right (metadata, texture) ->
            Gl.BindTexture (TextureTarget.Texture2d, texture)
            Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, int minFilter)
            Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, int magFilter)
            Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureWrapS, int TextureWrapMode.Repeat)
            Gl.TexParameter (TextureTarget.Texture2d, TextureParameterName.TextureWrapT, int TextureWrapMode.Repeat)
            Gl.TexParameter (TextureTarget.Texture2d, LanguagePrimitives.EnumOfValue Gl.TEXTURE_MAX_ANISOTROPY, Constants.Render.TextureAnisotropyMax) // NOTE: tho an extension, this one's considered ubiquitous.
            if generateMipmaps then Gl.GenerateMipmap TextureTarget.Texture2d
            Right (metadata, texture)
        | Left _ as error -> error

    /// Attempt to create an unfiltered texture from a file.
    let TryCreateTextureUnfiltered filePath =
        TryCreateTexture (TextureMinFilter.Nearest, TextureMagFilter.Nearest, false, filePath)

    /// Attempt to create a filtered texture from a file.
    let TryCreateTextureFiltered filePath =
        TryCreateTexture (TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Linear, true, filePath)

    /// Delete a texture.
    let DeleteTexture (texture : uint) =
        Gl.DeleteTextures texture

    /// Attempt to create a memoized texture from a file.
    let TryCreateTextureMemoized (minFilter, magFilter, generateMipmaps, filePath : string, textureMemo) =

        // memoize texture
        match textureMemo.Textures.TryGetValue filePath with
        | (false, _) ->

            // attempt to create texture
            match TryCreateTexture (minFilter, magFilter, generateMipmaps, filePath) with
            | Right texture ->
                textureMemo.Textures.Add (filePath, texture)
                Right texture
            | Left error -> Left error

        // already exists
        | (true, texture) -> Right texture

    /// Attempt to create a memoized unfiltered texture from a file.
    let TryCreateTextureMemoizedUnfiltered (filePath, textureMemo) =
        TryCreateTextureMemoized (TextureMinFilter.Nearest, TextureMagFilter.Nearest, false, filePath, textureMemo)

    /// Attempt to create a memoized filtered texture from a file.
    let TryCreateTextureMemoizedFiltered (filePath, textureMemo) =
        TryCreateTextureMemoized (TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Linear, true, filePath, textureMemo)

    /// Recreate the memoized textures.
    let RecreateTexturesMemoized generateMipmaps textureMemo =
        for entry in textureMemo.Textures do
            let filePath = entry.Key
            let (_, texture) = entry.Value
            TryCreateTextureInternal (Some texture, generateMipmaps, filePath) |> ignore

    /// Delete a memoized texture.
    let DeleteTextureMemoized textureKey (textureMemo : TextureMemo) =
        match textureMemo.Textures.TryGetValue textureKey with
        | (true, (_, texture)) ->
            Gl.DeleteTextures texture
            textureMemo.Textures.Remove textureKey |> ignore<bool>
        | (false, _) -> ()

    /// Delete memoized textures.
    let DeleteTexturesMemoized textureMemo =
        for entry in textureMemo.Textures do
            DeleteTexture (snd entry.Value)
        textureMemo.Textures.Clear ()