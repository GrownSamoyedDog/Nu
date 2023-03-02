﻿namespace Nelmish
open Prime
open Nu
open Nu.Declarative

// this is our Elm-style model type
type Model =
    int

// this is our Elm-style message type
type Message =
    | Decrement
    | Increment
    | Reset
    interface Nu.Message

// this is our Elm-style game dispatcher
type NelmishDispatcher () =
    inherit GameDispatcher<Model, Message, Command> (0) // initial model value

    // here we handle the Elm-style messages
    override this.Message (model, message, _, _) =
        match message with
        | Decrement -> just (model - 1)
        | Increment -> just (model + 1)
        | Reset -> just 0

    // here we describe the content of the game including its one screen, one group, three
    // button entities, and one text control
    override this.Content (model, _) =
        [Content.screen "Screen" Vanilla []
            [Content.group "Group" []
                [Content.button "Decrement"
                    [Entity.Position == v3 -128.0f 96.0f 0.0f
                     Entity.Text == "-"
                     Entity.ClickEvent => Decrement]
                 Content.button "Increment"
                    [Entity.Position == v3 128.0f 96.0f 0.0f
                     Entity.Text == "+"
                     Entity.ClickEvent => Increment]
                 Content.text "Counter"
                    [Entity.Position == v3 0.0f 0.0f 0.0f
                     Entity.Text := string model
                     Entity.Justification == Justified (JustifyCenter, JustifyMiddle)]
                 if model <> 0 then
                    Content.button "Reset"
                       [Entity.Position == v3 0.0f -96.0f 0.0f
                        Entity.Text == "Reset"
                        Entity.ClickEvent => Reset]]]]