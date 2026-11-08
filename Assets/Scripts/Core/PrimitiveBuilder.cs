using UnityEngine;

namespace SkiGame
{
    /// <summary>Helpers to build objects out of Unity primitives and procedural meshes.</summary>
    public static class PrimitiveBuilder
    {
        /// <summary>
        /// Creates a primitive WITHOUT its collider (visual only), parented and placed in local space.
        /// </summary>
        public static GameObject Create(PrimitiveType type, string name, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material material)
        {
            return Create(type, name, parent, localPosition, Quaternion.identity, localScale, material);
        }

        public static GameObject Create(PrimitiveType type, string name, Transform parent,
            Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false; // disable immediately so it never blocks raycasts this frame
                SafeDestroy(col);
            }
            Place(go.transform, parent, localPosition, localRotation, localScale);
            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (parent != null) go.layer = parent.gameObject.layer;
            return go;
        }

        /// <summary>Creates a visual-only object from a mesh.</summary>
        public static GameObject CreateMesh(Mesh mesh, string name, Transform parent,
            Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            Place(go.transform, parent, localPosition, localRotation, localScale);
            if (parent != null) go.layer = parent.gameObject.layer;
            return go;
        }

        /// <summary>Creates an empty child transform.</summary>
        public static Transform CreateEmpty(string name, Transform parent, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            Place(t, parent, localPosition, Quaternion.identity, Vector3.one);
            return t;
        }

        public static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        /// <summary>Destroy that works both in play mode and in the editor.</summary>
        public static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }

        private static void Place(Transform t, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            if (parent != null) t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = rot;
            t.localScale = scale;
        }
    }
}
