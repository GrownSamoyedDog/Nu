﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2023.

namespace Nu
open System
open System.Numerics
open Prime

[<AutoOpen>]
module WorldPhysics =

    type World with

        static member internal getPhysicsEngine2d world =
            world.Subsystems.PhysicsEngine2d

        static member internal getPhysicsEngine3d world =
            world.Subsystems.PhysicsEngine3d

        /// Localize a body shape to a specific size.
        static member localizeBodyShape (size : Vector3) (bodyShape : BodyShape) =
            Physics.localizeBodyShape size bodyShape

        /// Handle a 2d physics message in the world.
        static member handlePhysicsMessage2d (message : PhysicsMessage) world =
            let world =
                match message with
                | CreateBodyMessage message ->
                    let eventTrace = EventTrace.debug "World" "handlePhysicsMessage2d" "" EventTrace.empty
                    World.publishPlus message.BodyId Game.Handle.BodyAddingEvent eventTrace Game.Handle false false world
                | CreateBodiesMessage message ->
                    let eventTrace = EventTrace.debug "World" "handlePhysicsMessage2d" "" EventTrace.empty
                    List.fold (fun world (bodyProperties : BodyProperties) ->
                        let bodyId = { BodySource = message.BodySource; BodyIndex = bodyProperties.BodyIndex }
                        World.publishPlus bodyId Game.Handle.BodyAddingEvent eventTrace Game.Handle false false world)
                        world message.BodiesProperties
                | DestroyBodyMessage message ->
                    let eventTrace = EventTrace.debug "World" "handlePhysicsMessage2d" "" EventTrace.empty
                    let world = World.publishPlus { BodyId = message.BodyId } Game.Handle.BodySeparationImplicitEvent eventTrace Game.Handle false false world
                    let world = World.publishPlus message.BodyId Game.Handle.BodyRemovingEvent eventTrace Game.Handle false false world
                    world
                | DestroyBodiesMessage message ->
                    let eventTrace = EventTrace.debug "World" "handlePhysicsMessage2d" "" EventTrace.empty
                    List.fold (fun world (bodyId : BodyId) ->
                        let world = World.publishPlus { BodyId = bodyId } Game.Handle.BodySeparationImplicitEvent eventTrace Game.Handle false false world
                        let world = World.publishPlus bodyId Game.Handle.BodyRemovingEvent eventTrace Game.Handle false false world
                        world)
                        world message.BodyIds
                | _ -> world
            (World.getPhysicsEngine2d world).HandleMessage message
            world

        /// Send multiple 2d physics messages to the world.
        static member handlePhysicsMessages2d (messages : PhysicsMessage seq) world =
            Seq.fold (fun world message -> World.handlePhysicsMessage2d message world) world messages

        /// Send a 3d physics message in the world.
        static member handlePhysicsMessage3d (message : PhysicsMessage) world =
            let world =
                match message with
                | CreateBodyMessage message ->
                    let eventTrace = EventTrace.debug "World" "handlePhysicsMessage3d" "" EventTrace.empty
                    World.publishPlus message.BodyId Game.Handle.BodyAddingEvent eventTrace Game.Handle false false world
                | CreateBodiesMessage message ->
                    let eventTrace = EventTrace.debug "World" "handlePhysicsMessage3d" "" EventTrace.empty
                    List.fold (fun world (bodyProperties : BodyProperties) ->
                        let bodyId = { BodySource = message.BodySource; BodyIndex = bodyProperties.BodyIndex }
                        World.publishPlus bodyId Game.Handle.BodyAddingEvent eventTrace Game.Handle false false world)
                        world message.BodiesProperties
                | DestroyBodyMessage message ->
                    let eventTrace = EventTrace.debug "World" "handlePhysicsMessage3d" "" EventTrace.empty
                    let world = World.publishPlus { BodyId = message.BodyId } Game.Handle.BodySeparationImplicitEvent eventTrace Game.Handle false false world
                    let world = World.publishPlus message.BodyId Game.Handle.BodyRemovingEvent eventTrace Game.Handle false false world
                    world
                | DestroyBodiesMessage message ->
                    let eventTrace = EventTrace.debug "World" "handlePhysicsMessage3d" "" EventTrace.empty
                    List.fold (fun world (bodyId : BodyId) ->
                        let world = World.publishPlus { BodyId = bodyId } Game.Handle.BodySeparationImplicitEvent eventTrace Game.Handle false false world
                        let world = World.publishPlus bodyId Game.Handle.BodyRemovingEvent eventTrace Game.Handle false false world
                        world)
                        world message.BodyIds
                | _ -> world
            (World.getPhysicsEngine3d world).HandleMessage message
            world

        /// Send multiple 3d physics messages to the world.
        static member handlePhysicsMessages3d (messages : PhysicsMessage seq) world =
            Seq.fold (fun world message -> World.handlePhysicsMessage3d message world) world messages

        /// Check that the world contains a body with the given physics id.
        static member getBodyExists bodyId world =
            world.Subsystems.PhysicsEngine3d.GetBodyExists bodyId ||
            world.Subsystems.PhysicsEngine2d.GetBodyExists bodyId

        /// Get the contact normals of the body with the given physics id.
        static member getBodyContactNormals bodyId world =
            if world.Subsystems.PhysicsEngine3d.GetBodyExists bodyId then
                world.Subsystems.PhysicsEngine3d.GetBodyContactNormals bodyId
            elif world.Subsystems.PhysicsEngine2d.GetBodyExists bodyId then
                world.Subsystems.PhysicsEngine2d.GetBodyContactNormals bodyId
            else
                Log.info ("Body for '" + scstring bodyId + "' not found.")
                []

        /// Get the linear velocity of the body with the given physics id.
        static member getBodyLinearVelocity bodyId world =
            if world.Subsystems.PhysicsEngine3d.GetBodyExists bodyId then
                world.Subsystems.PhysicsEngine3d.GetBodyLinearVelocity bodyId
            elif world.Subsystems.PhysicsEngine2d.GetBodyExists bodyId then
                world.Subsystems.PhysicsEngine2d.GetBodyLinearVelocity bodyId
            else
                Log.info ("Body for '" + scstring bodyId + "' not found.")
                v3Zero

        /// Get the angular velocity of the body with the given physics id.
        static member getBodyAngularVelocity bodyId world =
            if world.Subsystems.PhysicsEngine3d.GetBodyExists bodyId then
                world.Subsystems.PhysicsEngine3d.GetBodyAngularVelocity bodyId
            elif world.Subsystems.PhysicsEngine2d.GetBodyExists bodyId then
                world.Subsystems.PhysicsEngine2d.GetBodyAngularVelocity bodyId
            else
                Log.info ("Body for '" + scstring bodyId + "' not found.")
                v3Zero

        /// Get the contact normals where the body with the given physics id is touching the ground.
        static member getBodyToGroundContactNormals bodyId world =
            if world.Subsystems.PhysicsEngine3d.GetBodyExists bodyId then
                world.Subsystems.PhysicsEngine3d.GetBodyToGroundContactNormals bodyId
            elif world.Subsystems.PhysicsEngine2d.GetBodyExists bodyId then
                world.Subsystems.PhysicsEngine2d.GetBodyToGroundContactNormals bodyId
            else
                Log.info ("Body for '" + scstring bodyId + "' not found.")
                []

        /// Get a contact normal where the body with the given physics id is touching the ground (if one exists).
        static member getBodyToGroundContactNormalOpt bodyId world =
            if world.Subsystems.PhysicsEngine3d.GetBodyExists bodyId then
                world.Subsystems.PhysicsEngine3d.GetBodyToGroundContactNormalOpt bodyId
            elif world.Subsystems.PhysicsEngine2d.GetBodyExists bodyId then
                world.Subsystems.PhysicsEngine2d.GetBodyToGroundContactNormalOpt bodyId
            else
                Log.info ("Body for '" + scstring bodyId + "' not found.")
                None

        /// Get a contact tangent where the body with the given physics id is touching the ground (if one exists).
        static member getBodyToGroundContactTangentOpt bodyId world =
            if world.Subsystems.PhysicsEngine3d.GetBodyExists bodyId then
                world.Subsystems.PhysicsEngine3d.GetBodyToGroundContactTangentOpt bodyId
            elif world.Subsystems.PhysicsEngine2d.GetBodyExists bodyId then
                world.Subsystems.PhysicsEngine2d.GetBodyToGroundContactTangentOpt bodyId
            else
                Log.info ("Body for '" + scstring bodyId + "' not found.")
                None

        /// Check that the body with the given physics id is on the ground.
        static member getBodyGrounded bodyId world =
            if world.Subsystems.PhysicsEngine3d.GetBodyExists bodyId then
                world.Subsystems.PhysicsEngine3d.IsBodyOnGround bodyId
            elif world.Subsystems.PhysicsEngine2d.GetBodyExists bodyId then
                world.Subsystems.PhysicsEngine2d.IsBodyOnGround bodyId
            else
                Log.info ("Body for '" + scstring bodyId + "' not found.")
                false

        /// Send a physics message to create a physics body.
        static member createBody is2d bodyId (bodyProperties : BodyProperties) world =
            let createBodyMessage = CreateBodyMessage { BodyId = bodyId; BodyProperties = bodyProperties }
            if not is2d
            then World.handlePhysicsMessage3d createBodyMessage world
            else World.handlePhysicsMessage2d createBodyMessage world

        /// Send a physics message to create several physics bodies.
        static member createBodies is2d bodySource bodiesProperties world =
            let createBodiesMessage = CreateBodiesMessage { BodySource = bodySource; BodiesProperties = bodiesProperties }
            if not is2d
            then World.handlePhysicsMessage3d createBodiesMessage world
            else World.handlePhysicsMessage2d createBodiesMessage world

        /// Send a physics message to destroy a physics body.
        static member destroyBody is2d bodyId world =
            let destroyBodyMessage = DestroyBodyMessage { BodyId = bodyId }
            if not is2d
            then World.handlePhysicsMessage3d destroyBodyMessage world
            else World.handlePhysicsMessage2d destroyBodyMessage world

        /// Send a physics message to destroy several physics bodies.
        static member destroyBodies is2d bodyIds world =
            let destroyBodiesMessage = DestroyBodiesMessage { BodyIds = bodyIds }
            if not is2d
            then World.handlePhysicsMessage3d destroyBodiesMessage world
            else World.handlePhysicsMessage2d destroyBodiesMessage world

        /// Send a physics message to create a physics joint.
        static member createJoint is2d jointSource jointProperties world =
            let createJointMessage = CreateJointMessage { JointSource = jointSource; JointProperties = jointProperties }
            if not is2d
            then World.handlePhysicsMessage3d createJointMessage world
            else World.handlePhysicsMessage2d createJointMessage world

        /// Send a physics message to create physics joints.
        static member createJoints is2d jointSource jointsProperties world =
            let createJointsMessage = CreateJointsMessage { JointsSource = jointSource; JointsProperties = jointsProperties }
            if not is2d
            then World.handlePhysicsMessage3d createJointsMessage world
            else World.handlePhysicsMessage2d createJointsMessage world

        /// Send a physics message to destroy a physics joint.
        static member destroyJoint is2d jointId world =
            let destroyJointMessage = DestroyJointMessage { JointId = jointId }
            if not is2d
            then World.handlePhysicsMessage3d destroyJointMessage world
            else World.handlePhysicsMessage2d destroyJointMessage world

        /// Send a physics message to destroy physics joints.
        static member destroyJoints is2d jointIds world =
            let destroyJointsMessage = DestroyJointsMessage { JointIds = jointIds }
            if not is2d
            then World.handlePhysicsMessage3d destroyJointsMessage world
            else World.handlePhysicsMessage2d destroyJointsMessage world

        /// Send a physics message to set the enabled-ness of a body with the given physics id.
        static member setBodyEnabled enabled bodyId world =
            let setBodyEnabledMessage = SetBodyEnabledMessage { BodyId = bodyId; Enabled = enabled }
            let world = World.handlePhysicsMessage3d setBodyEnabledMessage world
            let world = World.handlePhysicsMessage2d setBodyEnabledMessage world
            world

        /// Send a physics message to set the position of a body with the given physics id.
        static member setBodyCenter center bodyId world =
            let setBodyCenterMessage = SetBodyCenterMessage { BodyId = bodyId; Center = center }
            let world = World.handlePhysicsMessage3d setBodyCenterMessage world
            let world = World.handlePhysicsMessage2d setBodyCenterMessage world
            world

        /// Send a physics message to set the rotation of a body with the given physics id.
        static member setBodyRotation rotation bodyId world =
            let setBodyRotationMessage = SetBodyRotationMessage { BodyId = bodyId; Rotation = rotation }
            let world = World.handlePhysicsMessage3d setBodyRotationMessage world
            let world = World.handlePhysicsMessage2d setBodyRotationMessage world
            world

        /// Send a physics message to set the linear velocity of a body with the given physics id.
        static member setBodyLinearVelocity linearVelocity bodyId world =
            let setBodyLinearVelocityMessage = SetBodyLinearVelocityMessage { BodyId = bodyId; LinearVelocity = linearVelocity }
            let world = World.handlePhysicsMessage3d setBodyLinearVelocityMessage world
            let world = World.handlePhysicsMessage2d setBodyLinearVelocityMessage world
            world

        /// Send a physics message to set the angular velocity of a body with the given physics id.
        static member setBodyAngularVelocity angularVelocity bodyId world =
            let setBodyAngularVelocityMessage = SetBodyAngularVelocityMessage { BodyId = bodyId; AngularVelocity = angularVelocity }
            let world = World.handlePhysicsMessage3d setBodyAngularVelocityMessage world
            let world = World.handlePhysicsMessage2d setBodyAngularVelocityMessage world
            world

        /// Send a physics message to apply linear impulse to a body with the given physics id.
        static member applyBodyLinearImpulse linearImpulse offset bodyId world =
            let applyBodyLinearImpulseMessage = ApplyBodyLinearImpulseMessage { BodyId = bodyId; LinearImpulse = linearImpulse; Offset = offset }
            let world = World.handlePhysicsMessage3d applyBodyLinearImpulseMessage world
            let world = World.handlePhysicsMessage2d applyBodyLinearImpulseMessage world
            world

        /// Send a physics message to apply angular impulse to a body with the given physics id.
        static member applyBodyAngularImpulse angularImpulse bodyId world =
            let applyBodyAngularImpulseMessage = ApplyBodyAngularImpulseMessage { BodyId = bodyId; AngularImpulse = angularImpulse }
            let world = World.handlePhysicsMessage3d applyBodyAngularImpulseMessage world
            let world = World.handlePhysicsMessage2d applyBodyAngularImpulseMessage world
            world

        /// Send a physics message to apply force to a body with the given physics id.
        static member applyBodyForce force offset bodyId world =
            let applyBodyForceMessage = ApplyBodyForceMessage { BodyId = bodyId; Force = force; Offset = offset }
            let world = World.handlePhysicsMessage3d applyBodyForceMessage world
            let world = World.handlePhysicsMessage2d applyBodyForceMessage world
            world

        /// Send a physics message to apply torque to a body with the given physics id.
        static member applyBodyTorque torque bodyId world =
            let applyBodyTorqueMessage = ApplyBodyTorqueMessage { BodyId = bodyId; Torque = torque }
            let world = World.handlePhysicsMessage3d applyBodyTorqueMessage world
            let world = World.handlePhysicsMessage2d applyBodyTorqueMessage world
            world
            
        static member private setBodyObservableInternal allowInternalIndexing observable (bodyId : BodyId) world =
            if allowInternalIndexing || bodyId.BodyIndex <> Constants.Physics.InternalIndex then
                let setBodyObservableMessage = SetBodyObservableMessage { BodyId = bodyId; Observable = observable }
                let world = World.handlePhysicsMessage3d setBodyObservableMessage world
                let world = World.handlePhysicsMessage2d setBodyObservableMessage world
                world
            else
                Log.debug "Set the observability of an internally indexed body from outside the engine is prohibited."
                world

        /// Send a physics message to set the observability of a body.
        /// Disabling observability where it's not needed can significantly increase performance.
        static member setBodyObservable observable bodyId world =
            World.setBodyObservableInternal false observable bodyId world

        static member internal updateBodyObservable subscribing (bodySource : Entity) world =
            let observable =
                subscribing ||
                let collisionEventAddress = atooa bodySource.BodyCollisionEvent
                match (World.getSubscriptions world).TryGetValue collisionEventAddress with
                | (true, subscriptions) -> OMap.notEmpty subscriptions
                | (false, _) ->
                    let separationEventAddress = atooa bodySource.BodySeparationExplicitEvent
                    match (World.getSubscriptions world).TryGetValue separationEventAddress with
                    | (true, subscriptions) -> OMap.notEmpty subscriptions
                    | (false, _) -> false
            let bodyId = { BodySource = bodySource; BodyIndex = Constants.Physics.InternalIndex }
            World.setBodyObservableInternal true observable bodyId world