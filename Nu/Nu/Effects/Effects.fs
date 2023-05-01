﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2023.

namespace Nu.Effects
open System
open System.Collections.Generic
open System.Numerics
open Prime
open Nu

// TODO: document all this!

type [<StructuralEquality; StructuralComparison>] LogicApplicator =
    | Or
    | Nor
    | Xor
    | And
    | Nand
    | Equal

type [<StructuralEquality; StructuralComparison>] TweenAlgorithm =
    | Constant
    | Linear
    | Random
    | Chaos
    | Ease
    | EaseIn
    | EaseOut
    | Sin
    | SinScaled of single
    | Cos
    | CosScaled of single

type [<StructuralEquality; StructuralComparison>] TweenApplicator =
    | Sum
    | Delta
    | Scalar
    | Ratio
    | Modulo
    | Pow
    | Set

type Slice =
    { Position : Vector3
      Scale : Vector3
      Offset : Vector3
      Size : Vector3
      Angles : Vector3
      Elevation : single
      Inset : Box2
      Color : Color
      Blend : Blend
      Emission : Color
      Height : single
      Flip : Flip
      Brightness : single
      AttenuationLinear : single
      AttenuationQuadratic : single
      Cutoff : single
      Volume : single
      Enabled : bool
      Centered : bool }

type KeyFrame =
    abstract KeyFrameLength : GameTime

type LogicKeyFrame =
    { LogicValue : bool
      LogicLength : GameTime }
    interface KeyFrame with
        member this.KeyFrameLength = this.LogicLength

type TweenKeyFrame =
    { TweenValue : single
      TweenLength : GameTime }
    interface KeyFrame with
        member this.KeyFrameLength = this.TweenLength

type Tween2KeyFrame =
    { TweenValue : Vector2
      TweenLength : GameTime }
    interface KeyFrame with
        member this.KeyFrameLength = this.TweenLength

type Tween3KeyFrame =
    { TweenValue : Vector3
      TweenLength : GameTime }
    interface KeyFrame with
        member this.KeyFrameLength = this.TweenLength

type Tween4KeyFrame =
    { TweenValue : Vector4
      TweenLength : GameTime }
    interface KeyFrame with
        member this.KeyFrameLength = this.TweenLength

type TweenBox2KeyFrame =
    { TweenValue : Box2
      TweenLength : GameTime }
    interface KeyFrame with
        member this.KeyFrameLength = this.TweenLength

type TweenCKeyFrame =
    { TweenValue : Color
      TweenLength : GameTime }
    interface KeyFrame with
        member this.KeyFrameLength = this.TweenLength

type TweenIKeyFrame =
    { TweenValue : int
      TweenLength : GameTime }
    interface KeyFrame with
        member this.KeyFrameLength = this.TweenLength

type Tween2IKeyFrame =
    { TweenValue : Vector2i
      TweenLength : GameTime }
    interface KeyFrame with
        member this.KeyFrameLength = this.TweenLength

type [<StructuralEquality; StructuralComparison>] Playback =
    | Once
    | Loop
    | Bounce

type [<StructuralEquality; StructuralComparison>] Repetition =
    | Cycle of Cycles : int
    | Iterate of Iterations : int

type [<StructuralEquality; StructuralComparison>] Rate =
    Rate of single

type [<StructuralEquality; StructuralComparison>] Shift =
    Shift of single

type Resource =
    | Resource of string * string
    | Expand of string * Argument array

and Aspect =
    | Enabled of bool
    | PositionAbsolute of Vector3
    | PositionRelative of Vector3
    | Translation of Vector3
    | Scale of Vector3
    | Offset of Vector3
    | Angles of Vector3
    | Degrees of Vector3
    | Size of Vector3
    | Elevation of single
    | Inset of Box2
    | Color of Color
    | Blend of Blend
    | Emission of Color
    | Height of single
    | Flip of Flip
    | Brightness of single
    | AttenuationLinear of single
    | AttenuationQuadratic of single
    | Cutoff of single
    | Volume of single
    | Enableds of LogicApplicator * Playback * LogicKeyFrame array
    | Positions of TweenApplicator * TweenAlgorithm * Playback * Tween3KeyFrame array
    | Translations of TweenApplicator * TweenAlgorithm * Playback * Tween3KeyFrame array
    | Scales of TweenApplicator * TweenAlgorithm * Playback * Tween3KeyFrame array
    | Offsets of TweenApplicator * TweenAlgorithm * Playback * Tween3KeyFrame array
    | Angleses of TweenApplicator * TweenAlgorithm * Playback * Tween3KeyFrame array
    | Degreeses of TweenApplicator * TweenAlgorithm * Playback * Tween3KeyFrame array
    | Sizes of TweenApplicator * TweenAlgorithm * Playback * Tween3KeyFrame array
    | Elevations of TweenApplicator * TweenAlgorithm * Playback * TweenKeyFrame array
    | Insets of TweenApplicator * TweenAlgorithm * Playback * TweenBox2KeyFrame array
    | Colors of TweenApplicator * TweenAlgorithm * Playback * TweenCKeyFrame array
    | Emissions of TweenApplicator * TweenAlgorithm * Playback * TweenCKeyFrame array
    | Heights of TweenApplicator * TweenAlgorithm * Playback * TweenKeyFrame array
    | Volumes of TweenApplicator * TweenAlgorithm * Playback * TweenKeyFrame array
    | Expand of string * Argument array
    | Aspects of Aspect array

and Content =
    | Nil // first to make default value when missing
    | StaticSprite of Resource * Aspect array * Content
    | AnimatedSprite of Resource * Vector2i * int * int * GameTime * Playback * Aspect array * Content
    | TextSprite of Resource * string * Aspect array * Content
    | Billboard of Resource * Resource * Resource * Resource * Resource * Resource * Resource * OpenGL.TextureMinFilter option * OpenGL.TextureMagFilter option * Aspect array * Content
    | StaticModel of Resource * Aspect array * Content
    | Light3d of LightType * Aspect array * Content
    | SoundEffect of Resource * Aspect array * Content
    | Mount of Shift * Aspect array * Content
    | Repeat of Shift * Repetition * Aspect array * Content
    | Emit of Shift * Rate * Aspect array * Aspect array * Content
    | Tag of string * Aspect array * Content
    | Delay of GameTime * Content
    | Segment of GameTime * GameTime * Content
    | Expand of string * Argument array
    | Contents of Shift * Content array

and Argument =
    SymbolicCompression<Resource, SymbolicCompression<Aspect, Content>>

type Definition =
    { DefinitionParams : string array
      DefinitionBody : SymbolicCompression<Resource, SymbolicCompression<Aspect, Content>> }

type Definitions =
    Map<string, Definition>

/// Describes an effect in a compositional manner.
[<Syntax
    ("Constant Linear Random Chaos Ease EaseIn EaseOut Sin SinScaled Cos CosScaled " +
     "Or Nor Xor And Nand Equal " +
     "Sum Delta Scalar Ratio Set " +
     "Once Loop Bounce " +
     "Cycle Iterate " +
     "Rate " +
     "Shift " +
     "Resource Expand " +
     "Enabled PositionAbsolute PositionRelative Translation Scale Offset Angles Degrees Size Elevation Inset Color Emission Height Volume " +
     "Enableds Positions Translations Scales Offsets Angleses Degreeses Sizes Elevations Insets Colors Emissions Heights Volumes Aspects " +
     "Expand " +
     "StaticSprite AnimatedSprite TextSprite SoundEffect Mount Repeat Emit Delay Segment Composite Tag Nil " +
     "View",
     "", "", "", "",
     Constants.PrettyPrinter.DefaultThresholdMin,
     Constants.PrettyPrinter.CompositionalThresholdMax)>]
type EffectDescriptor =
    { EffectName : string
      LifeTimeOpt : GameTime option
      Definitions : Definitions
      Content : Content }

[<RequireQualifiedAccess>]
module EffectDescriptor =

    /// Combine multiple effect descriptors into one.
    let concat descriptors =
        { EffectName = String.concat "+" (Seq.map (fun descriptor -> descriptor.EffectName) descriptors)
          LifeTimeOpt = None
          Definitions = Seq.fold (fun definitions descriptor -> Map.concat definitions descriptor.Definitions) Map.empty descriptors
          Content = Contents (Shift 0.0f, descriptors |> Seq.map (fun descriptor -> descriptor.Content) |> Array.ofSeq) }

    /// Make an effect descriptor.
    let make name lifeTimeOpt definitions content =
        { EffectName = name
          LifeTimeOpt = lifeTimeOpt
          Definitions = definitions
          Content = content }

    /// The default effect descriptor.
    let defaultDescriptor = make Constants.Engine.EffectNameDefault None Map.empty (Contents (Shift 0.0f, [||]))

    /// The empty effect descriptor.
    let empty = make String.Empty None Map.empty (Contents (Shift 0.0f, [||]))

[<RequireQualifiedAccess>]
module EffectSystem =

    /// Evaluates effect descriptors.
    type [<ReferenceEquality>] EffectSystem =
        private
            { EffectLocalTime : GameTime
              EffectDelta : GameTime
              EffectProgressOffset : single
              EffectAbsolute : bool
              EffectRenderType : RenderType
              EffectViews : View List
              EffectEnv : Definitions }

    let rec private addView view effectSystem =
        effectSystem.EffectViews.Add view
        effectSystem

    let rec private selectKeyFrames2<'kf when 'kf :> KeyFrame> localTime playback (keyFrames : 'kf array) =
        match playback with
        | Once ->
            match keyFrames with
            | [||] -> failwithumf ()
            | [|head|] -> (localTime, head, head)
            | _ ->
                let (head, next, tail) = (keyFrames.[0], keyFrames.[1], Array.skip 2 keyFrames)
                if localTime > head.KeyFrameLength then
                    match tail with
                    | [||] -> (head.KeyFrameLength, next, next)
                    | _ -> selectKeyFrames2 (localTime - head.KeyFrameLength) playback (Array.cons next tail)
                else (localTime, head, next)
        | Loop ->
            let totalTime = Array.fold (fun totalTime (keyFrame : 'kf) -> totalTime + keyFrame.KeyFrameLength) GameTime.zero keyFrames 
            if totalTime <> GameTime.zero then
                let moduloTime = localTime % totalTime
                selectKeyFrames2 moduloTime Once keyFrames
            else (GameTime.zero, Array.head keyFrames, Array.head keyFrames)
        | Bounce ->
            let totalTime = Array.fold (fun totalTime (keyFrame : 'kf) -> totalTime + keyFrame.KeyFrameLength) GameTime.zero keyFrames
            if totalTime <> GameTime.zero then
                let moduloTime = localTime % totalTime
                let bouncing = int (localTime / totalTime) % 2 = 1
                let bounceTime = if bouncing then totalTime - moduloTime else moduloTime
                selectKeyFrames2 bounceTime Once keyFrames
            else (GameTime.zero, Array.head keyFrames, Array.head keyFrames)

    let private selectKeyFrames<'kf when 'kf :> KeyFrame> localTime playback (keyFrames : 'kf array) =
        keyFrames |>
        selectKeyFrames2 localTime playback |>
        fun (fst, snd, thd) -> (fst, snd, thd)

    let inline private tween (scale : (^a * single) -> ^a) (value : ^a) (value2 : ^a) progress algorithm =
        match algorithm with
        | Constant ->
            value
        | Linear ->
            value + scale (value2 - value, progress)
        | Random ->
            let rand = Rand.makeFromInt (int ((Math.Max (double progress, 0.000000001)) * double Int32.MaxValue))
            let randValue = fst (Rand.nextSingle rand)
            value + scale (value2 - value, randValue)
        | Chaos ->
            let chaosValue = Gen.randomf
            value + scale (value2 - value, chaosValue)
        | Ease ->
            let progressEase = single (Math.Pow (Math.Sin (Math.PI * double progress * 0.5), 2.0))
            value + scale (value2 - value, progressEase)
        | EaseIn ->
            let progressScaled = float progress * Math.PI * 0.5
            let progressEaseIn = 1.0 + Math.Sin (progressScaled + Math.PI * 1.5)
            value + scale (value2 - value, single progressEaseIn)
        | EaseOut ->
            let progressScaled = float progress * Math.PI * 0.5
            let progressEaseOut = Math.Sin progressScaled
            value + scale (value2 - value, single progressEaseOut)
        | Sin ->
            let progressScaled = float progress * Math.PI * 2.0
            let progressSin = Math.Sin progressScaled
            value + scale (value2 - value, single progressSin)
        | SinScaled scalar ->
            let progressScaled = float progress * Math.PI * 2.0 * float scalar
            let progressSin = Math.Sin progressScaled
            value + scale (value2 - value, single progressSin)
        | Cos ->
            let progressScaled = float progress * Math.PI * 2.0
            let progressCos = Math.Cos progressScaled
            value + scale (value2 - value, single progressCos)
        | CosScaled scalar ->
            let progressScaled = float progress * Math.PI * 2.0 * float scalar
            let progressCos = Math.Cos progressScaled
            value + scale (value2 - value, single progressCos)

    let private applyLogic value value2 applicator =
        match applicator with
        | Or -> value || value2
        | Nor -> not value && not value2
        | Xor -> value <> value2
        | And -> value && value2
        | Nand -> not (value && value2)
        | Equal -> value2

    let inline private applyTween mul div pow mod_ (value : ^a) (value2 : ^a) applicator =
        match applicator with
        | Sum -> value + value2
        | Delta -> value - value2
        | Scalar -> mul (value, value2)
        | Ratio -> div (value, value2)
        | Modulo -> mod_ (value, value2)
        | Pow -> pow (value, value2)
        | Set -> value2

    let private evalInset (celSize : Vector2i) celRun celCount delay playback effectSystem =
        // TODO: make sure Bounce playback works as intended!
        // TODO: stop assuming that animation sheets are fully and evenly populated when flipping!
        let celUnmodulated = int (effectSystem.EffectLocalTime / delay)
        let cel = celUnmodulated % celCount
        let celI = cel % celRun
        let celJ = cel / celRun
        let bouncing =
            match playback with
            | Bounce -> celUnmodulated % (celCount * 2) >= celCount
            | Once | Loop -> false
        let (celI, celJ) =
            if bouncing
            then (celRun - celI, (celRun % celCount) - celJ)
            else (celI, celJ)
        let celX = celI * celSize.X
        let celY = celJ * celSize.Y
        let celPosition = Vector2 (single celX, single celY)
        let celSize = Vector2 (single celSize.X, single celSize.Y)
        Box2 (celPosition, celSize)

    let evalArgument (argument : Argument) : Definition =
        match argument with
        | SymbolicCompressionA resource ->
            { DefinitionParams = [||]; DefinitionBody = SymbolicCompressionA resource }
        | SymbolicCompressionB (SymbolicCompressionA aspect) ->
            { DefinitionParams = [||]; DefinitionBody = SymbolicCompressionB (SymbolicCompressionA aspect) }
        | SymbolicCompressionB (SymbolicCompressionB content) ->
            { DefinitionParams = [||]; DefinitionBody = SymbolicCompressionB (SymbolicCompressionB content) }

    let rec evalResource resource effectSystem : obj AssetTag =
        match resource with
        | Resource (packageName, assetName) -> AssetTag.make<obj> packageName assetName
        | Resource.Expand (definitionName, _) ->
            match Map.tryFind definitionName effectSystem.EffectEnv with
            | Some definition ->
                match definition.DefinitionBody with
                | SymbolicCompressionA resource -> evalResource resource effectSystem
                | _ ->
                    Log.info ("Expected Resource for definition '" + definitionName + ".")
                    asset Assets.Default.PackageName Assets.Default.ImageName
            | None ->
                Log.info ("Could not find definition with name '" + definitionName + "'.")
                asset Assets.Default.PackageName Assets.Default.ImageName

    let rec private iterateViews incrementAspects content slice history effectSystem =
        let effectSystem = { effectSystem with EffectProgressOffset = 0.0f }
        let slice = evalAspects incrementAspects slice effectSystem
        (slice, evalContent content slice history effectSystem)

    and private cycleViews incrementAspects content slice history effectSystem =
        let slice = evalAspects incrementAspects slice effectSystem
        evalContent content slice history effectSystem

    and private evalProgress keyFrameTime keyFrameLength effectSystem =
        let progress = if GameTime.isZero keyFrameLength then 1.0f else single keyFrameTime / single keyFrameLength
        let progress = progress + effectSystem.EffectProgressOffset
        if progress > 1.0f then progress - 1.0f else progress

    and private evalAspect aspect (slice : Slice) effectSystem =
        match aspect with
        | Enabled enabled -> { slice with Enabled = enabled }
        | PositionAbsolute position -> { slice with Position = position }
        | PositionRelative position -> { slice with Position = slice.Position + position }
        | Translation translation ->
            let oriented = Vector3.Transform (translation, slice.Angles.RollPitchYaw)
            let translated = slice.Position + oriented
            { slice with Position = translated }
        | Scale scale -> { slice with Scale = scale }
        | Offset offset -> { slice with Offset = offset }
        | Angles angles -> { slice with Angles = angles }
        | Degrees degrees -> { slice with Angles = Math.degreesToRadians3d degrees }
        | Size size -> { slice with Size = size }
        | Elevation elevation -> { slice with Elevation = elevation }
        | Inset inset -> { slice with Inset = inset }
        | Color color -> { slice with Color = color }
        | Blend blend -> { slice with Blend = blend }
        | Emission emission -> { slice with Emission = emission }
        | Height height -> { slice with Height = height }
        | Flip flip -> { slice with Flip = flip }
        | Brightness brightness -> { slice with Brightness = brightness }
        | AttenuationLinear attenuationLinear -> { slice with AttenuationLinear = attenuationLinear }
        | AttenuationQuadratic attenuationQuadratic -> { slice with AttenuationQuadratic = attenuationQuadratic }
        | Cutoff cutoff -> { slice with Cutoff = cutoff }
        | Volume volume -> { slice with Volume = volume }
        | Enableds (applicator, playback, keyFrames) ->
            if Array.notEmpty keyFrames then
                let (_, keyFrame, _) = selectKeyFrames effectSystem.EffectLocalTime playback keyFrames
                let applied = applyLogic slice.Enabled keyFrame.LogicValue applicator
                { slice with Enabled = applied }
            else slice
        | Positions (applicator, algorithm, playback, keyFrames) ->
            if Array.notEmpty keyFrames then
                let (keyFrameTime, keyFrame, keyFrame2) = selectKeyFrames effectSystem.EffectLocalTime playback keyFrames
                let progress = evalProgress keyFrameTime keyFrame.TweenLength effectSystem
                let tweened = tween (fun (a, b) -> a * b) keyFrame.TweenValue keyFrame2.TweenValue progress algorithm
                let applied = applyTween Vector3.Multiply Vector3.Divide Vector3.Pow Vector3.Modulo slice.Position tweened applicator
                { slice with Position = applied }
            else slice
        | Translations (applicator, algorithm, playback, keyFrames) ->
            if Array.notEmpty keyFrames then
                let (keyFrameTime, keyFrame, keyFrame2) = selectKeyFrames effectSystem.EffectLocalTime playback keyFrames
                let progress = evalProgress keyFrameTime keyFrame.TweenLength effectSystem
                let tweened = tween Vector3.op_Multiply keyFrame.TweenValue keyFrame2.TweenValue progress algorithm
                let oriented = Vector3.Transform (tweened, slice.Angles.RollPitchYaw)
                let applied = applyTween Vector3.Multiply Vector3.Divide Vector3.Pow Vector3.Modulo slice.Position oriented applicator
                { slice with Position = applied }
            else slice
        | Scales (applicator, algorithm, playback, keyFrames) ->
            if Array.notEmpty keyFrames then
                let (keyFrameTime, keyFrame, keyFrame2) = selectKeyFrames effectSystem.EffectLocalTime playback keyFrames
                let progress = evalProgress keyFrameTime keyFrame.TweenLength effectSystem
                let tweened = tween Vector3.op_Multiply keyFrame.TweenValue keyFrame2.TweenValue progress algorithm
                let applied = applyTween Vector3.Multiply Vector3.Divide Vector3.Pow Vector3.Modulo slice.Size tweened applicator
                { slice with Scale = applied }
            else slice
        | Offsets (applicator, algorithm, playback, keyFrames) ->
            if Array.notEmpty keyFrames then
                let (keyFrameTime, keyFrame, keyFrame2) = selectKeyFrames effectSystem.EffectLocalTime playback keyFrames
                let progress = evalProgress keyFrameTime keyFrame.TweenLength effectSystem
                let tweened = tween Vector3.op_Multiply keyFrame.TweenValue keyFrame2.TweenValue progress algorithm
                let applied = applyTween Vector3.Multiply Vector3.Divide Vector3.Pow Vector3.Modulo slice.Position tweened applicator
                { slice with Offset = applied }
            else slice
        | Sizes (applicator, algorithm, playback, keyFrames) ->
            if Array.notEmpty keyFrames then
                let (keyFrameTime, keyFrame, keyFrame2) = selectKeyFrames effectSystem.EffectLocalTime playback keyFrames
                let progress = evalProgress keyFrameTime keyFrame.TweenLength effectSystem
                let tweened = tween Vector3.op_Multiply keyFrame.TweenValue keyFrame2.TweenValue progress algorithm
                let applied = applyTween Vector3.Multiply Vector3.Divide Vector3.Pow Vector3.Modulo slice.Size tweened applicator
                { slice with Size = applied }
            else slice
        | Angleses (applicator, algorithm, playback, keyFrames) ->
            if Array.notEmpty keyFrames then
                let (keyFrameTime, keyFrame, keyFrame2) = selectKeyFrames effectSystem.EffectLocalTime playback keyFrames
                let progress = evalProgress keyFrameTime keyFrame.TweenLength effectSystem
                let tweened = tween Vector3.Multiply keyFrame.TweenValue keyFrame2.TweenValue progress algorithm
                let applied = applyTween Vector3.Multiply Vector3.Divide Vector3.Pow Vector3.Modulo slice.Angles tweened applicator
                { slice with Angles = applied }
            else slice
        | Degreeses (applicator, algorithm, playback, keyFrames) ->
            if Array.notEmpty keyFrames then
                let (keyFrameTime, keyFrame, keyFrame2) = selectKeyFrames effectSystem.EffectLocalTime playback keyFrames
                let progress = evalProgress keyFrameTime keyFrame.TweenLength effectSystem
                let tweened = tween Vector3.Multiply keyFrame.TweenValue keyFrame2.TweenValue progress algorithm
                let applied = applyTween Vector3.Multiply Vector3.Divide Vector3.Pow Vector3.Modulo (Math.radiansToDegrees3d slice.Angles) tweened applicator
                { slice with Angles = Math.degreesToRadians3d applied }
            else slice
        | Elevations (applicator, algorithm, playback, keyFrames) ->
            if Array.notEmpty keyFrames then
                let (keyFrameTime, keyFrame, keyFrame2) = selectKeyFrames effectSystem.EffectLocalTime playback keyFrames
                let progress = evalProgress keyFrameTime keyFrame.TweenLength effectSystem
                let tweened = tween (fun (x, y) -> x * y) keyFrame.TweenValue keyFrame2.TweenValue progress algorithm
                let applied = applyTween (fun (x, y) -> x * y) (fun (x, y) -> x / y) (fun (x, y) -> single (Math.Pow (double x, double y))) (fun (x, y) -> x % y) slice.Elevation tweened applicator
                { slice with Elevation = applied }
            else slice
        | Insets (_, _, playback, keyFrames) ->
            if Array.notEmpty keyFrames then
                let (keyFrameTime, keyFrame, keyFrame2) = selectKeyFrames effectSystem.EffectLocalTime playback keyFrames
                let progress = evalProgress keyFrameTime keyFrame.TweenLength effectSystem
                let applied = if progress < 0.5f then keyFrame.TweenValue else keyFrame2.TweenValue
                { slice with Inset = applied }
            else slice
        | Colors (applicator, algorithm, playback, keyFrames) ->
            if Array.notEmpty keyFrames then
                let (keyFrameTime, keyFrame, keyFrame2) = selectKeyFrames effectSystem.EffectLocalTime playback keyFrames
                let progress = evalProgress keyFrameTime keyFrame.TweenLength effectSystem
                let tweened = tween Vector4.op_Multiply (keyFrame.TweenValue.Vector4) (keyFrame2.TweenValue.Vector4) progress algorithm
                let applied = applyTween Color.Multiply Color.Divide Color.Pow Color.Modulo slice.Color (Nu.Color tweened) applicator
                { slice with Color = applied }
            else slice
        | Emissions (applicator, algorithm, playback, keyFrames) ->
            if Array.notEmpty keyFrames then
                let (keyFrameTime, keyFrame, keyFrame2) = selectKeyFrames effectSystem.EffectLocalTime playback keyFrames
                let progress = evalProgress keyFrameTime keyFrame.TweenLength effectSystem
                let tweened = tween Color.op_Multiply keyFrame.TweenValue keyFrame2.TweenValue progress algorithm
                let applied = applyTween Color.Multiply Color.Divide Color.Pow Color.Modulo slice.Color tweened applicator
                { slice with Emission = applied }
            else slice
        | Heights (applicator, algorithm, playback, keyFrames) ->
            if Array.notEmpty keyFrames then
                let (keyFrameTime, keyFrame, keyFrame2) = selectKeyFrames effectSystem.EffectLocalTime playback keyFrames
                let progress = evalProgress keyFrameTime keyFrame.TweenLength effectSystem
                let tweened = tween (fun (x, y) -> x * y) keyFrame.TweenValue keyFrame2.TweenValue progress algorithm
                let applied = applyTween (fun (x, y) -> x * y) (fun (x, y) -> x / y) (fun (x, y) -> single (Math.Pow (double x, double y))) (fun (x, y) -> x % y) slice.Elevation tweened applicator
                { slice with Height = applied }
            else slice
        | Volumes (applicator, algorithm, playback, keyFrames) ->
            if Array.notEmpty keyFrames then
                let (keyFrameTime, keyFrame, keyFrame2) = selectKeyFrames effectSystem.EffectLocalTime playback keyFrames
                let progress = evalProgress keyFrameTime keyFrame.TweenLength effectSystem
                let tweened = tween (fun (x, y) -> x * y) keyFrame.TweenValue keyFrame2.TweenValue progress algorithm
                let applied = applyTween (fun (x, y) -> x * y) (fun (x, y) -> x / y) (fun (x, y) -> single (Math.Pow (double x, double y))) (fun (x, y) -> x % y) slice.Volume tweened applicator
                { slice with Volume = applied }
            else slice
        | Aspect.Expand (definitionName, _) ->
            match Map.tryFind definitionName effectSystem.EffectEnv with
            | Some definition ->
                match definition.DefinitionBody with
                | SymbolicCompressionB (SymbolicCompressionA aspect) -> evalAspect aspect slice effectSystem
                | _ -> Log.info ("Expected Aspect for definition '" + definitionName + "'."); slice
            | None -> Log.info ("Could not find definition with name '" + definitionName + "'."); slice
        | Aspects aspects ->
            Array.fold (fun slice aspect -> evalAspect aspect slice effectSystem) slice aspects

    and private evalAspects aspects slice effectSystem =
        Array.fold (fun slice aspect -> evalAspect aspect slice effectSystem) slice aspects

    and private evalExpand definitionName arguments slice history effectSystem =
        match Map.tryFind definitionName effectSystem.EffectEnv with
        | Some definition ->
            match definition.DefinitionBody with
            |  SymbolicCompressionB (SymbolicCompressionB content) ->
                let localDefinitions = Array.map evalArgument arguments
                match (try Array.zip definition.DefinitionParams localDefinitions |> Some with _ -> None) with
                | Some localDefinitionEntries ->
                    let effectSystem = { effectSystem with EffectEnv = Map.addMany localDefinitionEntries effectSystem.EffectEnv }
                    evalContent content slice history effectSystem
                | None -> Log.info "Wrong number of arguments provided to ExpandContent."; effectSystem
            | _ -> Log.info ("Expected Content for definition '" + definitionName + "'."); effectSystem
        | None -> Log.info ("Could not find definition with name '" + definitionName + "'."); effectSystem

    and private evalStaticSprite resource aspects content (slice : Slice) history effectSystem =

        // pull image from resource
        let image = evalResource resource effectSystem

        // eval aspects
        let slice = evalAspects aspects slice effectSystem

        // build sprite views
        let effectSystem =
            if slice.Enabled then
                let mutable transform = Transform.makeIntuitive slice.Position slice.Scale slice.Offset slice.Size slice.Angles slice.Elevation effectSystem.EffectAbsolute slice.Centered
                let spriteView =
                    Render2d (transform.Elevation, transform.Horizon, AssetTag.generalize image,
                        RenderSprite
                            { Transform = transform
                              InsetOpt = if slice.Inset.Equals box2Zero then ValueNone else ValueSome slice.Inset
                              Image = AssetTag.specialize<Image> image
                              Color = slice.Color
                              Blend = slice.Blend
                              Emission = slice.Emission
                              Flip = slice.Flip })
                addView spriteView effectSystem
            else effectSystem

        // build implicitly mounted content
        evalContent content slice history effectSystem

    and private evalAnimatedSprite resource (celSize : Vector2i) celRun celCount delay playback aspects content slice history effectSystem =

        // pull image from resource
        let image = evalResource resource effectSystem

        // eval aspects
        let slice = evalAspects aspects slice effectSystem

        // ensure valid data
        if GameTime.notZero delay && celRun <> 0 then

            // compute cel
            let cel = int (effectSystem.EffectLocalTime / delay)

            // eval inset
            let inset = evalInset celSize celRun celCount delay playback effectSystem

            // build animated sprite views
            let effectSystem =
                if  slice.Enabled &&
                    not (playback = Once && cel >= celCount) then
                    let mutable transform = Transform.makeIntuitive slice.Position slice.Scale slice.Offset slice.Size slice.Angles slice.Elevation effectSystem.EffectAbsolute slice.Centered
                    let animatedSpriteView =
                        Render2d (transform.Elevation, transform.Horizon, AssetTag.generalize image,
                            RenderSprite
                                { Transform = transform
                                  InsetOpt = ValueSome inset
                                  Image = AssetTag.specialize<Image> image
                                  Color = slice.Color
                                  Blend = slice.Blend
                                  Emission = slice.Emission
                                  Flip = slice.Flip })
                    addView animatedSpriteView effectSystem
                else effectSystem

            // build implicitly mounted content
            evalContent content slice history effectSystem

        // abandon evaL
        else effectSystem

    and private evalTextSprite resource text aspects content slice history effectSystem =

        // pull font from resource
        let font = evalResource resource effectSystem

        // eval aspects
        let slice = evalAspects aspects slice effectSystem

        // build text views
        let effectSystem =
            if slice.Enabled then
                let mutable transform = Transform.makeIntuitive slice.Position slice.Scale slice.Offset slice.Size slice.Angles slice.Elevation effectSystem.EffectAbsolute slice.Centered
                let textView =
                    Render2d (transform.Elevation, transform.Horizon, font,
                        RenderText
                            { Transform = transform
                              Text = text
                              Font = AssetTag.specialize<Font> font
                              Color = slice.Color
                              Justification = Justified (JustifyCenter, JustifyMiddle) })
                addView textView effectSystem
            else effectSystem

        // build implicitly mounted content
        evalContent content slice history effectSystem

    and private evalBillboard resourceAlbedo resourceMetallic resourceRoughness resourceAmbientOcclusion resourceEmission resourceNormal resourceHeight minFilterOpt magFilterOpt aspects content (slice : Slice) history effectSystem =

        // pull image from resource
        let imageAlbedo = evalResource resourceAlbedo effectSystem
        let imageMetallic = evalResource resourceMetallic effectSystem
        let imageRoughness = evalResource resourceRoughness effectSystem
        let imageAmbientOcclusion = evalResource resourceAmbientOcclusion effectSystem
        let imageEmission = evalResource resourceEmission effectSystem
        let imageNormal = evalResource resourceNormal effectSystem
        let imageHeight = evalResource resourceHeight effectSystem

        // eval aspects
        let slice = evalAspects aspects slice effectSystem

        // build model views
        let effectSystem =
            if slice.Enabled then
                let imageAlbedo = AssetTag.specialize<Image> imageAlbedo
                let imageMetallic = AssetTag.specialize<Image> imageMetallic
                let imageRoughness = AssetTag.specialize<Image> imageRoughness
                let imageAmbientOcclusion = AssetTag.specialize<Image> imageAmbientOcclusion
                let imageEmission = AssetTag.specialize<Image> imageEmission
                let imageNormal = AssetTag.specialize<Image> imageNormal
                let imageHeight = AssetTag.specialize<Image> imageHeight
                let affineMatrix = Matrix4x4.CreateFromTrs (slice.Position, slice.Angles.RollPitchYaw, slice.Scale)
                let insetOpt = if slice.Inset.Equals box2Zero then None else Some slice.Inset
                let properties =
                    { AlbedoOpt = Some slice.Color
                      MetallicOpt = None
                      RoughnessOpt = None
                      AmbientOcclusionOpt = None
                      EmissionOpt = Some slice.Emission.R
                      HeightOpt = Some slice.Height
                      InvertRoughnessOpt = None }
                let modelView =
                    Render3d
                        (RenderBillboard
                            { Absolute = effectSystem.EffectAbsolute
                              ModelMatrix = affineMatrix
                              InsetOpt = insetOpt
                              MaterialProperties = properties
                              AlbedoImage = imageAlbedo
                              MetallicImage = imageMetallic
                              RoughnessImage = imageRoughness
                              AmbientOcclusionImage = imageAmbientOcclusion
                              EmissionImage = imageEmission
                              NormalImage = imageNormal
                              HeightImage = imageHeight
                              MinFilterOpt = minFilterOpt
                              MagFilterOpt = magFilterOpt
                              RenderType = effectSystem.EffectRenderType })
                addView modelView effectSystem
            else effectSystem

        // build implicitly mounted content
        evalContent content slice history effectSystem

    and private evalStaticModel resource aspects content (slice : Slice) history effectSystem =

        // pull image from resource
        let staticModel = evalResource resource effectSystem

        // eval aspects
        let slice = evalAspects aspects slice effectSystem

        // build model views
        let effectSystem =
            if slice.Enabled then
                let staticModel = AssetTag.specialize<StaticModel> staticModel
                let affineMatrix = Matrix4x4.CreateFromTrs (slice.Position, slice.Angles.RollPitchYaw, slice.Scale)
                let insetOpt = if slice.Inset.Equals box2Zero then None else Some slice.Inset
                let properties =
                    { AlbedoOpt = Some slice.Color
                      MetallicOpt = None
                      RoughnessOpt = None
                      AmbientOcclusionOpt = None
                      EmissionOpt = Some slice.Emission.R
                      HeightOpt = Some slice.Height
                      InvertRoughnessOpt = None }
                let modelView =
                    Render3d
                        (RenderStaticModel
                            { Absolute = effectSystem.EffectAbsolute
                              ModelMatrix = affineMatrix
                              InsetOpt = insetOpt
                              MaterialProperties = properties
                              RenderType = effectSystem.EffectRenderType
                              StaticModel = staticModel })
                addView modelView effectSystem
            else effectSystem

        // build implicitly mounted content
        evalContent content slice history effectSystem

    and private evalLight3d lightType aspects content (slice : Slice) history effectSystem =

        // eval aspects
        let slice = evalAspects aspects slice effectSystem

        // build model views
        let effectSystem =
            if slice.Enabled then
                let modelView =
                    Render3d
                        (RenderLight3d
                            { Origin = slice.Position
                              Direction = Vector3.Transform (v3Up, Quaternion.CreateFromYawPitchRoll (slice.Angles.Z, slice.Angles.Y, slice.Angles.X))
                              Color = slice.Color
                              Brightness = slice.Brightness
                              AttenuationLinear = slice.AttenuationLinear
                              AttenuationQuadratic = slice.AttenuationQuadratic
                              Cutoff = slice.Cutoff
                              LightType = lightType })
                addView modelView effectSystem
            else effectSystem

        // build implicitly mounted content
        evalContent content slice history effectSystem

    and private evalSoundEffect resource aspects content slice history effectSystem =

        // pull sound from resource
        let sound = evalResource resource effectSystem

        // eval aspects
        let slice = evalAspects aspects slice effectSystem

        // build sound views
        let effectSystem =
            if slice.Enabled
            then addView (PlaySound (slice.Volume, AssetTag.specialize<Sound> sound)) effectSystem
            else effectSystem

        // build implicitly mounted content
        evalContent content slice history effectSystem

    and private evalMount shift aspects content (slice : Slice) history effectSystem =
        let slice = { slice with Elevation = slice.Elevation + shift }
        let slice = evalAspects aspects slice effectSystem
        evalContent content slice history effectSystem

    and private evalRepeat shift repetition incrementAspects content (slice : Slice) history effectSystem =

        // eval repeat either as iterative or cycling
        let slice = { slice with Elevation = slice.Elevation + shift }
        match repetition with

        // eval iterative repeat
        | Iterate count ->
            Array.fold
                (fun (slice, effectSystem) _ ->
                    let (slice, effectSystem) = iterateViews incrementAspects content slice history effectSystem
                    (slice, effectSystem))
                (slice, effectSystem)
                [|0 .. count - 1|] |>
            snd

        // eval cycling repeat
        | Cycle count ->
            Array.fold
                (fun effectSystem i ->
                    let effectSystem = { effectSystem with EffectProgressOffset = 1.0f / single count * single i }
                    cycleViews incrementAspects content slice history effectSystem)
                effectSystem
                [|0 .. count - 1|]

    and private evalTag name aspects content (slice : Slice) history effectSystem =

        // eval aspects
        let slice = evalAspects aspects slice effectSystem

        // build tag view
        let effectSystem =
            if slice.Enabled then
                let tagView = Nu.Tag (name, slice)
                addView tagView effectSystem
            else effectSystem

        // build implicitly mounted content
        evalContent content slice history effectSystem

    and private evalEmit shift rate emitterAspects aspects content history effectSystem =
        let effectSystem =
            Seq.foldi
                (fun i effectSystem (slice : Slice) ->
                    let oldEffectTime = effectSystem.EffectLocalTime
                    let timePassed = effectSystem.EffectDelta * GameTime.make (int64 i) (single i)
                    let slice = { slice with Elevation = slice.Elevation + shift }
                    let slice = evalAspects emitterAspects slice { effectSystem with EffectLocalTime = effectSystem.EffectLocalTime - timePassed }
                    let emitCountLastFrame = single (effectSystem.EffectLocalTime - timePassed - effectSystem.EffectDelta) * rate
                    let emitCountThisFrame = single (effectSystem.EffectLocalTime - timePassed) * rate
                    let emitCount = int emitCountThisFrame - int emitCountLastFrame
                    let effectSystem = { effectSystem with EffectLocalTime = timePassed }
                    let effectSystem =
                        Array.fold
                            (fun effectSystem _ ->
                                let slice = evalAspects aspects slice effectSystem
                                if slice.Enabled
                                then evalContent content slice history effectSystem
                                else effectSystem)
                            effectSystem
                            [|0 .. emitCount - 1|]
                    { effectSystem with EffectLocalTime = oldEffectTime })
                effectSystem
                history
        effectSystem

    and private evalSegment start stop content slice history effectSystem =
        if  effectSystem.EffectLocalTime >= start &&
            effectSystem.EffectLocalTime < stop then
            let effectSystem = { effectSystem with EffectLocalTime = effectSystem.EffectLocalTime - start }
            let effectSystem = evalContent content slice history effectSystem
            let effectSystem = { effectSystem with EffectLocalTime = effectSystem.EffectLocalTime + start }
            effectSystem
        else effectSystem

    and private evalContents shift contents slice history effectSystem =
        let slice = { slice with Slice.Elevation = slice.Elevation + shift }
        evalContents3 contents slice history effectSystem

    and private evalContent content slice history effectSystem =
        match content with
        | Nil ->
            effectSystem
        | StaticSprite (resource, aspects, content) ->
            evalStaticSprite resource aspects content slice history effectSystem
        | AnimatedSprite (resource, celSize, celRun, celCount, delay, playback, aspects, content) ->
            evalAnimatedSprite resource celSize celRun celCount delay playback aspects content slice history effectSystem
        | TextSprite (resource, text, aspects, content) ->
            evalTextSprite resource text aspects content slice history effectSystem
        | Billboard (resourceAlbedo, resourceMetallic, resourceRoughness, resourceAmbientOcclusion, resourceEmission, resourceNormal, resourceHeight, minFilterOpt, magFilterOpt, aspects, content) ->
            evalBillboard resourceAlbedo resourceMetallic resourceRoughness resourceAmbientOcclusion resourceEmission resourceNormal resourceHeight minFilterOpt magFilterOpt aspects content slice history effectSystem
        | StaticModel (resource, aspects, content) ->
            evalStaticModel resource aspects content slice history effectSystem
        | Light3d (lightType, aspects, content) ->
            evalLight3d lightType aspects content slice history effectSystem
        | SoundEffect (resource, aspects, content) ->
            evalSoundEffect resource aspects content slice history effectSystem
        | Mount (Shift shift, aspects, content) ->
            evalMount shift aspects content slice history effectSystem
        | Repeat (Shift shift, repetition, incrementAspects, content) ->
            evalRepeat shift repetition incrementAspects content slice history effectSystem
        | Emit (Shift shift, Rate rate, emitterAspects, aspects, content) ->
            evalEmit shift rate emitterAspects aspects content history effectSystem
        | Tag (name, aspects, content) ->
            evalTag name aspects content slice history effectSystem
        | Delay (delay, content) ->
            evalSegment delay GameTime.MaxValue content slice history effectSystem
        | Segment (start, stop, content) ->
            evalSegment start stop content slice history effectSystem
        | Contents (Shift shift, contents) ->
            evalContents shift contents slice history effectSystem
        | Expand (definitionName, arguments) ->
            evalExpand definitionName arguments slice history effectSystem

    and private evalContents3 contents slice history effectSystem =
        Array.fold
            (fun effectSystem content -> evalContent content slice history effectSystem)
            effectSystem
            contents

    let private release effectSystem =
        let views = Views (effectSystem.EffectViews.ToArray ())
        let effectSystem = { effectSystem with EffectViews = List<View> () }
        (views, effectSystem)

    let eval descriptor slice history effectSystem =
        let alive =
            match descriptor.LifeTimeOpt with
            | Some lifetime -> lifetime <= GameTime.zero || effectSystem.EffectLocalTime <= lifetime
            | None -> true
        if alive then
            let effectSystem = { effectSystem with EffectEnv = Map.concat effectSystem.EffectEnv descriptor.Definitions }
            try let effectSystem = evalContent descriptor.Content slice history effectSystem
                release effectSystem
            with exn ->
                let prettyPrinter = (SyntaxAttribute.defaultValue typeof<EffectDescriptor>).PrettyPrinter
                let effectStr = PrettyPrinter.prettyPrint (scstring descriptor) prettyPrinter
                Log.debug ("Error in effect descriptor:\n" + effectStr + "\n due to: " + scstring exn)
                release effectSystem
        else release effectSystem

    let make localTime delta absolute renderType globalEnv =
        { EffectLocalTime = localTime
          EffectDelta = delta
          EffectProgressOffset = 0.0f
          EffectAbsolute = absolute
          EffectRenderType = renderType
          EffectViews = List<View> ()
          EffectEnv = globalEnv }

/// Evaluates effect descriptors.
type EffectSystem = EffectSystem.EffectSystem