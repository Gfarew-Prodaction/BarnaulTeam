#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HurricaneVR.Framework.Core.Utils
{
    public static class EditorExtensions
    {

        public static string GetName(this ScriptableObject so)
        {
            var path = AssetDatabase.GetAssetPath(so);
            return Path.GetFileNameWithoutExtension(path);
        }

        public static void SetExpandedRecursive(this GameObject go, bool expand)
        {
            try
            {
                var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
                var methodInfo = type.GetMethod("SetExpandedRecursive");

                EditorApplication.ExecuteMenuItem("Window/General/Hierarchy");
                var window = EditorWindow.focusedWindow;

#if UNITY_6000_6_OR_NEWER
                methodInfo.Invoke(window, new object[] { go.GetEntityId(), expand });
#else
                methodInfo.Invoke(window, new object[] { go.GetInstanceID(), expand });
#endif
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

        }

        public static T UndoEnsureComponent<T>(this GameObject obj) where T : UnityEngine.Component
        {
            if (obj.TryGetComponent(out T temp))
                return temp;

            return Undo.AddComponent<T>(obj);
        }

        public static T UndoEnsureComponent<T>(this Transform t) where T : UnityEngine.Component
        {
            return t.gameObject.UndoEnsureComponent<T>();
        }
    }
}

#endif