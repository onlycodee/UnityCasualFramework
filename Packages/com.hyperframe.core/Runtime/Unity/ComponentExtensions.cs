using UnityEngine;

namespace HyperFrame.Core
{
    public static class ComponentExtensions
    {
        /// <summary>
        /// Returns the component on <paramref name="go"/>, adding it if missing. Use this instead of
        /// <c>GetComponent&lt;T&gt;() ?? AddComponent&lt;T&gt;()</c>: in the Editor a missing component comes back as
        /// Unity's "fake null" object, which <c>??</c> treats as non-null, so nothing gets added.
        /// </summary>
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component =>
            go.TryGetComponent<T>(out var existing) ? existing : go.AddComponent<T>();

        /// <inheritdoc cref="GetOrAddComponent{T}(GameObject)"/>
        public static T GetOrAddComponent<T>(this Component owner) where T : Component =>
            owner.gameObject.GetOrAddComponent<T>();
    }
}
