﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2023.

namespace Nu
open System
open System.Collections.Generic
open System.Numerics
open BulletSharp
open Prime
open Nu

/// Tracks Bullet physics bodies by their BodyIds.
type internal BulletBodyDictionary = OrderedDictionary<BodyId, Vector3 option * RigidBody>

/// Tracks Bullet physics ghosts by their BodyIds.
type internal BulletGhostDictionary = OrderedDictionary<BodyId, GhostObject>

/// Tracks Bullet physics collision objects by their BodyIds.
type internal BulletObjectDictionary = OrderedDictionary<BodyId, CollisionObject>

/// Tracks Bullet physics constraints by their BodyIds.
type internal BulletConstraintDictionary = OrderedDictionary<JointId, TypedConstraint>

/// The BulletPhysics 3d implementation of PhysicsEngine.
/// TODO: only record the collisions for bodies that have event subscriptions associated with them.
type [<ReferenceEquality>] BulletPhysicsEngine =
    private
        { PhysicsContext : DynamicsWorld
          Constraints : BulletConstraintDictionary
          Bodies : BulletBodyDictionary
          Ghosts : BulletGhostDictionary
          Objects : BulletObjectDictionary
          mutable Collisions : SegmentedDictionary<BodyId * BodyId, Vector3>
          CollisionConfiguration : CollisionConfiguration
          PhysicsDispatcher : Dispatcher
          BroadPhaseInterface : BroadphaseInterface
          ConstraintSolver : ConstraintSolver
          HacdCache : Hacds
          PhysicsMessages : PhysicsMessage UList
          IntegrationMessages : IntegrationMessage List
          mutable RebuildingHack : bool }

    static member private handleCollision physicsEngine (bodyId : BodyId) (bodyId2 : BodyId) normal =
        let bodyCollisionMessage =
            { BodyShapeSource = { BodyId = bodyId; ShapeIndex = 0 }
              BodyShapeSource2 = { BodyId = bodyId2; ShapeIndex = 0 }
              Normal = normal }
        let integrationMessage = BodyCollisionMessage bodyCollisionMessage
        physicsEngine.IntegrationMessages.Add integrationMessage
    
    static member private handleSeparation physicsEngine (bodyId : BodyId) (bodyId2 : BodyId) =
        let bodySeparationMessage =
            { BodyShapeSource = { BodyId = bodyId; ShapeIndex = 0 }
              BodyShapeSource2 = { BodyId = bodyId2; ShapeIndex = 0 }}
        let integrationMessage = BodySeparationMessage bodySeparationMessage
        physicsEngine.IntegrationMessages.Add integrationMessage

    static member private configureBodyShapeProperties (_ : BodyProperties) (_ : BodyShapeProperties option) (_ : ConvexInternalShape) =
        () // NOTE: cannot configure bullet shapes on a per-shape basis.

    static member private configureCollisionObjectProperties (bodyProperties : BodyProperties) (object : CollisionObject) =
        match (bodyProperties.Sleeping, bodyProperties.Enabled) with
        | (true, true) -> object.ActivationState <- ActivationState.IslandSleeping
        | (true, false) -> object.ActivationState <- ActivationState.DisableSimulation
        | (false, true) -> object.ActivationState <- ActivationState.ActiveTag
        | (false, false) -> object.ActivationState <- ActivationState.DisableSimulation
        object.Friction <- bodyProperties.Friction
        object.Restitution <- bodyProperties.Restitution
        match bodyProperties.CollisionDetection with
        | Discontinuous ->
            object.CcdMotionThreshold <- 0.0f
            object.CcdSweptSphereRadius <- 0.0f
        | Continuous continuous ->
            object.CcdMotionThreshold <- continuous.MotionThreshold
            object.CcdSweptSphereRadius <- continuous.SweptSphereRadius
        match bodyProperties.BodyType with
        | Static ->
            object.CollisionFlags <- object.CollisionFlags ||| CollisionFlags.StaticObject
            object.CollisionFlags <- object.CollisionFlags &&& ~~~CollisionFlags.KinematicObject
        | Dynamic ->
            object.CollisionFlags <- object.CollisionFlags &&& ~~~CollisionFlags.StaticObject
            object.CollisionFlags <- object.CollisionFlags &&& ~~~CollisionFlags.KinematicObject
        | Kinematic ->
            object.CollisionFlags <- object.CollisionFlags ||| CollisionFlags.KinematicObject
            object.CollisionFlags <- object.CollisionFlags &&& ~~~CollisionFlags.StaticObject
        //object.IsBullet <- bodyProperties.Bullet // TODO: see if we can find a Bullet equivalent of this to Aether.

    static member private configureBodyProperties (bodyProperties : BodyProperties) (body : RigidBody) gravity =
        BulletPhysicsEngine.configureCollisionObjectProperties bodyProperties body
        body.MotionState.WorldTransform <- Matrix4x4.CreateFromTrs (bodyProperties.Center, bodyProperties.Rotation, v3One)
        if bodyProperties.SleepingAllowed // TODO: see if we can find a more reliable way to disable sleeping.
        then body.SetSleepingThresholds (0.8f, 1.0f) // TODO: move to constants?
        else body.SetSleepingThresholds (0.0f, 0.0f)
        body.LinearVelocity <- bodyProperties.LinearVelocity
        body.LinearFactor <- if bodyProperties.BodyType = Static then v3Zero else v3One
        body.AngularVelocity <- bodyProperties.AngularVelocity
        body.AngularFactor <- if bodyProperties.BodyType = Static then v3Zero else bodyProperties.AngularFactor
        body.SetDamping (bodyProperties.LinearDamping, bodyProperties.AngularDamping)
        body.Gravity <- match bodyProperties.GravityOverrideOpt with Some gravityOverride -> gravityOverride | None -> gravity

    static member private attachBodyBox bodySource (bodyProperties : BodyProperties) (bodyBox : BodyBox) (compoundShape : CompoundShape) mass inertia =
        let box = new BoxShape (bodyBox.Size * 0.5f)
        BulletPhysicsEngine.configureBodyShapeProperties bodyProperties bodyBox.PropertiesOpt box
        box.UserObject <-
            { BodyId = { BodySource = bodySource; BodyIndex = bodyProperties.BodyIndex }
              ShapeIndex = match bodyBox.PropertiesOpt with Some p -> p.ShapeIndex | None -> 0 }
        let mass' =
            match bodyProperties.Substance with
            | Density density ->
                let volume = bodyBox.Size.X * bodyBox.Size.Y * bodyBox.Size.Z
                volume * density
            | Mass mass -> mass
        let inertia' = box.CalculateLocalInertia mass'
        compoundShape.AddChildShape (Option.defaultValue m4Identity bodyBox.TransformOpt, box)
        (mass + mass', inertia + inertia')

    static member private attachBodySphere bodySource (bodyProperties : BodyProperties) (bodySphere : BodySphere) (compoundShape : CompoundShape) mass inertia =
        let sphere = new SphereShape (bodySphere.Radius)
        BulletPhysicsEngine.configureBodyShapeProperties bodyProperties bodySphere.PropertiesOpt sphere
        sphere.UserObject <-
            { BodyId = { BodySource = bodySource; BodyIndex = bodyProperties.BodyIndex }
              ShapeIndex = match bodySphere.PropertiesOpt with Some p -> p.ShapeIndex | None -> 0 }
        let mass' =
            match bodyProperties.Substance with
            | Density density ->
                let volume = 4.0f / 3.0f * MathF.PI * pown bodySphere.Radius 3
                volume * density
            | Mass mass -> mass
        let inertia' = sphere.CalculateLocalInertia mass'
        compoundShape.AddChildShape (Option.defaultValue m4Identity bodySphere.TransformOpt, sphere)
        (mass + mass', inertia + inertia')

    static member private attachBodyCapsule bodySource (bodyProperties : BodyProperties) (bodyCapsule : BodyCapsule) (compoundShape : CompoundShape) mass inertia =
        let capsule = new CapsuleShape (bodyCapsule.Radius, bodyCapsule.Height)
        BulletPhysicsEngine.configureBodyShapeProperties bodyProperties bodyCapsule.PropertiesOpt capsule
        capsule.UserObject <-
            { BodyId = { BodySource = bodySource; BodyIndex = bodyProperties.BodyIndex }
              ShapeIndex = match bodyCapsule.PropertiesOpt with Some p -> p.ShapeIndex | None -> 0 }
        let mass' =
            match bodyProperties.Substance with
            | Density density ->
                let volume = MathF.PI * pown bodyCapsule.Radius 2 * (4.0f / 3.0f * bodyCapsule.Radius * bodyCapsule.Height)
                volume * density
            | Mass mass -> mass
        let inertia' = capsule.CalculateLocalInertia mass'
        compoundShape.AddChildShape (Option.defaultValue m4Identity bodyCapsule.TransformOpt, capsule)
        (mass + mass', inertia + inertia')

    static member private attachBodyBoxRounded bodySource (bodyProperties : BodyProperties) (bodyBoxRounded : BodyBoxRounded) (compoundShape : CompoundShape) mass inertia =
        Log.debugOnce "Rounded box not yet implemented via BulletPhysicsEngine; creating a normal box instead."
        let bodyBox = { Size = bodyBoxRounded.Size; TransformOpt = bodyBoxRounded.TransformOpt; PropertiesOpt = bodyBoxRounded.PropertiesOpt }
        BulletPhysicsEngine.attachBodyBox bodySource bodyProperties bodyBox compoundShape mass inertia

    static member private attachBodyConvexHull bodySource (bodyProperties : BodyProperties) (bodyConvexHull : BodyConvexHull) (compoundShape : CompoundShape) mass inertia =
        let hull = new ConvexHullShape (bodyConvexHull.Vertices)
        hull.OptimizeConvexHull ()
        BulletPhysicsEngine.configureBodyShapeProperties bodyProperties bodyConvexHull.PropertiesOpt hull
        hull.UserObject <-
            { BodyId = { BodySource = bodySource; BodyIndex = bodyProperties.BodyIndex }
              ShapeIndex = match bodyConvexHull.PropertiesOpt with Some p -> p.ShapeIndex | None -> 0 }
        let mass' =
            match bodyProperties.Substance with
            | Density density ->
                // NOTE: we approximate volume with the volume of a bounding box.
                // TODO: use a more accurate volume calculation.
                let mutable min = v3Zero
                let mutable max = v3Zero
                hull.GetAabb (m4Identity, &min, &max)
                let box = box3 min max
                let volume = box.Width * box.Height * box.Depth
                volume * density
            | Mass mass -> mass
        let inertia' = hull.CalculateLocalInertia mass'
        compoundShape.AddChildShape (Option.defaultValue m4Identity bodyConvexHull.TransformOpt, hull)
        (mass + mass', inertia + inertia')

    static member private attachBodyStaticModel bodySource (bodyProperties : BodyProperties) (bodyStaticModel : BodyStaticModel) (compoundShape : CompoundShape) mass inertia (hacdCache : Hacds) =
        if bodyStaticModel.Verticeses.Length = bodyStaticModel.Indiceses.Length then
            Seq.fold (fun (mass, inertia) i ->
                let vertices = bodyStaticModel.Verticeses.[i]
                let indices = bodyStaticModel.Indiceses.[i]
                let bodyStaticModelSurface = { Vertices = vertices; Indices = indices; SurfaceIndex = i; StaticModel = bodyStaticModel.StaticModel; TransformOpt = bodyStaticModel.TransformOpt; PropertiesOpt = bodyStaticModel.PropertiesOpt }
                let (mass', inertia') = BulletPhysicsEngine.attachBodyStaticModelSurface bodySource bodyProperties bodyStaticModelSurface compoundShape mass inertia hacdCache
                (mass + mass', inertia + inertia'))
                (mass, inertia)
                [0 .. dec bodyStaticModel.Verticeses.Length]
        else failwith "Uneven number of vertices entries and indices entries for BodyStaticModel."

    static member private attachBodyStaticModelSurface bodySource (bodyProperties : BodyProperties) (bodyStaticModelSurface : BodyStaticModelSurface) (compoundShape : CompoundShape) mass inertia (hacdCache : Hacds) =
        let hacd =
            let hacdId = { SurfaceIndex = bodyStaticModelSurface.SurfaceIndex; StaticModel = bodyStaticModelSurface.StaticModel }
            match hacdCache.TryGetValue hacdId with
            | (true, clusters) -> clusters
            | (false, _) ->
                use hacdBuilder =
                    new Hacd
                        (VerticesPerConvexHull = 100, // maximum number
                         CompacityWeight = 0.1,
                         VolumeWeight = 0,
                         NClusters = 2,
                         Concavity = 100,
                         AddExtraDistPoints = false,
                         AddFacesPoints = false,
                         AddNeighboursDistPoints = false)
                hacdBuilder.SetPoints bodyStaticModelSurface.Vertices
                hacdBuilder.SetTriangles bodyStaticModelSurface.Indices
                let hacd =
                    if hacdBuilder.Compute () then
                        let clusters = List ()
                        for i in 0 .. dec hacdBuilder.NClusters do
                            let trianglesLen = hacdBuilder.GetNTrianglesCH i * 3
                            if trianglesLen > 0 then
                                let triangles = Array.zeroCreate<int> trianglesLen
                                let nVertices = hacdBuilder.GetNPointsCH i
                                let points = Array.zeroCreate<double> (nVertices * 3)                
                                if hacdBuilder.GetCH (i, points, triangles) then
                                    let vertices = Array.zeroCreate<Vector3> nVertices
                                    for j in  0 .. dec nVertices do
                                        let k = j * 3
                                        let vertex = v3 (single points.[k]) (single points.[k + 1]) (single points.[k + 2])
                                        vertices.[j] <- vertex
                                    clusters.Add vertices
                        clusters
                    else List ()
                hacdCache.Add (hacdId, hacd)
                hacd
        Seq.fold (fun (mass, inertia) cluster ->
            let bodyConvexHull = { Vertices = cluster; TransformOpt = bodyStaticModelSurface.TransformOpt; PropertiesOpt = bodyStaticModelSurface.PropertiesOpt }
            let (mass', inertia') = BulletPhysicsEngine.attachBodyConvexHull bodySource bodyProperties bodyConvexHull compoundShape mass inertia
            (mass + mass', inertia + inertia'))
            (mass, inertia)
            hacd

    static member private attachBodyShapes bodySource bodyProperties bodyShapes compoundShape mass inertia hacdCache =
        List.fold (fun (mass, inertia) bodyShape ->
            let (mass', inertia') = BulletPhysicsEngine.attachBodyShape bodySource bodyProperties bodyShape compoundShape mass inertia hacdCache
            (mass + mass', inertia + inertia'))
            (mass, inertia)
            bodyShapes

    static member private attachBodyShape bodySource bodyProperties bodyShape compoundShape masses inertia hacdCache =
        match bodyShape with
        | BodyEmpty -> (masses, inertia)
        | BodyBox bodyBox -> BulletPhysicsEngine.attachBodyBox bodySource bodyProperties bodyBox compoundShape masses inertia
        | BodySphere bodySphere -> BulletPhysicsEngine.attachBodySphere bodySource bodyProperties bodySphere compoundShape masses inertia
        | BodyCapsule bodyCapsule -> BulletPhysicsEngine.attachBodyCapsule bodySource bodyProperties bodyCapsule compoundShape masses inertia
        | BodyBoxRounded bodyBoxRounded -> BulletPhysicsEngine.attachBodyBoxRounded bodySource bodyProperties bodyBoxRounded compoundShape masses inertia
        | BodyConvexHull bodyConvexHull -> BulletPhysicsEngine.attachBodyConvexHull bodySource bodyProperties bodyConvexHull compoundShape masses inertia
        | BodyStaticModel bodyStaticModel -> BulletPhysicsEngine.attachBodyStaticModel bodySource bodyProperties bodyStaticModel compoundShape masses inertia hacdCache
        | BodyStaticModelSurface bodyStaticModelSurface -> BulletPhysicsEngine.attachBodyStaticModelSurface bodySource bodyProperties bodyStaticModelSurface compoundShape masses inertia hacdCache
        | BodyShapes bodyShapes -> BulletPhysicsEngine.attachBodyShapes bodySource bodyProperties bodyShapes compoundShape masses inertia hacdCache

    static member private createBody3 attachBodyShape (bodyId : BodyId) (bodyProperties : BodyProperties) physicsEngine =
        let (shape, mass, inertia) =
            let compoundShape = new CompoundShape ()
            let (mass, inertia) = attachBodyShape bodyProperties compoundShape 0.0f v3Zero
            if compoundShape.ChildList.Count = 1 && not (compoundShape.ChildList.[0].ChildShape :? CompoundShape)
            then (compoundShape.ChildList.[0].ChildShape, mass, inertia)
            else (compoundShape, mass, inertia)
        let userIndex = if bodyId.BodyIndex = Constants.Physics.InternalIndex then -1 else 1
        if not bodyProperties.Sensor then
            let motionState = new DefaultMotionState (Matrix4x4.CreateFromTrs (bodyProperties.Center, bodyProperties.Rotation, v3One))
            let constructionInfo = new RigidBodyConstructionInfo (mass, motionState, shape, inertia)
            let body = new RigidBody (constructionInfo)
            body.UserObject <- bodyId
            body.UserIndex <- userIndex
            BulletPhysicsEngine.configureBodyProperties bodyProperties body physicsEngine.PhysicsContext.Gravity
            physicsEngine.PhysicsContext.AddRigidBody (body, bodyProperties.CollisionCategories, bodyProperties.CollisionMask)
            if physicsEngine.Bodies.TryAdd (bodyId, (bodyProperties.GravityOverrideOpt, body))
            then physicsEngine.Objects.Add (bodyId, body)
            else Log.debug ("Could not add body for '" + scstring bodyId + "'.")
        else
            let ghost = new GhostObject ()
            ghost.CollisionFlags <- ghost.CollisionFlags &&& ~~~CollisionFlags.NoContactResponse
            ghost.UserObject <- bodyId
            ghost.UserIndex <- userIndex
            BulletPhysicsEngine.configureCollisionObjectProperties bodyProperties ghost
            physicsEngine.PhysicsContext.AddCollisionObject (ghost, bodyProperties.CollisionCategories, bodyProperties.CollisionMask)
            if physicsEngine.Ghosts.TryAdd (bodyId, ghost)
            then physicsEngine.Objects.Add (bodyId, ghost)
            else Log.debug ("Could not add body for '" + scstring bodyId + "'.")

    static member private createBody4 bodyShape (bodyId : BodyId) bodyProperties physicsEngine =
        BulletPhysicsEngine.createBody3 (fun ps cs mass inertia ->
            BulletPhysicsEngine.attachBodyShape bodyId.BodySource ps bodyShape cs mass inertia physicsEngine.HacdCache)
            bodyId bodyProperties physicsEngine

    static member private createBody (createBodyMessage : CreateBodyMessage) physicsEngine =
        let bodyId = createBodyMessage.BodyId
        let bodyProperties = createBodyMessage.BodyProperties
        BulletPhysicsEngine.createBody4 bodyProperties.BodyShape bodyId bodyProperties physicsEngine

    static member private createBodies (createBodiesMessage : CreateBodiesMessage) physicsEngine =
        List.iter
            (fun (bodyProperties : BodyProperties) ->
                let createBodyMessage =
                    { BodyId = { BodySource = createBodiesMessage.BodySource; BodyIndex = bodyProperties.BodyIndex }
                      BodyProperties = bodyProperties }
                BulletPhysicsEngine.createBody createBodyMessage physicsEngine)
            createBodiesMessage.BodiesProperties

    static member private destroyBody (destroyBodyMessage : DestroyBodyMessage) physicsEngine =
        let bodyId = destroyBodyMessage.BodyId
        match physicsEngine.Objects.TryGetValue bodyId with
        | (true, object) ->
            match object with
            | :? RigidBody as body ->
                physicsEngine.Objects.Remove bodyId |> ignore
                physicsEngine.Bodies.Remove bodyId |> ignore
                physicsEngine.PhysicsContext.RemoveRigidBody body
            | :? GhostObject as ghost ->
                physicsEngine.Objects.Remove bodyId |> ignore
                physicsEngine.Ghosts.Remove bodyId |> ignore
                physicsEngine.PhysicsContext.RemoveCollisionObject ghost
            | _ -> ()
        | (false, _) -> ()

    static member private destroyBodies (destroyBodiesMessage : DestroyBodiesMessage) physicsEngine =
        List.iter (fun bodyId ->
            BulletPhysicsEngine.destroyBody { BodyId = bodyId } physicsEngine)
            destroyBodiesMessage.BodyIds

    static member private createJoint (createJointMessage : CreateJointMessage) physicsEngine =
        let jointProperties = createJointMessage.JointProperties
        let jointId = { JointSource = createJointMessage.JointSource; JointIndex = jointProperties.JointIndex }
        match jointProperties.JointDevice with
        | JointEmpty -> ()
        | JointAngle jointAngle ->
            match (physicsEngine.Bodies.TryGetValue jointAngle.TargetId, physicsEngine.Bodies.TryGetValue jointAngle.TargetId2) with
            | ((true, (_, body)), (true, (_, body2))) ->
                let hinge = new HingeConstraint (body, body2, jointAngle.Anchor, jointAngle.Anchor2, jointAngle.Axis, jointAngle.Axis2)
                hinge.SetLimit (jointAngle.AngleMin, jointAngle.AngleMax, jointAngle.Softness, jointAngle.BiasFactor, jointAngle.RelaxationFactor)
                hinge.BreakingImpulseThreshold <- jointAngle.BreakImpulseThreshold
                physicsEngine.PhysicsContext.AddConstraint hinge
                if physicsEngine.Constraints.TryAdd (jointId, hinge)
                then () // nothing to do
                else Log.debug ("Could not add joint via '" + scstring createJointMessage + "'.")
            | (_, _) -> Log.debug "Could not create a joint for one or more non-existent bodies."
        | _ -> failwithnie ()

    static member private createJoints (createJointsMessage : CreateJointsMessage) physicsEngine =
        List.iter
            (fun (jointProperties : JointProperties) ->
                let createJointMessage =
                    { JointSource = createJointsMessage.JointsSource
                      JointProperties = jointProperties }
                BulletPhysicsEngine.createJoint createJointMessage physicsEngine)
            createJointsMessage.JointsProperties

    static member private destroyJoint (destroyJointMessage : DestroyJointMessage) physicsEngine =
        match physicsEngine.Constraints.TryGetValue destroyJointMessage.JointId with
        | (true, contrain) ->
            physicsEngine.Constraints.Remove destroyJointMessage.JointId |> ignore
            physicsEngine.PhysicsContext.RemoveConstraint contrain
        | (false, _) -> ()

    static member private destroyJoints (destroyJointsMessage : DestroyJointsMessage) physicsEngine =
        List.iter (fun jointId ->
            BulletPhysicsEngine.destroyJoint { JointId = jointId } physicsEngine)
            destroyJointsMessage.JointIds

    static member private setBodyEnabled (setBodyEnabledMessage : SetBodyEnabledMessage) physicsEngine =
        match physicsEngine.Objects.TryGetValue setBodyEnabledMessage.BodyId with
        | (true, object) ->
            object.ActivationState <-
                if setBodyEnabledMessage.Enabled
                then ActivationState.ActiveTag
                else ActivationState.DisableSimulation
        | (false, _) -> ()

    static member private setBodyCenter (setBodyCenterMessage : SetBodyCenterMessage) physicsEngine =
        match physicsEngine.Objects.TryGetValue setBodyCenterMessage.BodyId with
        | (true, object) ->
            let mutable transform = object.WorldTransform
            transform.Translation <- setBodyCenterMessage.Center
            object.WorldTransform <- transform
        | (false, _) -> ()

    static member private setBodyRotation (setBodyRotationMessage : SetBodyRotationMessage) physicsEngine =
        match physicsEngine.Objects.TryGetValue setBodyRotationMessage.BodyId with
        | (true, object) -> object.WorldTransform <- object.WorldTransform.SetRotation setBodyRotationMessage.Rotation
        | (false, _) -> ()

    static member private setBodyAngularVelocity (setBodyAngularVelocityMessage : SetBodyAngularVelocityMessage) physicsEngine =
        match physicsEngine.Objects.TryGetValue setBodyAngularVelocityMessage.BodyId with
        | (true, (:? RigidBody as body)) -> body.AngularVelocity <- setBodyAngularVelocityMessage.AngularVelocity
        | (_, _) -> ()

    static member private applyBodyAngularImpulse (applyBodyAngularImpulseMessage : ApplyBodyAngularImpulseMessage) physicsEngine =
        match physicsEngine.Objects.TryGetValue applyBodyAngularImpulseMessage.BodyId with
        | (true, (:? RigidBody as body)) -> body.ApplyTorqueImpulse (applyBodyAngularImpulseMessage.AngularImpulse)
        | (_, _) -> ()

    static member private setBodyLinearVelocity (setBodyLinearVelocityMessage : SetBodyLinearVelocityMessage) physicsEngine =
        match physicsEngine.Objects.TryGetValue setBodyLinearVelocityMessage.BodyId with
        | (true, (:? RigidBody as body)) -> body.LinearVelocity <- setBodyLinearVelocityMessage.LinearVelocity
        | (_, _) -> ()

    static member private applyBodyLinearImpulse (applyBodyLinearImpulseMessage : ApplyBodyLinearImpulseMessage) physicsEngine =
        match physicsEngine.Objects.TryGetValue applyBodyLinearImpulseMessage.BodyId with
        | (true, (:? RigidBody as body)) -> body.ApplyImpulse (applyBodyLinearImpulseMessage.LinearImpulse, applyBodyLinearImpulseMessage.Offset)
        | (_, _) -> ()

    static member private applyBodyForce (applyBodyForceMessage : ApplyBodyForceMessage) physicsEngine =
        match physicsEngine.Objects.TryGetValue applyBodyForceMessage.BodyId with
        | (true, (:? RigidBody as body)) -> body.ApplyForce (applyBodyForceMessage.Force, applyBodyForceMessage.Offset)
        | (_, _) -> ()

    static member private applyBodyTorque (applyBodyTorqueMessage : ApplyBodyTorqueMessage) physicsEngine =
        match physicsEngine.Objects.TryGetValue applyBodyTorqueMessage.BodyId with
        | (true, (:? RigidBody as body)) -> body.ApplyTorque applyBodyTorqueMessage.Torque
        | (_, _) -> ()

    static member private setBodyObservable (setBodyObservableMessage : SetBodyObservableMessage) physicsEngine =
        match physicsEngine.Bodies.TryGetValue setBodyObservableMessage.BodyId with
        | (true, (_, body)) -> body.UserIndex <- if setBodyObservableMessage.Observable then 1 else -1
        | (false, _) ->
            match physicsEngine.Ghosts.TryGetValue setBodyObservableMessage.BodyId with
            | (true, ghost) -> ghost.UserIndex <- if setBodyObservableMessage.Observable then 1 else -1
            | (false, _) -> ()

    static member private handlePhysicsMessage physicsEngine physicsMessage =
        match physicsMessage with
        | CreateBodyMessage createBodyMessage -> BulletPhysicsEngine.createBody createBodyMessage physicsEngine
        | CreateBodiesMessage createBodiesMessage -> BulletPhysicsEngine.createBodies createBodiesMessage physicsEngine
        | DestroyBodyMessage destroyBodyMessage -> BulletPhysicsEngine.destroyBody destroyBodyMessage physicsEngine
        | DestroyBodiesMessage destroyBodiesMessage -> BulletPhysicsEngine.destroyBodies destroyBodiesMessage physicsEngine
        | CreateJointMessage createJointMessage -> BulletPhysicsEngine.createJoint createJointMessage physicsEngine
        | CreateJointsMessage createJointsMessage -> BulletPhysicsEngine.createJoints createJointsMessage physicsEngine
        | DestroyJointMessage destroyJointMessage -> BulletPhysicsEngine.destroyJoint destroyJointMessage physicsEngine
        | DestroyJointsMessage destroyJointsMessage -> BulletPhysicsEngine.destroyJoints destroyJointsMessage physicsEngine
        | SetBodyEnabledMessage setBodyEnabledMessage -> BulletPhysicsEngine.setBodyEnabled setBodyEnabledMessage physicsEngine
        | SetBodyCenterMessage setBodyCenterMessage -> BulletPhysicsEngine.setBodyCenter setBodyCenterMessage physicsEngine
        | SetBodyRotationMessage setBodyRotationMessage -> BulletPhysicsEngine.setBodyRotation setBodyRotationMessage physicsEngine
        | SetBodyAngularVelocityMessage setBodyAngularVelocityMessage -> BulletPhysicsEngine.setBodyAngularVelocity setBodyAngularVelocityMessage physicsEngine
        | ApplyBodyAngularImpulseMessage applyBodyAngularImpulseMessage -> BulletPhysicsEngine.applyBodyAngularImpulse applyBodyAngularImpulseMessage physicsEngine
        | SetBodyLinearVelocityMessage setBodyLinearVelocityMessage -> BulletPhysicsEngine.setBodyLinearVelocity setBodyLinearVelocityMessage physicsEngine
        | ApplyBodyLinearImpulseMessage applyBodyLinearImpulseMessage -> BulletPhysicsEngine.applyBodyLinearImpulse applyBodyLinearImpulseMessage physicsEngine
        | ApplyBodyForceMessage applyBodyForceMessage -> BulletPhysicsEngine.applyBodyForce applyBodyForceMessage physicsEngine
        | ApplyBodyTorqueMessage applyBodyTorqueMessage -> BulletPhysicsEngine.applyBodyTorque applyBodyTorqueMessage physicsEngine
        | SetBodyObservableMessage setBodyObservableMessage -> BulletPhysicsEngine.setBodyObservable setBodyObservableMessage physicsEngine
        | SetGravityMessage gravity ->
            physicsEngine.PhysicsContext.Gravity <- gravity
            for (gravityOverrideOpt, body) in physicsEngine.Bodies.Values do
                match gravityOverrideOpt with
                | Some gravityOverride -> body.Gravity <- gravityOverride
                | None -> body.Gravity <- gravity
        | RebuildPhysicsHackMessage ->
            physicsEngine.RebuildingHack <- true
            for constrain in physicsEngine.Constraints.Values do physicsEngine.PhysicsContext.RemoveConstraint constrain
            physicsEngine.Objects.Clear ()
            physicsEngine.Constraints.Clear ()
            for ghost in physicsEngine.Ghosts.Values do physicsEngine.PhysicsContext.RemoveCollisionObject ghost
            physicsEngine.Ghosts.Clear ()
            for (_, body) in physicsEngine.Bodies.Values do physicsEngine.PhysicsContext.RemoveRigidBody body
            physicsEngine.Bodies.Clear ()
            physicsEngine.IntegrationMessages.Clear ()

    static member private integrate stepTime physicsEngine =
        let physicsStepAmount =
            match (Constants.GameTime.DesiredFrameRate, stepTime) with
            | (StaticFrameRate frameRate, UpdateTime frames) -> 1.0f / single frameRate * single frames
            | (DynamicFrameRate _, ClockTime secs) -> secs
            | (_, _) -> failwithumf ()
        if physicsStepAmount > 0.0f then
            let result = physicsEngine.PhysicsContext.StepSimulation physicsStepAmount
            ignore result

    static member private createIntegrationMessages physicsEngine =

        // create collision messages
        let collisionsOld = physicsEngine.Collisions
        physicsEngine.Collisions <- SegmentedDictionary.make HashIdentity.Structural
        let numManifolds = physicsEngine.PhysicsContext.Dispatcher.NumManifolds
        for i in 0 .. dec numManifolds do
            let manifold = physicsEngine.PhysicsContext.Dispatcher.GetManifoldByIndexInternal i
            let body0 = manifold.Body0
            let body1 = manifold.Body1
            if  body0.UserIndex = 1 ||
                body1.UserIndex = 1 then
                let bodySource0 = body0.UserObject :?> BodyId
                let bodySource1 = body1.UserObject :?> BodyId
                let collisionKey = (bodySource0, bodySource1)
                let mutable normal = v3Zero
                let numContacts = manifold.NumContacts
                for j in 0 .. dec numContacts do
                    let contact = manifold.GetContactPoint j
                    normal <- normal - contact.NormalWorldOnB
                normal <- normal / single numContacts
                SegmentedDictionary.add collisionKey normal physicsEngine.Collisions

        // create collision messages
        for entry in physicsEngine.Collisions do
            let (bodySourceA, bodySourceB) = entry.Key
            if not (SegmentedDictionary.containsKey entry.Key collisionsOld) then
                BulletPhysicsEngine.handleCollision physicsEngine bodySourceA bodySourceB entry.Value
                BulletPhysicsEngine.handleCollision physicsEngine bodySourceB bodySourceA -entry.Value

        // create separation messages
        for entry in collisionsOld do
            let (bodySourceA, bodySourceB) = entry.Key
            if not (SegmentedDictionary.containsKey entry.Key physicsEngine.Collisions) then
                BulletPhysicsEngine.handleSeparation physicsEngine bodySourceA bodySourceB
                BulletPhysicsEngine.handleSeparation physicsEngine bodySourceB bodySourceA

        // create transform messages
        for (_, body) in physicsEngine.Bodies.Values do
            if body.IsActive then
                let bodyTransformMessage =
                    BodyTransformMessage
                        { BodyId = body.UserObject :?> BodyId
                          Center = body.MotionState.WorldTransform.Translation
                          Rotation = body.MotionState.WorldTransform.Rotation
                          LinearVelocity = body.LinearVelocity
                          AngularVelocity = body.AngularVelocity }
                physicsEngine.IntegrationMessages.Add bodyTransformMessage

    static member private handlePhysicsMessages physicsMessages physicsEngine =
        for physicsMessage in physicsMessages do
            BulletPhysicsEngine.handlePhysicsMessage physicsEngine physicsMessage
        physicsEngine.RebuildingHack <- false

    static member make imperative gravity =
        let config = if imperative then Imperative else Functional
        let physicsMessages = UList.makeEmpty config
        let collisionConfiguration = new DefaultCollisionConfiguration ()
        let physicsDispatcher = new CollisionDispatcher (collisionConfiguration)
        let broadPhaseInterface = new DbvtBroadphase ()
        let constraintSolver = new SequentialImpulseConstraintSolver ()
        let world = new DiscreteDynamicsWorld (physicsDispatcher, broadPhaseInterface, constraintSolver, collisionConfiguration)
        world.Gravity <- gravity
        { PhysicsContext = world
          Constraints = OrderedDictionary HashIdentity.Structural
          Bodies = OrderedDictionary HashIdentity.Structural
          Ghosts = OrderedDictionary HashIdentity.Structural
          Objects = OrderedDictionary HashIdentity.Structural
          Collisions = SegmentedDictionary.make HashIdentity.Structural
          CollisionConfiguration = collisionConfiguration
          PhysicsDispatcher = physicsDispatcher
          BroadPhaseInterface = broadPhaseInterface
          ConstraintSolver = constraintSolver
          HacdCache = Dictionary HashIdentity.Structural
          PhysicsMessages = physicsMessages
          IntegrationMessages = List ()
          RebuildingHack = false }

    static member cleanUp physicsEngine =
        physicsEngine.PhysicsContext.Dispose ()
        physicsEngine.ConstraintSolver.Dispose ()
        physicsEngine.BroadPhaseInterface.Dispose ()
        physicsEngine.PhysicsDispatcher.Dispose ()
        physicsEngine.CollisionConfiguration.Dispose ()

    interface PhysicsEngine with

        member physicsEngine.BodyExists bodyId =
            physicsEngine.Objects.ContainsKey bodyId

        member physicsEngine.GetBodyContactNormals bodyId =
            // TODO: see if this can be optimized from a linear-time search to constant-time look-up.
            match physicsEngine.Objects.TryGetValue bodyId with
            | (true, object) ->
                let dispatcher = physicsEngine.PhysicsContext.Dispatcher
                let manifoldCount = dispatcher.NumManifolds
                [for i in 0 .. dec manifoldCount do
                    let manifold = dispatcher.GetManifoldByIndexInternal i
                    if manifold.Body0 = object then
                        let contactCount = manifold.NumContacts
                        for j in 0 .. dec contactCount do
                            let contact = manifold.GetContactPoint j
                            yield -contact.NormalWorldOnB]
            | (false, _) -> []

        member physicsEngine.GetBodyLinearVelocity bodyId =
            match physicsEngine.Bodies.TryGetValue bodyId with
            | (true, (_, body)) -> body.LinearVelocity
            | (false, _) ->
                if physicsEngine.Ghosts.ContainsKey bodyId then v3Zero
                else failwith ("No body with BodyId = " + scstring bodyId + ".")

        member physicsEngine.GetBodyToGroundContactNormals bodyId =
            List.filter
                (fun normal ->
                    let theta = Vector3.Dot (normal, Vector3.UnitY) |> double |> Math.Acos |> Math.Abs
                    theta < Math.PI * 0.25)
                ((physicsEngine :> PhysicsEngine).GetBodyContactNormals bodyId)

        member physicsEngine.GetBodyToGroundContactNormalOpt bodyId =
            let groundNormals = (physicsEngine :> PhysicsEngine).GetBodyToGroundContactNormals bodyId
            match groundNormals with
            | [] -> None
            | _ ->
                let averageNormal = List.reduce (fun normal normal2 -> (normal + normal2) * 0.5f) groundNormals
                Some averageNormal

        member physicsEngine.GetBodyToGroundContactTangentOpt bodyId =
            match (physicsEngine :> PhysicsEngine).GetBodyToGroundContactNormalOpt bodyId with
            | Some normal -> Some (Vector3.Cross (v3Forward, normal))
            | None -> None

        member physicsEngine.IsBodyOnGround bodyId =
            let groundNormals = (physicsEngine :> PhysicsEngine).GetBodyToGroundContactNormals bodyId
            List.notEmpty groundNormals

        member physicsEngine.PopMessages () =
            let messages = physicsEngine.PhysicsMessages
            let physicsEngine = { physicsEngine with PhysicsMessages = UList.makeEmpty (UList.getConfig physicsEngine.PhysicsMessages) }
            (messages, physicsEngine :> PhysicsEngine)

        member physicsEngine.ClearMessages () =
            let physicsEngine = { physicsEngine with PhysicsMessages = UList.makeEmpty (UList.getConfig physicsEngine.PhysicsMessages) }
            physicsEngine :> PhysicsEngine

        member physicsEngine.EnqueueMessage physicsMessage =
#if HANDLE_PHYSICS_MESSAGES_IMMEDIATE
            BulletPhysicsEngine.handlePhysicsMessage physicsEngine physicsMessage
            physicsEngine
#else
            let physicsMessages = UList.add physicsMessage physicsEngine.PhysicsMessages
            let physicsEngine = { physicsEngine with PhysicsMessages = physicsMessages }
            physicsEngine :> PhysicsEngine
#endif

        member physicsEngine.Integrate stepTime physicsMessages =
            BulletPhysicsEngine.handlePhysicsMessages physicsMessages physicsEngine
            BulletPhysicsEngine.integrate stepTime physicsEngine
            BulletPhysicsEngine.createIntegrationMessages physicsEngine
            let integrationMessages = SegmentedArray.ofSeq physicsEngine.IntegrationMessages
            physicsEngine.IntegrationMessages.Clear ()
            integrationMessages

        member physicsEngine.CleanUp () =
            BulletPhysicsEngine.cleanUp physicsEngine