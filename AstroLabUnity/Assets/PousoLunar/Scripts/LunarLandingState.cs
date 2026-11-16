using System;
using UnityEngine.Events;

namespace AstroLab.PousoLunar
{
    public enum LunarLandingState
    {
        Idle, TransitionIn, Approach, Deceleration, VerticalDescent,
        Touchdown, EngineShutdown, Completed
    }

    [Serializable] public sealed class LandingStateEvent : UnityEvent<LunarLandingState> { }
    [Serializable] public sealed class LandingIntensityEvent : UnityEvent<float> { }
}
