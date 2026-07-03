using System.Reflection;
using UnityEngine;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Assigns [SerializeField] private fields on components that are created at runtime
    /// instead of being configured in the inspector.
    /// </summary>
    public static class RuntimeFieldBinder
    {
        public static void SetPrivateField<TTarget, TValue>(TTarget target, string fieldName, TValue value) where TTarget : class
        {
            if (target == null)
                return;

            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                Debug.LogWarning($"RuntimeFieldBinder: {target.GetType().Name} field '{fieldName}' was not found.");
                return;
            }

            field.SetValue(target, value);
        }
    }
}
