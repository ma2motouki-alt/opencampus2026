using System;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;

namespace LittlePeopleWorld.Application
{
    public sealed class AdvanceWorldUseCase
    {
        public void Execute(World world, float deltaTime, MasterDatabase masters)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (masters == null)
            {
                throw new ArgumentNullException(nameof(masters));
            }

            world.Advance(deltaTime, masters);
        }
    }
}
