﻿// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Prowl.Runtime.Cloning;
using Prowl.Runtime.SceneManagement;

namespace Prowl.Runtime;

/// <summary>
/// The Base Class for all Object/Entities in a Scene.
/// Holds a collection of Components that contain the logic for this Object/Entity
/// </summary>
[CloneBehavior(CloneBehavior.Reference)]
public class GameObject : EngineObject, ISerializable, ICloneExplicit
{
    #region Private Fields/Properties

    private Dictionary<Type, MonoBehaviour> _components = [];

    private bool _static = false;

    private bool _enabled = true;
    private bool _enabledInHierarchy = true;
    [SerializeField]
    // DONT RENAME, GameObjectEditor finds this field by name "prefabLink" Doesn't use NameOf since its private
    private PrefabLink prefabLink = null;

    // We dont serialize parent, since if we want to serialize X object who is a child to Y object, we dont want to serialize Y object as well.
    // The parent is reconstructed when the object is deserialized for all children.
    private GameObject? _parent;

    [SerializeField]
    // DONT RENAME, GameObjectEditor finds this field by name "_transform" Doesn't use NameOf since its private
    private Transform _transform = new();

    [SerializeIgnore]
    private Scene _scene;

    #endregion

    #region Public Fields/Properties

    /// <summary> The Tag Index of this GameObject </summary>
    public byte tagIndex;

    /// <summary> The Layer Index of this GameObject </summary>
    public byte layerIndex;

    /// <summary> The Hide Flags of this GameObject, Used to hide the GameObject from a variety of places like Serializing, Inspector or Hierarchy </summary>
    public HideFlags hideFlags = HideFlags.None;

    /// <summary> Gets whether or not this gameobject is enabled explicitly </summary>
    public bool enabled
    {
        get => _enabled;
        set { if (value != _enabled) { SetEnabled(value); } }
    }

    /// <summary> Gets whether this gameobejct is enabled in the hierarchy, so if its parent is disabled this will return false </summary>
    public bool enabledInHierarchy => _enabledInHierarchy;

    /// <summary> The Tag of this GameObject </summary>
    public string tag
    {
        get => TagLayerManager.GetTag(tagIndex);
        set => tagIndex = TagLayerManager.GetTagIndex(value);
    }

    /// <summary> The Layer of this GameObject </summary>
    public string layer
    {
        get => TagLayerManager.GetLayer(layerIndex);
        set => layerIndex = TagLayerManager.GetLayerIndex(value);
    }

    /// <summary> The Static flag of this GameObject, Changing this may not behave as expected! </summary>
    public bool isStatic
    {
        get => _static;
        set => _static = value;
    }


    /// <summary> The Parent of this GameObject, Can be null </summary>
    public GameObject? parent => _parent;

    /// <summary> A List of all children of this GameObject </summary>
    public List<GameObject> children = [];

    public int childCount => children.Count;

    /// <summary>
    /// The <see cref="PrefabLink"/> that connects this object to a <see cref="Prefab"/>.
    /// </summary>
    public PrefabLink PrefabLink
    {
        get => prefabLink;
        internal set => prefabLink = value;
    }

    /// <summary>
    /// The <see cref="PrefabLink"/> that connects this object or one or its parent GameObjects to a <see cref="Prefab"/>.
    /// </summary>
    /// <remarks>
    /// This does not necessarily mean that this GameObject will be affected by the PrefabLink, since it might not be part of
    /// the linked Prefab. It simply indicates the returned PrefabLink's potential to adjust this GameObject when being applied.
    /// </remarks>
    public PrefabLink AffectedByPrefabLink
    {
        get
        {
            if (prefabLink != null) return prefabLink;
            else if (parent != null) return parent.AffectedByPrefabLink;
            else return null;
        }
    }

    /// <summary>
    /// The GameObjects parent <see cref="Prowl.Runtime.Scene"/>. Each GameObject can belong to
    /// exactly one Scene, or no Scene at all. To add or remove GameObjects to / from a Scene, use the <see cref="Prowl.Runtime.Scene.Add(GameObject)"/> and
    /// <see cref="Prowl.Runtime.Scene.Remove(GameObject)"/> methods.
    /// </summary>
    public Scene Scene
    {
        get => _scene;
        internal set => _scene = value;
    }

    #endregion

    public Transform Transform
    {
        get
        {
            _transform.gameObject = this; // ensure game object is this
            return _transform;
        }
    }

    /// <summary>
    /// Checks if this GameObject is a child or the same as the given parent transform.
    /// </summary>
    /// <param name="transform">The GameObject to check.</param>
    /// <param name="inParent">The potential parent GameObject.</param>
    /// <returns>True if this GameObject is a child or the same as the given parent, false otherwise.</returns>
    public static bool IsChildOrSameTransform(GameObject transform, GameObject inParent)
    {
        GameObject child = transform;
        while (child != null)
        {
            if (child == inParent)
                return true;
            child = child._parent;
        }
        return false;
    }


    /// <summary>
    /// Checks if this GameObject is a child of the given parent.
    /// </summary>
    /// <param name="parent">The potential parent GameObject.</param>
    /// <returns>True if this GameObject is a child of the given parent, false otherwise.</returns>
    public bool IsChildOf(GameObject parent)
    {
        if (InstanceID == parent.InstanceID) return false; // Not a child their the same object

        GameObject child = this;
        while (child != null)
        {
            if (child == parent)
                return true;
            child = child._parent;
        }
        return false;
    }

    /// <summary>
    /// Sets the parent of this GameObject.
    /// </summary>
    /// <param name="NewParent">The new parent GameObject.</param>
    /// <param name="worldPositionStays">If true, the world position of the GameObject is maintained.</param>
    /// <returns>True if the parent was successfully set, false otherwise.</returns>
    public bool SetParent(GameObject NewParent, bool worldPositionStays = true)
    {
        if (NewParent == _parent)
            return true;

        // Make sure that the new father is not a child of this transform.
        if (IsChildOrSameTransform(NewParent, this))
            return false;

        Scene newScene = (NewParent != null) ? NewParent._scene : _scene;

        if (newScene != _scene)
        {
            _scene?.Remove(this);
            newScene?.Add(this);
        }

        // Save the old position in worldspace
        Vector3 worldPosition = new Vector3();
        Quaternion worldRotation = new Quaternion();
        Matrix4x4 worldScale = new Matrix4x4();

        if (worldPositionStays)
        {
            worldPosition = Transform.position;
            worldRotation = Transform.rotation;
            worldScale = Transform.GetWorldRotationAndScale();
        }

        if (NewParent != _parent)
        {
            // If it already has an father, remove this from fathers children
            if (_parent != null)
                _parent.children.Remove(this);

            if (NewParent != null)
                NewParent.children.Add(this);

            _parent = NewParent;
        }

        if (worldPositionStays)
        {
            if (_parent != null)
            {
                Transform.localPosition = _parent.Transform.InverseTransformPoint(worldPosition);
                Transform.localRotation = Quaternion.NormalizeSafe(Quaternion.Inverse(_parent.Transform.rotation) * worldRotation);
            }
            else
            {
                Transform.localPosition = worldPosition;
                Transform.localRotation = Quaternion.NormalizeSafe(worldRotation);
            }

            Transform.localScale = Vector3.one;
            Matrix4x4 inverseRS = Transform.GetWorldRotationAndScale().Invert() * worldScale;
            Transform.localScale = new Vector3(inverseRS[0, 0], inverseRS[1, 1], inverseRS[2, 2]);
        }

        HierarchyStateChanged();

        return true;
    }

    #region Constructors

    /// <summary>Creates a new gameobject with tbe name 'New GameObject'.</summary>
    public GameObject() : base("New GameObject") { }

    /// <summary>Creates a new gameobject.</summary>
    /// <param name="name">The name of the gameobject.</param>
    public GameObject(string name = "New GameObject") : base(name) { }

    /// <summary>
    /// Creates a GameObject based on a specific <see cref="Prowl.Runtime.Prefab"/>.
    /// </summary>
    /// <param name="prefab">The Prefab that will be applied to this GameObject.</param>
    /// <seealso cref="Prowl.Runtime.Prefab"/>
    public GameObject(AssetRef<Prefab> prefab)
    {
        if (!prefab.IsAvailable) return;
        if (Application.IsEditor == false)
        {
            prefab.Res.CopyTo(this);
        }
        else
        {
            this.LinkToPrefab(prefab);
            PrefabLink.Apply();
        }
    }

    #endregion


    /// <summary> Recursive function to check if this GameObject is a parent of another GameObject </summary>
    public bool IsParentOf(GameObject go)
    {
        if (go.parent?.InstanceID == InstanceID)
            return true;

        foreach (GameObject child in children)
            if (child.IsParentOf(go))
                return true;

        return false;
    }

    /// <summary>
    /// Sets or alters this GameObject's <see cref="PrefabLink"/> to reference the specified <see cref="Prefab"/>.
    /// </summary>
    /// <param name="prefab">The Prefab that will be linked to.</param>
    public void LinkToPrefab(AssetRef<Prefab> prefab)
    {
        if (prefabLink == null)
        {
            // Not affected by another (higher) PrefabLink
            if (AffectedByPrefabLink == null)
            {
                prefabLink = new PrefabLink(this, prefab);
                // If a nested object is already PrefabLinked, add it to the change list
                foreach (GameObject child in GetChildrenDeep())
                {
                    if (child.PrefabLink != null && child.PrefabLink.ParentLink == prefabLink)
                    {
                        prefabLink.PushChange(child, nameof(prefabLink), child.PrefabLink.Clone());
                    }
                }
            }
            // Already affected by another (higher) PrefabLink
            else
            {
                prefabLink = new PrefabLink(this, prefab);
                prefabLink.ParentLink.RelocateChanges(prefabLink);
            }
        }
        else
            prefabLink = prefabLink.Clone(this, prefab);
    }

    /// <summary>
    /// Breaks this GameObject's <see cref="PrefabLink"/>
    /// </summary>
    public void BreakPrefabLink()
    {
        prefabLink = null;
    }

    /// <summary>
    /// Checks if this GameObject's tag matches the given tag.
    /// </summary>
    /// <param name="otherTag">The tag to compare against.</param>
    /// <returns>True if the tags match, false otherwise.</returns>
    public bool CompareTag(string otherTag) => TagLayerManager.GetTag(tagIndex).Equals(otherTag, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Finds a GameObject by name.
    /// </summary>
    /// <param name="otherName">The name of the GameObject to find.</param>
    /// <param name="ignoreCase">If true, the search is case-insensitive.</param>
    /// <returns>The first GameObject with the given name, or null if not found.</returns>
    public static GameObject Find(string otherName, bool ignoreCase = false) => SceneManager.Scene.AllObjects.FirstOrDefault(gameObject => gameObject.Name.Equals(otherName, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));

    /// <summary>
    /// Finds a GameObject with the specified tag.
    /// </summary>
    /// <param name="otherTag">The tag to search for.</param>
    /// <returns>The first GameObject with the given tag, or null if not found.</returns>
    public static GameObject FindGameObjectWithTag(string otherTag) => SceneManager.Scene.AllObjects.FirstOrDefault(gameObject => gameObject.CompareTag(otherTag));

    /// <summary>
    /// Finds all GameObjects with the specified tag.
    /// </summary>
    /// <param name="otherTag">The tag to search for.</param>
    /// <returns>An array of GameObjects with the given tag.</returns>
    public static GameObject[] FindGameObjectsWithTag(string otherTag) => SceneManager.Scene.AllObjects.Where(gameObject => gameObject.CompareTag(otherTag)).ToArray();


    /// <summary>
    /// Enumerates all GameObjects that are directly or indirectly parented to this object, i.e. its
    /// children, grandchildren, etc.
    /// </summary>
    public IEnumerable<GameObject> GetChildrenDeep()
    {
        if (children == null) return Enumerable.Empty<GameObject>();

        int startCapacity = Math.Max(children.Count * 2, 8);
        List<GameObject> result = new(startCapacity);
        GetChildrenDeep(result);
        return result;
    }

    /// <summary>
    /// Gathers all GameObjects that are directly or indirectly parented to this object, i.e. its
    /// children, grandchildren, etc.
    /// </summary>
    public void GetChildrenDeep(List<GameObject> resultList)
    {
        if (children == null) return;
        resultList.AddRange(children);
        for (int i = 0; i < children.Count; i++)
            children[i].GetChildrenDeep(resultList);
    }

    public GameObject GetChildAtIndexPath(IEnumerable<int> indexPath)
    {
        GameObject curObj = this;
        foreach (int i in indexPath)
        {
            if (i < 0) return null;
            if (curObj.children == null) return null;
            if (i >= curObj.children.Count) return null;
            curObj = curObj.children[i];
        }
        return curObj;
    }

    /// <summary>
    /// Determines the index path from this GameObject to the specified child (or grandchild, etc.) of it.
    /// </summary>
    /// <param name="child">The child GameObject to lead to.</param>
    /// <returns>A <see cref="List{T}"/> of indices that lead from this GameObject to the specified child GameObject.</returns>
    /// <seealso cref="GetChildAtIndexPath"/>
    public List<int> GetIndexPathOfChild(GameObject child)
    {
        List<int> path = [];
        while (child.parent != null && child != this)
        {
            path.Add(child.parent.children.IndexOf(child));
            child = child.parent;
        }
        path.Reverse();
        return path;
    }

    /// <summary>
    /// Performs pre-update operations on the GameObject's components.
    /// </summary>
    internal void PreUpdate()
    {
        foreach (MonoBehaviour component in _components.Values)
        {
            if (!component.HasAwoken)
                component.Do(component.InternalAwake);

            if (!component.HasStarted)
                if (component.EnabledInHierarchy)
                {
                    component.Do(component.InternalStart);
                }
        }
    }

    /// <summary>
    /// Adds a component of type T to the GameObject.
    /// </summary>
    /// <typeparam name="T">The type of component to add.</typeparam>
    /// <returns>The newly added component of type T.</returns>
    public T AddComponent<T>() where T : MonoBehaviour, new() => AddComponent(typeof(T)) as T;

    /// <summary>
    /// Adds a component of the specified type to the GameObject.
    /// </summary>
    /// <param name="type">The type of component to add.</param>
    /// <returns>The newly added MonoBehaviour component.</returns>
    public MonoBehaviour AddComponent(Type type)
    {
        if (!typeof(MonoBehaviour).IsAssignableFrom(type)) return null;

        if (_components.ContainsKey(type))
        {
            Debug.LogError("Cannot Add the Same Component Type Multiple Times!");
            return null;
        }

        RequireComponentAttribute? requireComponentAttribute = type.GetCustomAttribute<RequireComponentAttribute>();
        if (requireComponentAttribute != null)
        {
            foreach (Type requiredComponentType in requireComponentAttribute.types)
            {
                if (!typeof(MonoBehaviour).IsAssignableFrom(requiredComponentType))
                    continue;

                // If there is already a component on the object
                if (GetComponent(requiredComponentType) != null)
                    continue;

                // Recursive call to attempt to add the new component
                AddComponent(requiredComponentType);
            }
        }

        var newComponent = Activator.CreateInstance(type) as MonoBehaviour;
        if (newComponent == null) return null;

        newComponent.AttachToGameObject(this);
        _components.Add(type, newComponent);

        if (enabledInHierarchy)
        {
            newComponent.Do(newComponent.InternalAwake);
        }

        return newComponent;
    }

    /// <summary>
    /// Adds an existing MonoBehaviour component to the GameObject.
    /// </summary>
    /// <param name="comp">The MonoBehaviour component to add.</param>
    public void AddComponent(MonoBehaviour comp)
    {
        ArgumentNullException.ThrowIfNull(comp, nameof(comp));

        if (_components.ContainsKey(comp.GetType()))
        {
            Debug.LogError("Cannot Add the Same Component Type Multiple Times!");
            return;
        }

        Type type = comp.GetType();
        RequireComponentAttribute? requireComponentAttribute = type.GetCustomAttribute<RequireComponentAttribute>();
        if (requireComponentAttribute != null)
        {
            foreach (Type requiredComponentType in requireComponentAttribute.types)
            {
                if (!typeof(MonoBehaviour).IsAssignableFrom(requiredComponentType))
                    continue;

                // If there is already a component on the object
                if (GetComponent(requiredComponentType) != null)
                    continue;

                // Recursive call to attempt to add the new component
                AddComponent(requiredComponentType);
            }
        }

        comp.AttachToGameObject(this);
        _components.Add(comp.GetType(), comp);
        if (enabledInHierarchy)
        {
            comp.Do(comp.InternalAwake);
        }
    }

    /// <summary>
    /// Removes a specific component from the GameObject.
    /// </summary>
    /// <typeparam name="T">The type of component to remove.</typeparam>
    /// <param name="component">The component instance to remove.</param>
    public void RemoveComponent<T>(T component) where T : MonoBehaviour
    {
        ArgumentNullException.ThrowIfNull(component, nameof(component));
        if (component.CanDestroy() == false) return;

        _components.Remove(typeof(T));

        if (component.EnabledInHierarchy) component.Do(component.OnDisable);
        if (component.HasStarted) component.Do(component.OnDestroy); // OnDestroy is only called if the component has previously been active
    }

    /// <summary>
    /// Removes a specific component from the GameObject.
    /// </summary>
    /// <param name="cType">The component type to remove.</param>
    public void RemoveComponent(Type cType)
    {
        ArgumentNullException.ThrowIfNull(cType, nameof(cType));
        MonoBehaviour? c = GetComponent(cType);
        if (c == null) return;
        if (c.CanDestroy() == false) return;

        _components.Remove(cType);

        if (c.EnabledInHierarchy) c.Do(c.OnDisable);
        if (c.HasStarted) c.Do(c.OnDestroy); // OnDestroy is only called if the component has previously been active
    }

    /// <summary>
    /// Removes a specific MonoBehaviour component from the GameObject.
    /// </summary>
    /// <param name="component">The MonoBehaviour component to remove.</param>
    public void RemoveComponent(MonoBehaviour component)
    {
        ArgumentNullException.ThrowIfNull(component, nameof(component));
        if (component.CanDestroy() == false) return;

        if (_components.TryGetValue(component.GetType(), out MonoBehaviour? comp) && comp == component)
        {
            _components.Remove(component.GetType());

            if (component.EnabledInHierarchy) component.Do(component.OnDisable);
            if (component.HasStarted) component.Do(component.OnDestroy); // OnDestroy is only called if the component has previously been active
        }
        else
        {
            Debug.LogError("Attempted to remove the component " + component.GetType().Name + " but failed because it is not attached to the target GameObject");
        }
    }

    /// <summary>
    /// Gets the first component of type T attached to the GameObject.
    /// </summary>
    /// <typeparam name="T">The type of component to get.</typeparam>
    /// <returns>The component of type T, or null if not found.</returns>
    public T? GetComponent<T>() where T : MonoBehaviour => (T?)GetComponent(typeof(T));

    /// <summary>
    /// Gets the first component of the specified type attached to the GameObject.
    /// </summary>
    /// <param name="type">The type of component to get.</param>
    /// <returns>The MonoBehaviour component of the specified type, or null if not found.</returns>
    public MonoBehaviour? GetComponent(Type type)
    {
        if (type == null) return null;
        if (_components.TryGetValue(type, out MonoBehaviour? component))
            return component;
        else
            foreach (MonoBehaviour comp in _components.Values)
                if (comp.GetType().IsAssignableTo(type))
                    return comp;
        return null;
    }

    /// <summary>
    /// Gets all components attached to the GameObject.
    /// </summary>
    /// <returns>An IEnumerable of all MonoBehaviour components.</returns>
    public IEnumerable<MonoBehaviour> GetComponents() => _components.Values;

    /// <summary>
    /// Tries to get the first component of type T attached to the GameObject.
    /// </summary>
    /// <typeparam name="T">The type of component to get.</typeparam>
    /// <param name="component">The output parameter to store the found component.</param>
    /// <returns>True if a component of type T was found, false otherwise.</returns>
    public bool TryGetComponent<T>(out T? component) where T : MonoBehaviour => (component = GetComponent<T>()) != null;

    /// <summary>
    /// Gets all components of type T attached to the GameObject.
    /// </summary>
    /// <typeparam name="T">The type of components to get.</typeparam>
    /// <returns>An IEnumerable of components of type T.</returns>
    public IEnumerable<T> GetComponents<T>() where T : MonoBehaviour => GetComponents(typeof(T)).Cast<T>();

    /// <summary>
    /// Gets all components of the specified type attached to the GameObject.
    /// </summary>
    /// <param name="type">The type of components to get.</param>
    /// <returns>An IEnumerable of MonoBehaviour components of the specified type.</returns>
    public IEnumerable<MonoBehaviour> GetComponents(Type type)
    {
        if (type == typeof(MonoBehaviour))
        {
            // Special case for Component
            foreach (MonoBehaviour comp in _components.Values)
                yield return comp;
        }
        else
        {
            if (!_components.TryGetValue(type, out MonoBehaviour? component))
            {
                foreach (KeyValuePair<Type, MonoBehaviour> kvp in _components.ToArray())
                    if (kvp.Key.IsAssignableTo(type))
                        yield return kvp.Value;
            }
            else
            {
                if (component.GetType().IsAssignableTo(type))
                    yield return component;
            }
        }
    }

    /// <summary>
    /// Gets the first component of type T in the GameObject or its parents.
    /// </summary>
    /// <typeparam name="T">The type of component to get.</typeparam>
    /// <param name="includeSelf">If true, includes the current GameObject in the search.</param>
    /// <param name="includeInactive">If true, includes inactive GameObjects in the search.</param>
    /// <returns>The component of type T, or null if not found.</returns>
    public T? GetComponentInParent<T>(bool includeSelf = true, bool includeInactive = false) where T : MonoBehaviour => (T)GetComponentInParent(typeof(T), includeSelf, includeInactive);

    /// <summary>
    /// Gets the first component of the specified type in the GameObject or its parents.
    /// </summary>
    /// <param name="componentType">The type of component to get.</param>
    /// <param name="includeSelf">If true, includes the current GameObject in the search.</param>
    /// <param name="includeInactive">If true, includes inactive GameObjects in the search.</param>
    /// <returns>The MonoBehaviour component of the specified type, or null if not found.</returns>
    public MonoBehaviour? GetComponentInParent(Type componentType, bool includeSelf = true, bool includeInactive = false)
    {
        if (componentType == null) return null;
        // First check the current Object
        MonoBehaviour component;
        if (includeSelf && enabledInHierarchy)
        {
            component = GetComponent(componentType);
            if (component != null)
                return component;
        }
        // Now check all parents
        GameObject parent = this;
        while ((parent = parent.parent) != null)
        {
            if (parent.enabledInHierarchy || includeInactive)
            {
                component = parent.GetComponent(componentType);
                if (component != null)
                    return component;
            }
        }
        return null;
    }

    /// <summary>
    /// Gets all components of type T in the GameObject and its parents.
    /// </summary>
    /// <typeparam name="T">The type of components to get.</typeparam>
    /// <param name="includeSelf">If true, includes the current GameObject in the search.</param>
    /// <param name="includeInactive">If true, includes inactive GameObjects in the search.</param>
    /// <returns>An IEnumerable of components of type T.</returns>
    public IEnumerable<T> GetComponentsInParent<T>(bool includeSelf = true, bool includeInactive = false) where T : MonoBehaviour => GetComponentsInParent(typeof(T), includeSelf, includeInactive).Cast<T>();

    /// <summary>
    /// Gets all components of the specified type in the GameObject and its parents.
    /// </summary>
    /// <param name="type">The type of components to get.</param>
    /// <param name="includeSelf">If true, includes the current GameObject in the search.</param>
    /// <param name="includeInactive">If true, includes inactive GameObjects in the search.</param>
    /// <returns>An IEnumerable of MonoBehaviour components of the specified type.</returns>
    public IEnumerable<MonoBehaviour> GetComponentsInParent(Type type, bool includeSelf = true, bool includeInactive = false)
    {
        // First check the current Object
        if (includeSelf && enabledInHierarchy)
            foreach (MonoBehaviour component in GetComponents(type))
                yield return component;
        // Now check all parents
        GameObject parent = this;
        while ((parent = parent.parent) != null)
        {
            if (parent.enabledInHierarchy || includeInactive)
                foreach (MonoBehaviour component in parent.GetComponents(type))
                    yield return component;
        }
    }

    /// <summary>
    /// Gets the first component of type T in the GameObject or its children.
    /// </summary>
    /// <typeparam name="T">The type of component to get.</typeparam>
    /// <param name="includeSelf">If true, includes the current GameObject in the search.</param>
    /// <param name="includeInactive">If true, includes inactive GameObjects in the search.</param>
    /// <returns>The component of type T, or null if not found.</returns>
    public T? GetComponentInChildren<T>(bool includeSelf = true, bool includeInactive = false) where T : MonoBehaviour => (T)GetComponentInChildren(typeof(T), includeSelf, includeInactive);

    /// <summary>
    /// Gets the first component of the specified type in the GameObject or its children.
    /// </summary>
    /// <param name="componentType">The type of component to get.</param>
    /// <param name="includeSelf">If true, includes the current GameObject in the search.</param>
    /// <param name="includeInactive">If true, includes inactive GameObjects in the search.</param>
    /// <returns>The MonoBehaviour component of the specified type, or null if not found.</returns>
    public MonoBehaviour GetComponentInChildren(Type componentType, bool includeSelf = true, bool includeInactive = false)
    {
        if (componentType == null) return null;
        // First check the current Object
        MonoBehaviour component;
        if (includeSelf && enabledInHierarchy)
        {
            component = GetComponent(componentType);
            if (component != null)
                return component;
        }
        // Now check all children
        foreach (GameObject child in children)
        {
            if (enabledInHierarchy || includeInactive)
            {
                component = child.GetComponent(componentType) ?? child.GetComponentInChildren(componentType);
                if (component != null)
                    return component;
            }
        }
        return null;
    }

    /// <summary>
    /// Gets all components of type T in the GameObject and its children.
    /// </summary>
    /// <typeparam name="T">The type of components to get.</typeparam>
    /// <param name="includeSelf">If true, includes the current GameObject in the search.</param>
    /// <param name="includeInactive">If true, includes inactive GameObjects in the search.</param>
    /// <returns>An IEnumerable of components of type T.</returns>
    public IEnumerable<T> GetComponentsInChildren<T>(bool includeSelf = true, bool includeInactive = false) where T : MonoBehaviour => GetComponentsInChildren(typeof(T), includeSelf, includeInactive).Cast<T>();

    /// <summary>
    /// Gets all components of the specified type in the GameObject and its children.
    /// </summary>
    /// <param name="type">The type of components to get.</param>
    /// <param name="includeSelf">If true, includes the current GameObject in the search.</param>
    /// <param name="includeInactive">If true, includes inactive GameObjects in the search.</param>
    /// <returns>An IEnumerable of MonoBehaviour components of the specified type.</returns>
    public IEnumerable<MonoBehaviour> GetComponentsInChildren(Type type, bool includeSelf = true, bool includeInactive = false)
    {
        // First check the current Object
        if (includeSelf && enabledInHierarchy)
            foreach (MonoBehaviour component in GetComponents(type))
                yield return component;
        // Now check all children
        foreach (GameObject child in children)
        {
            if (enabledInHierarchy || includeInactive)
                foreach (MonoBehaviour component in child.GetComponentsInChildren(type, true, includeInactive))
                    yield return component;
        }
    }

    /// <summary>
    /// Checks if a component is required by other components on the GameObject.
    /// </summary>
    /// <param name="requiredComponent">The component to check.</param>
    /// <param name="dependentType">The output parameter to store the type of the dependent component.</param>
    /// <returns>True if the component is required, false otherwise.</returns>
    internal bool IsComponentRequired(MonoBehaviour requiredComponent, out Type dependentType)
    {
        Type componentType = requiredComponent.GetType();
        foreach (KeyValuePair<Type, MonoBehaviour> component in _components)
        {
            RequireComponentAttribute? requireComponentAttribute =
                component.GetType().GetCustomAttribute<RequireComponentAttribute>();
            if (requireComponentAttribute == null)
                continue;

            if (requireComponentAttribute.types.All(type => type != componentType))
                continue;

            dependentType = component.GetType();
            return true;
        }
        dependentType = null;
        return false;
    }

    /// <summary>
    /// Instantiates a new GameObject from the original.
    /// </summary>
    /// <param name="original">The original GameObject to clone.</param>
    /// <returns>A new instance of the GameObject.</returns>
    public static GameObject Instantiate(GameObject original) => Instantiate(original, null);

    /// <summary>
    /// Instantiates a new GameObject from the original with the specified parent.
    /// </summary>
    /// <param name="original">The original GameObject to clone.</param>
    /// <param name="parent">The parent GameObject for the new instance.</param>
    /// <returns>A new instance of the GameObject.</returns>
    public static GameObject Instantiate(GameObject original, GameObject? parent)
    {
        GameObject clone = (GameObject)EngineObject.Instantiate(original, false);
        clone.SetParent(parent);
        SceneManager.Scene.Add(clone);
        return clone;
    }

    /// <summary>
    /// Instantiates a new GameObject from the original with the specified parent, position, and rotation.
    /// </summary>
    /// <param name="original">The original GameObject to clone.</param>
    /// <param name="parent">The parent GameObject for the new instance.</param>
    /// <param name="position">The position for the new instance.</param>
    /// <param name="rotation">The rotation for the new instance.</param>
    /// <returns>A new instance of the GameObject.</returns>
    public static GameObject Instantiate(GameObject original, GameObject? parent, Vector3 position, Quaternion rotation) => Instantiate(original, position, rotation, parent);

    /// <summary>
    /// Instantiates a new GameObject from the original with the specified position, rotation, and parent.
    /// </summary>
    /// <param name="original">The original GameObject to clone.</param>
    /// <param name="position">The position for the new instance.</param>
    /// <param name="rotation">The rotation for the new instance.</param>
    /// <param name="parent">The parent GameObject for the new instance.</param>
    /// <returns>A new instance of the GameObject.</returns>
    public static GameObject Instantiate(GameObject original, Vector3 position, Quaternion rotation, GameObject? parent)
    {
        GameObject clone = (GameObject)EngineObject.Instantiate(original, false);
        clone.Transform.position = position;
        clone.Transform.rotation = rotation;
        clone.SetParent(parent, true);
        SceneManager.Scene.Add(clone);
        return clone;
    }

    /// <summary>
    /// Disposes of the GameObject and its components.
    /// </summary>
    public override void OnDispose()
    {
        for (int i = children.Count - 1; i >= 0; i--)
            children[i].DestroyImmediate();

        for (int i = _components.Count - 1; i >= 0; i--)
        {
            MonoBehaviour component = _components.Values.ElementAt(i);
            if (component.IsDestroyed) continue;
            if (component.EnabledInHierarchy) component.Do(component.OnDisable);
            if (component.HasStarted) component.Do(component.OnDestroy); // OnDestroy is only called if the component has previously been active
            component.DestroyImmediate();
        }
        _components.Clear();

        if (_parent != null && !_parent.IsDestroyed)
            SetParent(null);
    }

    /// <summary>
    /// Sets the enabled state of the GameObject.
    /// </summary>
    /// <param name="state">The new enabled state.</param>
    private void SetEnabled(bool state)
    {
        _enabled = state;
        HierarchyStateChanged();
    }

    /// <summary>
    /// Updates the hierarchy state of the GameObject and its children.
    /// </summary>
    private void HierarchyStateChanged()
    {
        bool newState = _enabled && IsParentEnabled();
        if (_enabledInHierarchy != newState)
        {
            _enabledInHierarchy = newState;
            foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
                component.HierarchyStateChanged();
        }

        foreach (GameObject child in children)
            child.HierarchyStateChanged();
    }

    /// <summary>
    /// Checks if the parent of this GameObject is enabled.
    /// </summary>
    /// <returns>True if the parent is enabled or if there is no parent, false otherwise.</returns>
    private bool IsParentEnabled() => parent == null || parent.enabledInHierarchy;

    /// <summary>
    /// Calls the specified method on every MonoBehaviour in this GameObject and its children.
    /// </summary>
    /// <param name="methodName">The name of the method to call.</param>
    /// <param name="objs">Optional parameters to pass to the method.</param>
    public void BroadcastMessage(string methodName, params object[] objs)
    {
        foreach (MonoBehaviour component in GetComponents<MonoBehaviour>())
            component.SendMessage(methodName, objs);

        foreach (GameObject child in children)
            child.BroadcastMessage(methodName, objs);
    }

    /// <summary>
    /// Calls the specified method on every MonoBehaviour in this GameObject.
    /// </summary>
    /// <param name="methodName">The name of the method to call.</param>
    /// <param name="objs">Optional parameters to pass to the method.</param>
    public void SendMessage(string methodName, params object[] objs)
    {
        foreach (MonoBehaviour c in GetComponents<MonoBehaviour>())
        {
            MethodInfo method = c.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            method?.Invoke(c, objs);
        }
    }

    /// <summary>
    /// Serializes the GameObject to a SerializedProperty.
    /// </summary>
    /// <param name="ctx">The serialization context.</param>
    /// <returns>A SerializedProperty containing the GameObject's data.</returns>
    public SerializedProperty Serialize(Serializer.SerializationContext ctx)
    {
        SerializedProperty compoundTag = SerializedProperty.NewCompound();
        compoundTag.Add("Name", new SerializedProperty(Name));

        compoundTag.Add("Static", new SerializedProperty((byte)(_static ? 1 : 0)));

        compoundTag.Add("Enabled", new SerializedProperty((byte)(_enabled ? 1 : 0)));
        compoundTag.Add("EnabledInHierarchy", new SerializedProperty((byte)(_enabledInHierarchy ? 1 : 0)));

        compoundTag.Add("TagIndex", new SerializedProperty(tagIndex));
        compoundTag.Add("LayerIndex", new SerializedProperty(layerIndex));

        compoundTag.Add("HideFlags", new SerializedProperty((int)hideFlags));

        compoundTag.Add("Transform", Serializer.Serialize(_transform, ctx));
        compoundTag.Add("PrefabLink", Serializer.Serialize(prefabLink, ctx));

        if (AssetID != Guid.Empty)
        {
            compoundTag.Add("AssetID", new SerializedProperty(AssetID.ToString()));
            if (FileID != 0)
                compoundTag.Add("FileID", new SerializedProperty(FileID));
        }

        SerializedProperty components = SerializedProperty.NewList();
        foreach (MonoBehaviour comp in _components.Values)
            components.ListAdd(Serializer.Serialize(comp, ctx));
        compoundTag.Add("Components", components);

        SerializedProperty children = SerializedProperty.NewList();
        foreach (GameObject child in this.children)
            children.ListAdd(Serializer.Serialize(child, ctx));
        compoundTag.Add("Children", children);

        return compoundTag;
    }

    /// <summary>
    /// Deserializes the GameObject from a SerializedProperty.
    /// </summary>
    /// <param name="value">The SerializedProperty containing the GameObject's data.</param>
    /// <param name="ctx">The serialization context.</param>
    public void Deserialize(SerializedProperty value, Serializer.SerializationContext ctx)
    {
        Name = value["Name"].StringValue;
        _static = value["Static"]?.ByteValue == 1;
        _enabled = value["Enabled"]?.ByteValue == 1;
        _enabledInHierarchy = value["EnabledInHierarchy"]?.ByteValue == 1;
        tagIndex = value["TagIndex"]?.ByteValue ?? 0;
        layerIndex = value["LayerIndex"]?.ByteValue ?? 0;
        hideFlags = (HideFlags)value["HideFlags"]?.IntValue!;

        _transform = Serializer.Deserialize<Transform>(value["Transform"], ctx);
        _transform.gameObject = this;
        if (value.TryGet("PrefabLink", out SerializedProperty? link))
        {
            prefabLink = Serializer.Deserialize<PrefabLink>(link, ctx);
            if (prefabLink != null)
                prefabLink.Obj = this;
        }

        if (value.TryGet("AssetID", out SerializedProperty? guid))
            AssetID = Guid.Parse(guid.StringValue);
        if (value.TryGet("FileID", out SerializedProperty? fileID))
            FileID = fileID.UShortValue;

        SerializedProperty children = value["Children"];
        this.children = [];
        foreach (SerializedProperty childTag in children.List)
        {
            GameObject? child = Serializer.Deserialize<GameObject>(childTag, ctx);
            if (child == null) continue;
            child._parent = this;
            this.children.Add(child);
        }

        SerializedProperty comps = value["Components"];
        _components = [];
        foreach (SerializedProperty compTag in comps.List)
        {
            // Fallback for Missing Type
            SerializedProperty? typeProperty = compTag.Get("$type");
            // If the type is missing or string null/whitespace something is wrong, so just let the Deserializer handle it, maybe it knows what to do
            if (typeProperty != null && !string.IsNullOrWhiteSpace(typeProperty.StringValue))
            {
                // Look for Monobehaviour Type
                Type oType = RuntimeUtils.FindType(typeProperty.StringValue);
                if (oType == null)
                {
                    Debug.LogWarning("Missing Monobehaviour Type: " + typeProperty.StringValue + " On " + Name);
                    MissingMonobehaviour missing = new MissingMonobehaviour();
                    missing.ComponentData = compTag;
                    _components.Add(typeof(MissingMonobehaviour), missing);
                    continue;
                }
                else if (oType == typeof(MissingMonobehaviour))
                {
                    HandleMissingComponent(compTag, ctx);
                    continue;
                }
            }

            MonoBehaviour? component = Serializer.Deserialize<MonoBehaviour>(compTag, ctx);
            if (component == null) continue;
            _components.Add(component.GetType(), component);
        }
        // Attach all components
        foreach (KeyValuePair<Type, MonoBehaviour> comp in _components)
            comp.Value.AttachToGameObject(this);
    }

    /// <summary>
    /// Handles a missing component by attempting to recover it.
    /// </summary>
    /// <param name="compTag">The SerializedProperty containing the component data.</param>
    /// <param name="ctx">The serialization context.</param>
    private void HandleMissingComponent(SerializedProperty compTag, Serializer.SerializationContext ctx)
    {
        // Were missing! see if we can recover
        MissingMonobehaviour missing = Serializer.Deserialize<MissingMonobehaviour>(compTag, ctx);
        SerializedProperty oldData = missing.ComponentData;
        // Try to recover the component
        if (oldData.TryGet("$type", out SerializedProperty? typeProp))
        {
            Type oType = RuntimeUtils.FindType(typeProp.StringValue);
            if (oType != null)
            {
                // We have the type! Deserialize it and add it to the components
                MonoBehaviour? component = Serializer.Deserialize<MonoBehaviour>(oldData);
                if (component != null)
                {
                    _components.Add(component.GetType(), component);
                }
            }
        }
    }

    /// <summary>
    /// Creates a deep copy of this GameObject.
    /// </summary>
    /// <returns>A reference to a newly created deep copy of this GameObject.</returns>
    public new GameObject Clone()
    {
        return this.DeepClone();
    }

    /// <summary>
    /// Deep-copies this GameObject's data to the specified target GameObject.
    /// </summary>
    /// <param name="target">The target GameObject to copy to.</param>
    public void CopyTo(GameObject target)
    {
        this.DeepCopyTo(target);
    }

    void ICloneExplicit.SetupCloneTargets(object targetObj, ICloneTargetSetup setup)
    {
        GameObject target = targetObj as GameObject;
        bool isPrefabApply = setup.Context is ApplyPrefabContext;

        // We don't destroy anything when Applying a prefab
        // Since the user could have added new components or children those should stay
        if (!isPrefabApply)
        {
            // Destroy additional Components in the target GameObject
            if (target._components.Count > 0)
            {
                List<Type> removeComponentTypes = null;
                foreach (KeyValuePair<Type, MonoBehaviour> targetComp in target._components)
                {
                    if (!_components.ContainsKey(targetComp.Key))
                    {
                        removeComponentTypes ??= [];
                        removeComponentTypes.Add(targetComp.Key);
                    }
                }
                if (removeComponentTypes != null)
                {
                    foreach (Type type in removeComponentTypes)
                    {
                        target.RemoveComponent(type);
                    }
                }
            }

            // Destroy additional child objects in the target GameObject
            if (target.children != null)
            {
                int thisChildCount = children != null ? children.Count : 0;
                for (int i = target.children.Count - 1; i >= thisChildCount; i--)
                {
                    target.children[i].DestroyImmediate();
                }
            }
        }

        // Create missing Components in the target GameObject
        foreach (KeyValuePair<Type, MonoBehaviour> pair in _components)
        {
            if (target._components.ContainsKey(pair.Key))
                continue;

            MonoBehaviour targetComponent = target.AddComponent(pair.Key);
            setup.HandleObject(pair.Value, targetComponent, CloneBehavior.ChildObject);
        }

        // Create missing child objects in the target GameObject
        if (children != null)
        {
            for (int i = 0; i < children.Count; i++)
            {
                GameObject targetChild;
                if (target.children != null && target.children.Count > i)
                    targetChild = target.children[i];
                else
                {
                    targetChild = new GameObject();
                    targetChild.SetParent(target);
                }

                setup.HandleObject(children[i], targetChild, CloneBehavior.ChildObject);
            }
        }

        // Handle referenced and child objects
        setup.HandleObject(_transform, target._transform, CloneBehavior.ChildObject);
        setup.HandleObject(_scene, target._scene, CloneBehavior.Reference);
        setup.HandleObject(prefabLink, target.prefabLink);
    }

    void ICloneExplicit.CopyDataTo(object targetObj, ICloneOperation operation)
    {
        GameObject target = targetObj as GameObject;

        // Copy plain old data
        target.Name = Name;
        target._static = _static;
        target._enabled = _enabled;
        target._enabledInHierarchy = _enabledInHierarchy;
        target.tagIndex = tagIndex;
        target.layerIndex = layerIndex;
        target.hideFlags = hideFlags;
        if (!operation.Context.PreserveIdentity)
            target._instanceID = _instanceID;

        target.AssetID = AssetID;
        target.FileID = FileID;

        operation.HandleObject<Transform>(_transform);

        // Copy Components from source to target
        for (int i = 0; i < _components.Count; i++)
        {
            operation.HandleObject<MonoBehaviour>(_components.Values.ElementAt(i));
        }

        // Copy child objects from source to target
        if (children != null)
        {
            for (int i = 0; i < children.Count; i++)
            {
                operation.HandleObject<GameObject>(children[i]);
            }
        }

        // Copy the objects parent scene as a weak reference, i.e.
        // by assignment, and only when the the scene is itself part of the
        // copied object graph. That way, cloning a GameObject but not its
        // scene will result in a clone that doesn't reference a parent scene.
        Scene targetScene = operation.GetWeakTarget(_scene);
        if (targetScene != null)
        {
            target._scene = targetScene;
        }

        // Copy the objects prefab link
        operation.HandleObject(prefabLink, ref target.prefabLink, true);
    }
}
