﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2020.

namespace OmniBlade
open System
open Prime
open Nu
open Nu.Declarative
open OmniBlade

[<AutoOpen>]
module CharacterDispatcher =

    type Entity with
        member this.GetCharacter world = this.GetModelGeneric<Character> world
        member this.SetCharacter value world = this.SetModelGeneric<Character> value world
        member this.Character = this.ModelGeneric<Character> ()

    type CharacterDispatcher () =
        inherit EntityDispatcher2d<Character, Message, Command> (true, Character.empty)

        static let getAfflictionInsetOpt (character : Character) world =
            if not character.Wounding then
                let statuses = character.Statuses
                let celYOpt =
                    if character.Wounded then None
                    elif Map.containsKey Confuse statuses then Some 3
                    elif Map.containsKey Sleep statuses then Some 2
                    elif Map.containsKey Silence statuses then Some 1
                    elif Map.containsKey Poison statuses then Some 0
                    elif Map.exists (fun key _ -> match key with Time false -> true | _ -> false) statuses then Some 4
                    elif Map.exists (fun key _ -> match key with Power (false, _) -> true | _ -> false) statuses then Some 5
                    elif Map.exists (fun key _ -> match key with Magic (false, _) -> true | _ -> false) statuses then Some 6
                    elif Map.exists (fun key _ -> match key with Shield (false, _) -> true | _ -> false) statuses then Some 7
                    else None
                match celYOpt with
                | Some afflictionY ->
                    let time = World.getUpdateTime world
                    let afflictionX = time / 8L % 8L |> int
                    let afflictionPosition = v2 (single afflictionX * Constants.Battle.AfflictionCelSize.X) (single afflictionY * Constants.Battle.AfflictionCelSize.Y)
                    let inset = box2 afflictionPosition Constants.Battle.AfflictionCelSize
                    Some inset
                | None -> None
            else None

        static let getChargeOrbInsetOpt (character : Character) world =
            if not character.Wounding then
                let celXOpt =
                    match (character.ConjureChargeOpt, character.TechChargeOpt |> Option.map Triple.snd) with
                    | (Some chargeAmount, _)
                    | (_, Some chargeAmount) ->
                        if chargeAmount < 3 then Some 0
                        elif chargeAmount < 6 then Some 1
                        elif chargeAmount < 9 then Some 2
                        elif chargeAmount < 12 then Some 3
                        else World.getUpdateTime world / 12L % 4L + 4L |> int |> Some
                    | (None, None) -> None
                match celXOpt with
                | Some celX ->
                    let chargeOrbPosition = v2 (single celX * Constants.Battle.ChargeOrbCelSize.X) 0.0f
                    let inset = box2 chargeOrbPosition Constants.Battle.ChargeOrbCelSize
                    Some inset
                | None -> None
            else None

        override this.Initialize (character, _) =
            [Entity.Presence == Omnipresent
             Entity.Perimeter := character.Perimeter
             Entity.Elevation == Constants.Battle.ForegroundElevation]

        override this.View (character, entity, world) =
            if entity.GetVisible world then
                let time = World.getUpdateTime world
                let mutable transform = entity.GetTransform world
                let perimeter = transform.Perimeter
                let characterView =
                    Render2d (transform.Elevation, transform.Horizon, AssetTag.generalize character.AnimationSheet,
                        SpriteDescriptor
                            { Transform = transform
                              InsetOpt = ValueSome (Character.getAnimationInset time character)
                              Image = character.AnimationSheet
                              Color = Character.getAnimationColor time character
                              Blend = Transparent
                              Glow = Character.getAnimationGlow time character
                              Flip = FlipNone })
                let afflictionView =
                    match getAfflictionInsetOpt character world with
                    | Some afflictionInset ->
                        let afflictionImage = Assets.Battle.AfflictionsAnimationSheet
                        let afflictionPosition =
                            match character.Stature with
                            | SmallStature | NormalStature ->
                                perimeter.Min + perimeter.Size - Constants.Battle.AfflictionSize
                            | LargeStature ->
                                perimeter.Min + perimeter.Size - Constants.Battle.AfflictionSize.MapY((*) 0.5f)
                            | BossStature ->
                                perimeter.Min + perimeter.Size - Constants.Battle.AfflictionSize.MapX((*) 2.0f).MapY((*) 1.75f)
                        let mutable afflictionTransform = Transform.makeDefault false
                        afflictionTransform.Position <- afflictionPosition
                        afflictionTransform.Size <- Constants.Battle.AfflictionSize
                        afflictionTransform.Elevation <- transform.Elevation + 0.1f
                        Render2d (afflictionTransform.Elevation, afflictionTransform.Horizon, AssetTag.generalize afflictionImage,
                            SpriteDescriptor
                                { Transform = afflictionTransform
                                  InsetOpt = ValueSome afflictionInset
                                  Image = afflictionImage
                                  Color = Color.One
                                  Blend = Transparent
                                  Glow = Color.Zero
                                  Flip = FlipNone })
                    | None -> View.empty
                let chargeOrbView =
                    match getChargeOrbInsetOpt character world with
                    | Some chargeOrbInset ->
                        let chargeOrbImage = Assets.Battle.ChargeOrbAnimationSheet
                        let chargeOrbPosition =
                            match character.Stature with
                            | SmallStature | NormalStature ->
                                perimeter.Min + perimeter.Size - Constants.Battle.ChargeOrbSize.MapX((*) 1.5f)
                            | LargeStature ->
                                perimeter.Min + perimeter.Size - Constants.Battle.ChargeOrbSize.MapX((*) 1.5f).MapY((*) 0.5f)
                            | BossStature ->
                                perimeter.Min + perimeter.Size - Constants.Battle.ChargeOrbSize.MapX((*) 2.5f).MapY((*) 1.75f)
                        let mutable chargeOrbTransform = Transform.makeDefault false
                        chargeOrbTransform.Position <- chargeOrbPosition
                        chargeOrbTransform.Size <- Constants.Battle.ChargeOrbSize
                        chargeOrbTransform.Elevation <- transform.Elevation + 0.1f
                        Render2d (chargeOrbTransform.Elevation, chargeOrbTransform.Horizon, AssetTag.generalize chargeOrbImage,
                            SpriteDescriptor
                                { Transform = chargeOrbTransform
                                  InsetOpt = ValueSome chargeOrbInset
                                  Image = chargeOrbImage
                                  Color = Color.One
                                  Blend = Transparent
                                  Glow = Color.Zero
                                  Flip = FlipNone })
                    | None -> View.empty
                Views [|characterView; afflictionView; chargeOrbView|]
            else View.empty