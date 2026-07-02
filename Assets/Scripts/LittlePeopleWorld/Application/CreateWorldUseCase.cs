using System;
using LittlePeopleWorld.Domain;
using LittlePeopleWorld.Master;

namespace LittlePeopleWorld.Application
{
    public sealed class CreateWorldUseCase
    {
        public World Execute(MasterDatabase masters, int worldPresetId)
        {
            if (masters == null)
            {
                throw new ArgumentNullException(nameof(masters));
            }

            return World.Create(masters, worldPresetId);
        }
    }
}
