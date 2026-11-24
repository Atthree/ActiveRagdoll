using UnityEditor;
using UnityEngine;

namespace Atthree.Ragdoll.Editor
{
    /// <summary>Tools > Active Ragdoll > Builder penceresi.</summary>
    public class RagdollBuilderWindow : EditorWindow
    {
        GameObject character;
        readonly RagdollBuilder.Options options = new RagdollBuilder.Options();

        [MenuItem("Tools/Active Ragdoll/Builder")]
        static void Open()
        {
            var w = GetWindow<RagdollBuilderWindow>("Active Ragdoll");
            w.minSize = new Vector2(320, 260);
            if (Selection.activeGameObject != null) w.character = Selection.activeGameObject;
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "1) Karakteri sahneye koy (ayakları yerde, dik).\n" +
                "2) Aşağıya sürükle ve Build'e bas.\n" +
                "3) Play: WASD yürü, Space zıpla, Sol/Sağ tık kolları uzat ve tut.",
                MessageType.Info);

            character = (GameObject)EditorGUILayout.ObjectField("Karakter", character, typeof(GameObject), true);
            options.totalMass = EditorGUILayout.FloatField("Toplam Kütle (kg)", options.totalMass);
            options.addPlayerInput = EditorGUILayout.Toggle("Oyuncu Girdisi Ekle", options.addPlayerInput);
            options.setupCamera = EditorGUILayout.Toggle("Kamerayı Kur", options.setupCamera);

            if (character == null) return;

            if (EditorUtility.IsPersistent(character))
            {
                EditorGUILayout.HelpBox("Prefab asset'i değil, SAHNEDEKİ karakteri seç.", MessageType.Warning);
                return;
            }

            RagdollBuilder.FindBones(character, out string error);
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Error);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(error != null))
            {
                if (GUILayout.Button("Build", GUILayout.Height(32)))
                {
                    var ragdoll = RagdollBuilder.Build(character, options);
                    if (ragdoll != null) Selection.activeGameObject = ragdoll.gameObject;
                }
            }
            if (GUILayout.Button("Remove"))
            {
                Undo.SetCurrentGroupName("Remove Active Ragdoll");
                RagdollBuilder.Remove(character);
            }
        }
    }
}
