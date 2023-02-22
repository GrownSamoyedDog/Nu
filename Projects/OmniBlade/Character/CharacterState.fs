﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2020.

namespace OmniBlade
open System
open Prime
open Nu
open OmniBlade

type [<SymbolicExpansion>] AutoBattle =
    { AutoTarget : CharacterIndex
      AutoTechOpt : TechType option
      ChargeTech : bool }

type [<SymbolicExpansion>] CharacterState =
    { ArchetypeType : ArchetypeType
      ExpPoints : int
      AbsorbCreep : single
      WeaponOpt : WeaponType option
      ArmorOpt : ArmorType option
      Accessories : AccessoryType list
      HitPoints : int
      TechPoints : int
      Statuses : Map<StatusType, single>
      Defending : bool // also applies a perhaps stackable buff for attributes such as countering or magic power depending on class
      Charging : bool
      TechProbabilityOpt : single option
      GoldPrize : int
      ExpPrize : int
      ItemPrizeOpt : ItemType option }

    member this.Level = Algorithms.expPointsToLevel this.ExpPoints
    member this.Healthy = this.HitPoints > 0
    member this.Wounded = this.HitPoints <= 0
    member this.HitPointsMax = Algorithms.hitPointsMax this.ArmorOpt this.ArchetypeType this.Level
    member this.TechPointsMax = Algorithms.techPointsMax this.ArmorOpt this.ArchetypeType this.Level
    member this.Power = Algorithms.power this.WeaponOpt this.Statuses this.ArchetypeType this.Level
    member this.Magic isMetal = Algorithms.magic isMetal this.WeaponOpt this.Statuses this.ArchetypeType this.Level
    member this.Shield effectType = Algorithms.shield effectType this.AbsorbCreep this.Accessories this.Statuses this.ArchetypeType this.Level
    member this.Defense = Algorithms.defense this.Accessories this.Statuses this.ArchetypeType this.Level
    member this.Absorb = Algorithms.absorb this.AbsorbCreep this.Accessories this.Statuses this.ArchetypeType this.Level
    member this.AffinityOpt = Algorithms.affinityOpt this.Accessories this.ArchetypeType this.Level
    member this.Immunities = Algorithms.immunities this.Accessories this.ArchetypeType this.Level
    member this.Enchantments = Algorithms.enchantments this.Accessories this.ArchetypeType this.Level
    member this.Techs = Algorithms.techs this.ArchetypeType this.Level
    member this.ChargeTechs = Algorithms.chargeTechs this.ArchetypeType this.Level
    member this.Stature = match Map.tryFind this.ArchetypeType Data.Value.Archetypes with Some archetypeData -> archetypeData.Stature | None -> NormalStature

    static member getAttackResult effectType (source : CharacterState) (target : CharacterState) =
        let power = source.Power
        let shield = target.Shield effectType
        let defendingScalar = if target.Defending then Constants.Battle.DefendingScalar else 1.0f
        let damage = single (power - shield) * defendingScalar |> int |> max 1
        damage

    static member burndownStatuses burndown state =
        let statuses =
            Map.fold (fun statuses status burndown2 ->
                let burndown3 = burndown2 - burndown
                if burndown3 <= 0.0f
                then Map.remove status statuses
                else Map.add status burndown3 statuses)
                Map.empty
                state.Statuses
        { state with Statuses = statuses }

    static member updateHitPoints updater (state : CharacterState) =
        let hitPoints = updater state.HitPoints
        let hitPoints = max 0 hitPoints
        let hitPoints = min state.HitPointsMax hitPoints
        { state with
            HitPoints = hitPoints
            Statuses = if hitPoints = 0 then Map.empty else state.Statuses }

    static member updateTechPoints updater state =
        let techPoints = updater state.TechPoints
        let techPoints = max 0 techPoints
        let techPoints = min state.TechPointsMax techPoints
        { state with TechPoints = techPoints }

    static member updateExpPoints updater state =
        let expPoints = updater state.ExpPoints
        let expPoints = max 0 expPoints
        { state with ExpPoints = expPoints }

    static member tryGetTechRandom (state : CharacterState) =
        Gen.randomItemOpt state.Techs

    static member getPoiseType state =
        if state.Defending then Defending
        elif state.Charging then Charging
        else Poising

    static member make (characterData : CharacterData) hitPoints techPoints expPoints weaponOpt armorOpt accessories =
        let archetypeType = characterData.ArchetypeType
        let absorbCreep = characterData.AbsorbCreep
        let level = Algorithms.expPointsToLevel expPoints
        let enchantments = Algorithms.enchantments accessories archetypeType level
        let statuses = Map.ofSeqBy (fun status -> (status, Single.MaxValue)) enchantments
        let characterState =
            { ArchetypeType = archetypeType
              ExpPoints = expPoints
              AbsorbCreep = absorbCreep
              WeaponOpt = weaponOpt
              ArmorOpt = armorOpt
              Accessories = accessories
              HitPoints = hitPoints
              TechPoints = techPoints
              Statuses = statuses
              Defending = false
              Charging = false
              TechProbabilityOpt = characterData.TechProbabilityOpt
              GoldPrize = Algorithms.goldPrize archetypeType characterData.GoldScalar level
              ExpPrize = Algorithms.expPrize archetypeType characterData.ExpScalar level
              ItemPrizeOpt = Algorithms.itemPrizeOpt archetypeType level }
        characterState

    static member empty =
        let characterState =
            { ArchetypeType = Apprentice
              ExpPoints = 0
              AbsorbCreep = 1.0f
              WeaponOpt = None
              ArmorOpt = None
              Accessories = []
              HitPoints = 1
              TechPoints = 0
              Statuses = Map.empty
              Defending = false
              Charging = false
              TechProbabilityOpt = None
              GoldPrize = 0
              ExpPrize = 0
              ItemPrizeOpt = None }
        characterState