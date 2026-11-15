using System;
using System.Reflection;
using UnityEngine;

namespace AstroLab.Nicholas
{
    /// <summary>
    /// Os recursos de HUD, scanner e JogadorFPS ainda não estão todos na branch nicholas.
    /// Integração por reflexão mantém essa branch compilável antes do merge do time.
    /// </summary>
    internal static class OptionalTeamComponents
    {
        public static MonoBehaviour FindByName(string typeName)
        {
            MonoBehaviour[] components = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (MonoBehaviour component in components)
                if (component && component.GetType().Name == typeName) return component;
            return null;
        }

        public static bool IsHelmetModalOpen(MonoBehaviour helmet)
        {
            if (!helmet) return false;
            PropertyInfo property = helmet.GetType().GetProperty("FichaAberta", BindingFlags.Instance | BindingFlags.Public);
            if (property == null) return false;
            object result = property.GetValue(helmet, null);
            return result is bool && (bool)result;
        }
    }
}
