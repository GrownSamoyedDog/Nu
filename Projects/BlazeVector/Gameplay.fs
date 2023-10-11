﻿namespace BlazeVector
open System
open Prime
open Nu
open Nu.Declarative
open BlazeVector

[<AutoOpen>]
module Gameplay =

    type GameplayState =
        | Playing
        | Quitting
        | Quit

    type [<SymbolicExpansion>] Gameplay =
        { State : GameplayState
          Score : int }

    type GameplayMessage =
        | Score of int
        | StartQutting
        | FinishQuitting
        interface Message

    type GameplayCommand =
        | CreateSections
        | DestroySections
        | UpdateEye
        interface Command

    type Screen with
        member this.GetGameplay world = this.GetModelGeneric<Gameplay> world
        member this.SetGameplay value world = this.SetModelGeneric<Gameplay> value world
        member this.Gameplay = this.ModelGeneric<Gameplay> ()

    type GameplayDispatcher () =
        inherit ScreenDispatcher<Gameplay, GameplayMessage, GameplayCommand> ({ State = Quit; Score = 0 })

        static let [<Literal>] SectionCount = 16

        override this.Initialize (_, _) =
            [Screen.SelectEvent => CreateSections
             Screen.DeselectingEvent => DestroySections
             Screen.PostUpdateEvent => UpdateEye
             Simulants.GameplayGuiQuit.ClickEvent => StartQutting
             for i in 0 .. dec SectionCount do (Simulants.GameplaySectionEntities i).DieEvent => Score 100]

        override this.Message (gameplay, message, _, _) =
            match message with
            | Score score -> just { gameplay with Score = gameplay.Score + score }
            | StartQutting -> just { gameplay with State = Quitting }
            | FinishQuitting -> just { gameplay with State = Quit }

        override this.Command (_, command, _, world) =

            match command with
            | CreateSections ->

                // create stage sections from random section files
                let world = (world, [0 .. dec SectionCount]) ||> List.fold (fun world sectionIndex ->

                    // load a random section from file (except the first section which is always 0)
                    let sectionName = "Section" + string sectionIndex
                    let sectionFilePath =
                        if sectionIndex = 0
                        then Assets.Gameplay.SectionFilePaths.[0]
                        else Gen.randomItem Assets.Gameplay.SectionFilePaths
                    let (section, world) =
                        World.readGroupFromFile sectionFilePath (Some sectionName) Simulants.Gameplay world

                    // shift all entities in the loaded section so that they go after the previously loaded section
                    let sectionXShift = 2048.0f * single sectionIndex
                    let sectionEntities = World.getEntitiesFlattened section world
                    Seq.fold (fun world (sectionEntity : Entity) ->
                        sectionEntity.SetPosition (sectionEntity.GetPosition world + v3 sectionXShift 0.0f 0.0f) world)
                        world sectionEntities)

                // fin
                just world

            | DestroySections ->

                // destroy stage sections that were created from section files
                let world = (world, [0 .. dec SectionCount]) ||> List.fold (fun world sectionIndex ->

                    // destroy section
                    let sectionName = "Section" + string sectionIndex
                    let group = Simulants.Gameplay / sectionName
                    World.destroyGroup group world)

                // quitting finished
                withSignal FinishQuitting world

            | UpdateEye ->

                // update eye to look at player while game is advancing
                if world.Advancing then
                    let playerPosition = Simulants.GameplayScenePlayer.GetPosition world
                    let playerSize = Simulants.GameplayScenePlayer.GetSize world
                    let eyeCenter = World.getEyeCenter2d world
                    let eyeSize = World.getEyeSize2d world
                    let eyeCenter = v2 (playerPosition.X + playerSize.X * 0.5f + eyeSize.X * 0.33f) eyeCenter.Y
                    let world = World.setEyeCenter2d eyeCenter world
                    just world
                else just world

        override this.Content (gameplay, _) =

            [// the gui group
             Content.group Simulants.GameplayGui.Name []
                 [Content.text Simulants.GameplayGuiScore.Name
                    [Entity.Position == v3 392.0f 232.0f 0.0f
                     Entity.Elevation == 10.0f
                     Entity.Text := "Score: " + string gameplay.Score]
                  Content.button Simulants.GameplayGuiQuit.Name
                    [Entity.Position == v3 336.0f -216.0f 0.0f
                     Entity.Elevation == 10.0f
                     Entity.Text == "Quit"
                     Entity.ClickEvent => StartQutting]]

             // the scene group while playing
             match gameplay.State with
             | Playing | Quitting ->
                Content.group Simulants.GameplayScene.Name []
                    [Content.entity<PlayerDispatcher> Simulants.GameplayScenePlayer.Name
                        [Entity.Position == v3 -876.0f -127.6805f 0.0f
                         Entity.Elevation == 1.0f
                         Entity.DieEvent => StartQutting]]
             | Quit -> ()]