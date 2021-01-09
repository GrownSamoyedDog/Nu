﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2020.

namespace Nu
open System

/// IO artifacts passively produced and consumed by Nu.
type [<NoEquality; NoComparison>] View =
    | Render of single * single * obj AssetTag * RenderDescriptor
    | PlaySound of single * Sound AssetTag
    | PlaySong of int * single * Song AssetTag
    | FadeOutSong of int
    | StopSong
    | SpawnEmitter of string * Particles.BasicEmitterDescriptor
    | Tag of string * obj
    | Views of View array

[<RequireQualifiedAccess>]
module View =

    /// Convert a view to an seq of zero or more views.
    let rec toSeq view =
        seq {
            match view with
            | Views views -> for view in views do yield! toSeq view
            | _ -> yield view }

    /// Convert a view to an array of zero or more views.
    let rec toArray view =
        view |> toSeq |> Seq.toArray

    /// The empty view.
    let empty = Views [||]
