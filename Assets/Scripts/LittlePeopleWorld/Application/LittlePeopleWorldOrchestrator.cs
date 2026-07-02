using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;

namespace LittlePeopleWorld.Application
{
    public sealed class LittlePeopleWorldOrchestrator
    {
        readonly CreateWorldUseCase createWorld;
        readonly ApplyInteractionObjectsUseCase applyInteractionObjects;
        readonly AdvanceWorldUseCase advanceWorld;

        public MasterDatabase Masters { get; }
        public World World { get; private set; }

        public LittlePeopleWorldOrchestrator(MasterDatabase masters)
            : this(masters, new CreateWorldUseCase(), new ApplyInteractionObjectsUseCase(), new AdvanceWorldUseCase())
        {
        }

        public LittlePeopleWorldOrchestrator(
            MasterDatabase masters,
            CreateWorldUseCase createWorld,
            ApplyInteractionObjectsUseCase applyInteractionObjects,
            AdvanceWorldUseCase advanceWorld)
        {
            Masters = masters ?? throw new ArgumentNullException(nameof(masters));
            this.createWorld = createWorld ?? throw new ArgumentNullException(nameof(createWorld));
            this.applyInteractionObjects = applyInteractionObjects ?? throw new ArgumentNullException(nameof(applyInteractionObjects));
            this.advanceWorld = advanceWorld ?? throw new ArgumentNullException(nameof(advanceWorld));
        }

        public World CreateWorld(int worldPresetId)
        {
            World = createWorld.Execute(Masters, worldPresetId);
            return World;
        }

        public void AdvanceFrame(float deltaTime, IReadOnlyList<InteractionObject> interactionObjects)
        {
            if (World == null)
            {
                throw new InvalidOperationException("World must be created before advancing a frame.");
            }

            applyInteractionObjects.Execute(World, interactionObjects, Masters);
            advanceWorld.Execute(World, deltaTime, Masters);
        }
    }
}
