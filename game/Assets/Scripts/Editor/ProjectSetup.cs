using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DTK.EditorTools
{
    /// <summary>
    /// One-click (or command line) project setup so nobody has to click around in the editor:
    /// base materials, the Main scene, portrait window settings, lighter renderer for an integrated GPU.
    /// Menu: DTK > Kurulum.  Batch: -executeMethod DTK.EditorTools.ProjectSetup.RunBatch
    /// </summary>
    public static class ProjectSetup
    {
        const string MatDir = "Assets/Resources/Materials";
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("DTK/Kurulum (sahne + materyaller + ayarlar)")]
        public static void Run()
        {
            Directory.CreateDirectory(MatDir);
            CreateMaterials();
            var sky = CreateSky();
            CreateScene(sky);
            ConfigurePlayer();
            ConfigureRenderer();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DTK] Kurulum tamam.");
        }

        public static void RunBatch()
        {
            Run();
        }

        static Material Save(Material m, string name)
        {
            string path = $"{MatDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.shader = m.shader;
                existing.CopyPropertiesFromMaterial(m);
                existing.shaderKeywords = m.shaderKeywords;
                existing.renderQueue = m.renderQueue;
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void CreateMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");

            var mLit = new Material(lit) { enableInstancing = true };
            mLit.SetColor("_BaseColor", Color.white);
            mLit.SetFloat("_Smoothness", 0.25f);
            Save(mLit, "M_Lit");

            var mEm = new Material(lit) { enableInstancing = true };
            mEm.SetColor("_BaseColor", Color.white);
            mEm.EnableKeyword("_EMISSION");
            mEm.SetColor("_EmissionColor", Color.white);
            mEm.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            Save(mEm, "M_LitEmissive");

            var mTr = new Material(lit);
            mTr.SetColor("_BaseColor", new Color(1, 1, 1, 0.8f));
            mTr.SetFloat("_Surface", 1f);
            mTr.SetFloat("_Blend", 0f);
            mTr.SetFloat("_Cull", (float)CullMode.Off);
            mTr.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mTr.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mTr.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mTr.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mTr.SetFloat("_ZWrite", 0f);
            mTr.SetOverrideTag("RenderType", "Transparent");
            mTr.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mTr.renderQueue = (int)RenderQueue.Transparent;
            mTr.SetShaderPassEnabled("DepthOnly", false);
            mTr.SetShaderPassEnabled("ShadowCaster", false);
            Save(mTr, "M_LitTransparent");

            var mUn = new Material(unlit);
            mUn.SetColor("_BaseColor", Color.white);
            Save(mUn, "M_Unlit");

            var crowd = Shader.Find("DTK/Crowd");
            if (crowd != null) Save(new Material(crowd), "M_Crowd");
            else Debug.LogError("[DTK] DTK/Crowd shader bulunamadı");

            var vc = Shader.Find("DTK/VertexColorLit");
            if (vc != null) Save(new Material(vc), "M_VertexColor");
            else Debug.LogError("[DTK] DTK/VertexColorLit shader bulunamadı");
        }

        static Material CreateSky()
        {
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.03f);
            sky.SetFloat("_AtmosphereThickness", 0.8f);
            sky.SetColor("_SkyTint", new Color(0.45f, 0.62f, 0.95f));
            sky.SetColor("_GroundColor", new Color(0.32f, 0.36f, 0.33f));
            sky.SetFloat("_Exposure", 1.15f);
            return Save(sky, "M_Sky");
        }

        static void CreateScene(Material sky)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var game = new GameObject("Game");
            var type = System.Type.GetType("DTK.Game.GameBootstrap, DTK.Game");
            if (type != null) game.AddComponent(type);
            else Debug.LogError("[DTK] GameBootstrap bulunamadı (DTK.Game derlenmedi mi?)");
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = false;
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (File.Exists("Assets/Scenes/SampleScene.unity")) AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
            if (File.Exists("Assets/Settings/SampleSceneProfile.asset")) AssetDatabase.DeleteAsset("Assets/Settings/SampleSceneProfile.asset");
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Berk";
            PlayerSettings.productName = "Dunya Taraftar Kupasi";
            PlayerSettings.defaultScreenWidth = 608;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.forceSingleInstance = true;
            PlayerSettings.allowFullscreenSwitch = false;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        }

        static void ConfigureRenderer()
        {
            // Screen-space AO is expensive on an integrated GPU and barely visible on a phone; switch it off.
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;
                foreach (var f in data.rendererFeatures)
                    if (f != null && f.GetType().Name.Contains("AmbientOcclusion")) f.SetActive(false);
                EditorUtility.SetDirty(data);
            }
        }
    }

    public static class BuildScript
    {
        public const string ExePath = "../Builds/Windows/DunyaTaraftarKupasi.exe";

        [MenuItem("DTK/Windows için derle")]
        public static void BuildWindows()
        {
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = ExePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("[DTK] Derleme sonucu: " + report.summary.result + " -> " + Path.GetFullPath(ExePath) + " (" + report.summary.totalSize / (1024 * 1024) + " MB)");
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>Batch: setup + build in one editor launch.</summary>
        public static void SetupAndBuild()
        {
            ProjectSetup.Run();
            BuildWindows();
        }
    }
}
