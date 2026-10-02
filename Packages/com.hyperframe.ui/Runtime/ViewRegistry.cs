using System;
using System.Collections.Generic;
using UnityEngine;

namespace HyperFrame.UI
{
    /// <summary>Creates view instances. The default uses <see cref="ViewRegistry"/> prefabs, then falls back to code-built placeholders.</summary>
    public interface IViewProvider
    {
        UIView Create(Type viewType, Transform parent);
    }

    /// <summary>
    /// Maps view types to prefabs. A prefab's root must have the view component (e.g. HomeScreen).
    /// Views with no prefab here are built in code from their BuildPlaceholder method.
    /// </summary>
    [CreateAssetMenu(menuName = "HyperFrame/UI/View Registry", fileName = "ViewRegistry")]
    public sealed class ViewRegistry : ScriptableObject
    {
        public List<UIView> prefabs = new List<UIView>();

        public UIView FindPrefab(Type viewType)
        {
            foreach (var p in prefabs)
                if (p != null && p.GetType() == viewType) return p;
            return null;
        }
    }

    public sealed class DefaultViewProvider : IViewProvider
    {
        readonly ViewRegistry _registry;

        public DefaultViewProvider(ViewRegistry registry) => _registry = registry;

        public UIView Create(Type viewType, Transform parent)
        {
            if (!typeof(UIView).IsAssignableFrom(viewType))
                throw new ArgumentException($"{viewType.Name} is not a UIView");

            var prefab = _registry != null ? _registry.FindPrefab(viewType) : null;
            if (prefab != null) return UnityEngine.Object.Instantiate(prefab, parent, false);

            var rt = UIBuilder.CreateRect(viewType.Name, parent);
            return (UIView)rt.gameObject.AddComponent(viewType);
        }
    }
}
