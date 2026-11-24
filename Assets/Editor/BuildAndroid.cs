#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Xianxia.EditorTools
{
    /// <summary>
    /// CI 构建入口：game-ci/unity-builder 的 buildMethod 指向 Xianxia.EditorTools.BuildAndroid.Build。
    /// 工程里刻意不提交 .unity 场景与 PlayerSettings.asset —— 都由这里在构建前生成，
    /// 免得手写 YAML 出现 GUID/序列化错位。
    /// </summary>
    public static class BuildAndroid
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        public static void Build()
        {
            Prepare();

            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Build");
            Directory.CreateDirectory(outDir);
            string path = Path.Combine(outDir, "xianxia.apk");

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[Build] result={report.summary.result} size={report.summary.totalSize} path={path}");
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Android build failed: " + report.summary.result);
        }

        private static void Prepare()
        {
            Directory.CreateDirectory("Assets/Scenes");
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "Xianxia";
            PlayerSettings.productName = "一世";

            string ver = Environment.GetEnvironmentVariable("VERSION");
            if (string.IsNullOrEmpty(ver)) ver = "0.7.0";
            PlayerSettings.bundleVersion = ver;
            PlayerSettings.Android.bundleVersionCode =
                int.TryParse(Environment.GetEnvironmentVariable("BUILD_NUMBER"), out int code) ? Mathf.Max(1, code) : 1;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.xianxia.yishi");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.useCustomKeystore = false;   // 个人测试包：用调试签名，手机可直接安装
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.stripEngineCode = true;

            // 60 帧预算：无实时阴影、无实时 GI、无后处理，画面全靠顶点色与构图
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 0;
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.pixelLightCount = 0;
            QualitySettings.globalTextureMipmapLimit = 1;

            TrySetupUrp();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 用反射创建 URP 资产：避免在仓库里手写 URP 的 YAML（版本字段与脚本 GUID 极易错）。
        /// 失败就退回内置管线 —— 我们的着色器是管线无关的 SRPDefaultUnlit 通道，两条路都能跑。
        /// </summary>
        private static void TrySetupUrp()
        {
            try
            {
                Type assetT = null, dataT = null;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    assetT ??= a.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset");
                    dataT ??= a.GetType("UnityEngine.Rendering.Universal.UniversalRendererData");
                }
                if (assetT == null || dataT == null)
                {
                    Debug.Log("[Build] 未加载 URP 程序集，使用内置管线");
                    return;
                }

                Directory.CreateDirectory("Assets/Settings");
                var data = ScriptableObject.CreateInstance(dataT);
                var create = assetT.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, null, new[] { dataT }, null);
                if (create == null) { Debug.Log("[Build] URP Create 签名不匹配，使用内置管线"); return; }
                var asset = create.Invoke(null, new object[] { data }) as RenderPipelineAsset;
                if (asset == null) { Debug.Log("[Build] URP 资产创建失败，使用内置管线"); return; }

                AssetDatabase.CreateAsset(data, "Assets/Settings/URP-Renderer.asset");
                AssetDatabase.CreateAsset(asset, "Assets/Settings/URP.asset");
                GraphicsSettings.defaultRenderPipeline = asset;
                QualitySettings.renderPipeline = asset;
                Debug.Log("[Build] URP 已挂载");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Build] URP 设置跳过：" + e.Message);
            }
        }
    }
}
#endif