using System.Collections.Generic;
using LittlePeopleWorld.Domain;

namespace LittlePeopleWorld.Input
{
    public interface IInteractionInputProvider
    {
        IReadOnlyList<InteractionObject> InteractionObjects { get; }
        bool DebugEnabled { get; }
    }
}

