using UnityEditor;
using UnityEngine;

namespace NeonPrism.EditorTools
{
    public static class NeonPrismSetupTools
    {
        [MenuItem("Tools/Neon Prism/Create Folder Structure")]
        public static void CreateFolders()
        {
            string[] folders = { "Prefabs", "Materials", "Scripts", "Scenes", "Sprites", "Tilemaps" };
            foreach (string f in folders)
            {
                if (!AssetDatabase.IsValidFolder("Assets/" + f)) AssetDatabase.CreateFolder("Assets", f);
            }
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Neon Prism/Create Bouncy Physics Material")]
        public static void CreateBouncyMaterial()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");

            const string path = "Assets/Materials/Bouncy_PhysicsMat2D.physicsMaterial2D";
            if (AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path) != null)
            {
                Debug.Log("Bouncy_PhysicsMat2D уже существует.");
                return;
            }

            PhysicsMaterial2D mat = new PhysicsMaterial2D("Bouncy_PhysicsMat2D")
            {
                friction = 0f,
                bounciness = 1f
            };
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = mat;
        }
    }
}
