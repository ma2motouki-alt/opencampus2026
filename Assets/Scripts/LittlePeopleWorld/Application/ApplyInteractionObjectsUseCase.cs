using System;
using System.Collections.Generic;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;

namespace LittlePeopleWorld.Application
{
    public sealed class ApplyInteractionObjectsUseCase
    {
        public void Execute(World world, IReadOnlyList<InteractionObject> interactionObjects, MasterDatabase masters)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (masters == null)
            {
                throw new ArgumentNullException(nameof(masters));
            }

            world.SetInteractionObjects(interactionObjects, masters);
        }
    }
}
