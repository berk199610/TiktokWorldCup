using DTK.Sim;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DTK.Game
{
    /// <summary>
    /// Executes the camera shots the simulation's CameraDirector asks for (DESIGN §11):
    /// a calm fixed main camera, a slow push-in for a chance, a hard cut behind the goal, orbit for cinematics and the ceremony.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        MatchView view;
        RootConfig cfg;
        string shot = "main";
        int shotTeam;
        float shotStart, shotDuration;
        Vector3 pos, look;
        Vector3 fromPos, fromLook;
        float blend = 1, blendTime = 0.6f;
        float fov, fromFov;

        public static CameraRig Build(Transform parent, MatchView view, RootConfig cfg)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<CameraRig>();
            rig.view = view;
            rig.cfg = cfg;
            rig.Cam = go.AddComponent<Camera>();
            rig.Cam.nearClipPlane = 0.3f;
            rig.Cam.farClipPlane = 400f;
            rig.Cam.clearFlags = CameraClearFlags.Skybox;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            go.AddComponent<AudioListener>();
            rig.MainPose(out rig.pos, out rig.look);
            rig.Apply();
            return rig;
        }

        void MainPose(out Vector3 p, out Vector3 l)
        {
            var c = cfg.presentation.camera;
            p = new Vector3(0, c.mainHeight, -c.mainBack);
            l = new Vector3(0, c.mainLookY, c.mainLookZ);
        }

        public void OnEvent(SimEvent e)
        {
            if (e.kind != SimEventKind.CameraShot) return;
            fromPos = pos;
            fromLook = look;
            fromFov = fov;
            shot = e.style;
            shotTeam = e.team;
            shotStart = Time.time;
            shotDuration = e.duration;
            blend = 0;
            blendTime = shot == "goal" ? 0f : (shot == "main" ? 0.9f : Mathf.Max(0.2f, cfg.camera.chanceZoomSec));
        }

        void Target(out Vector3 p, out Vector3 l)
        {
            var c = cfg.presentation.camera;
            MainPose(out p, out l);
            float t = Time.time - shotStart;
            Vector3 ball = view.BallPos;
            switch (shot)
            {
                case "chance":
                    {
                        // slide the whole main camera along the pitch (same angle), the narrower FOV does the zoom
                        float lookZ = Field.AttackDir(shotTeam) > 0 ? c.chanceLookTopZ : c.chanceLookBottomZ;
                        var shift = new Vector3(0, 0, lookZ - l.z);
                        p += shift;
                        l += shift;
                        break;
                    }
                case "goal":
                    {
                        float dir = Field.AttackDir(shotTeam);
                        if (t < 2.2f)
                        {
                            // "net cam": just behind the goal, in front of the first row of the stand, looking back at the pitch
                            p = new Vector3(0.8f, 2.5f, dir * (Field.HalfL + Field.GoalDepth + 0.9f));
                            l = new Vector3(0, 0.9f, dir * (Field.HalfL - 9f));
                        }
                        else
                        {
                            // side view of the celebrating players
                            float a = (t - 2.2f) * 0.25f;
                            var center = new Vector3(0, 1f, dir * (Field.HalfL - 4f));
                            p = center + new Vector3(Mathf.Cos(a) * 14f, 6f, -dir * (9f + Mathf.Sin(a) * 2f));
                            l = center;
                        }
                        break;
                    }
                case "cinematic":
                    {
                        float a = t * 0.9f;
                        var center = ball + Vector3.up * 1f;
                        p = center + new Vector3(Mathf.Sin(a) * c.cinematicOrbitRadius, c.cinematicHeight, -Mathf.Cos(a) * c.cinematicOrbitRadius * Field.AttackDir(shotTeam));
                        l = center;
                        break;
                    }
                case "ceremony":
                    {
                        float a = t * 0.35f;
                        var center = new Vector3(0, 1.6f, 0);
                        p = center + new Vector3(Mathf.Sin(a) * 11f, 4.5f, -Mathf.Cos(a) * 11f);
                        l = center;
                        break;
                    }
            }
        }

        float ShotFov()
        {
            float main = cfg.presentation.camera.fov;
            if (shot == "main") return main;
            if (shot == "chance") return cfg.presentation.camera.chanceFov;
            return cfg.presentation.camera.closeFov;
        }

        void LateUpdate()
        {
            Target(out var tp, out var tl);
            float tf = ShotFov();
            if (blend < 1)
            {
                blend = blendTime <= 0 ? 1 : Mathf.Min(1, blend + Time.deltaTime / blendTime);
                float s = blend * blend * (3 - 2 * blend);
                pos = Vector3.Lerp(fromPos, tp, s);
                look = Vector3.Lerp(fromLook, tl, s);
                fov = Mathf.Lerp(fromFov, tf, s);
            }
            else
            {
                pos = tp;
                look = tl;
                fov = tf;
            }
            Apply();
        }

        void Apply()
        {
            Cam.fieldOfView = fov > 1 ? fov : cfg.presentation.camera.fov;
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
        }
    }
}
