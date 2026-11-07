using System.Collections.Generic;
using UnityEngine;

namespace DTK.Game
{
    /// <summary>
    /// A footballer built from simple shapes (Aşama 1). Aşama 3 swaps the body for a Quaternius character;
    /// the rest of the code only talks to this class (MoveTo / Kick / Dive / Celebrate / Head).
    /// </summary>
    public class PlayerView : MonoBehaviour
    {
        public Transform Head { get; private set; }
        public bool IsKeeper;
        public float Speed = 7.5f;
        public Vector3 Target;
        Transform body, legL, legR, armL, armR;
        MeshRenderer shirtR, shortsR, sockLR, sockRR, armLR, armRR;
        float runPhase, kickT = -1, diveT = -1, celebrateUntil, diveDir, jumpT = -1;
        Vector3 vel;

        public static PlayerView Build(Transform parent, string name, bool keeper)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var p = root.AddComponent<PlayerView>();
            p.IsKeeper = keeper;
            float s = 1.25f; // a bit larger than life so they read on a phone
            var skin = Mats.Lit(new Color(0.88f, 0.70f, 0.55f));
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(root.transform, false);
            p.body = bodyGo.transform;
            var shirt = MeshKit.Obj("Shirt", p.body, MeshKit.Capsule, Mats.Lit(Color.white), new Vector3(0, 1.22f * s, 0), new Vector3(0.62f, 0.42f, 0.42f) * s);
            var shorts = MeshKit.Obj("Shorts", p.body, MeshKit.Cylinder, Mats.Lit(Color.white), new Vector3(0, 0.86f * s, 0), new Vector3(0.56f, 0.13f, 0.4f) * s);
            p.shirtR = shirt.GetComponent<MeshRenderer>();
            p.shortsR = shorts.GetComponent<MeshRenderer>();
            p.legL = Limb(p.body, "LegL", new Vector3(-0.15f, 0.76f, 0) * s, new Vector3(0.18f, 0.38f, 0.18f) * s, out p.sockLR);
            p.legR = Limb(p.body, "LegR", new Vector3(0.15f, 0.76f, 0) * s, new Vector3(0.18f, 0.38f, 0.18f) * s, out p.sockRR);
            p.armL = Limb(p.body, "ArmL", new Vector3(-0.42f, 1.42f, 0) * s, new Vector3(0.13f, 0.3f, 0.13f) * s, out p.armLR);
            p.armR = Limb(p.body, "ArmR", new Vector3(0.42f, 1.42f, 0) * s, new Vector3(0.13f, 0.3f, 0.13f) * s, out p.armRR);
            var head = MeshKit.Obj("Head", p.body, MeshKit.Sphere, skin, new Vector3(0, 1.80f * s, 0), Vector3.one * 0.36f * s);
            MeshKit.Obj("Hair", head.transform, MeshKit.Sphere, Mats.Lit(new Color(0.15f, 0.1f, 0.08f)), new Vector3(0, 0.18f, -0.05f), new Vector3(1.02f, 0.75f, 1.02f));
            p.Head = head.transform;
            // soft blob shadow helps readability on the bright grass
            return p;
        }

        static Transform Limb(Transform parent, string name, Vector3 hip, Vector3 scale, out MeshRenderer r)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = hip;
            var limb = MeshKit.Obj("Mesh", pivot, MeshKit.Capsule, Mats.Lit(Color.white), new Vector3(0, -scale.y, 0), scale);
            r = limb.GetComponent<MeshRenderer>();
            return pivot;
        }

        public void SetKit(Color shirt, Color shorts, Color socks)
        {
            shirtR.sharedMaterial = Mats.Lit(shirt);
            armLR.sharedMaterial = Mats.Lit(shirt);
            armRR.sharedMaterial = Mats.Lit(shirt);
            shortsR.sharedMaterial = Mats.Lit(shorts);
            sockLR.sharedMaterial = Mats.Lit(socks);
            sockRR.sharedMaterial = Mats.Lit(socks);
        }

        public void Kick() { kickT = 0; }
        public void Dive(float dir) { diveT = 0; diveDir = Mathf.Sign(dir == 0 ? 1 : dir); }
        public void Celebrate(float seconds) { celebrateUntil = Time.time + seconds; }
        public void Hop() { jumpT = 0; }
        public void Snap(Vector3 pos) { transform.localPosition = pos; Target = pos; vel = Vector3.zero; }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 pos = transform.localPosition;
            Vector3 to = Target - pos;
            to.y = 0;
            float dist = to.magnitude;
            Vector3 desired = dist > 0.05f ? to.normalized * Mathf.Min(Speed, dist * 3f) : Vector3.zero;
            vel = Vector3.MoveTowards(vel, desired, dt * 30f);
            pos += vel * dt;
            pos.y = 0;
            transform.localPosition = pos;
            float spd = vel.magnitude;
            if (spd > 0.3f)
            {
                var look = Quaternion.LookRotation(new Vector3(vel.x, 0, vel.z));
                transform.localRotation = Quaternion.Slerp(transform.localRotation, look, dt * 10f);
            }
            // run cycle
            runPhase += dt * (4f + spd * 1.4f);
            float swing = Mathf.Clamp01(spd / Speed) * 45f;
            float a = Mathf.Sin(runPhase) * swing;
            legL.localRotation = Quaternion.Euler(a, 0, 0);
            legR.localRotation = Quaternion.Euler(-a, 0, 0);
            armL.localRotation = Quaternion.Euler(-a * 0.8f, 0, 6);
            armR.localRotation = Quaternion.Euler(a * 0.8f, 0, -6);
            float bob = Mathf.Abs(Mathf.Sin(runPhase)) * 0.08f * Mathf.Clamp01(spd / 2f);
            float lean = Mathf.Clamp01(spd / Speed) * 10f;
            Vector3 bodyPos = new Vector3(0, bob, 0);
            Quaternion bodyRot = Quaternion.Euler(lean, 0, 0);

            if (kickT >= 0)
            {
                kickT += dt;
                float k = Mathf.Sin(Mathf.Clamp01(kickT / 0.35f) * Mathf.PI);
                legR.localRotation = Quaternion.Euler(-80f * k, 0, 0);
                if (kickT > 0.35f) kickT = -1;
            }
            if (diveT >= 0)
            {
                diveT += dt;
                float d = Mathf.Clamp01(diveT / 0.35f);
                float back = diveT > 1.4f ? Mathf.Clamp01((diveT - 1.4f) / 0.5f) : 0;
                float amt = d * (1 - back);
                bodyRot = Quaternion.Euler(0, 0, -diveDir * 80f * amt);
                bodyPos = new Vector3(diveDir * 1.2f * amt, 0.5f * amt * (1 - d * 0.5f), 0);
                armL.localRotation = Quaternion.Euler(0, 0, 160 * amt);
                armR.localRotation = Quaternion.Euler(0, 0, -160 * amt);
                if (diveT > 1.9f) diveT = -1;
            }
            if (jumpT >= 0)
            {
                jumpT += dt;
                bodyPos.y += Mathf.Sin(Mathf.Clamp01(jumpT / 0.5f) * Mathf.PI) * 0.7f;
                if (jumpT > 0.5f) jumpT = -1;
            }
            if (Time.time < celebrateUntil)
            {
                float c = Mathf.Abs(Mathf.Sin(Time.time * 7f));
                armL.localRotation = Quaternion.Euler(0, 0, 150 + c * 20);
                armR.localRotation = Quaternion.Euler(0, 0, -150 - c * 20);
                if (spd < 0.5f) bodyPos.y += c * 0.35f;
            }
            body.localPosition = Vector3.Lerp(body.localPosition, bodyPos, dt * 18f);
            body.localRotation = Quaternion.Slerp(body.localRotation, bodyRot, dt * 14f);
        }
    }

    /// <summary>Five players of one side and their simple formation around the ball (looks only, DESIGN §6.1 item 5).</summary>
    public class TeamView
    {
        public readonly int Index;
        public readonly List<PlayerView> Players = new List<PlayerView>();
        public PlayerView Keeper => Players[0];
        float Dir => Field.AttackDir(Index);
        // distance from own goal line, x
        static readonly Vector2[] Shape = { new Vector2(0, 0.8f), new Vector2(-5f, 9f), new Vector2(5f, 9f), new Vector2(-4f, 21f), new Vector2(4f, 21f) };

        public TeamView(Transform parent, int index)
        {
            Index = index;
            for (int i = 0; i < 5; i++) Players.Add(PlayerView.Build(parent, $"T{index}_P{i}", i == 0));
            ResetToKickoff(false);
        }

        public void SetKit(Color[] kit, Color keeperShirt)
        {
            for (int i = 0; i < Players.Count; i++)
                Players[i].SetKit(i == 0 ? keeperShirt : kit[0], i == 0 ? new Color(0.1f, 0.1f, 0.12f) : kit[1], i == 0 ? keeperShirt : kit[0]);
        }

        public void ResetToKickoff(bool kicking)
        {
            for (int i = 0; i < Players.Count; i++)
            {
                var s = Shape[i];
                float fromGoal = Mathf.Min(s.y, Field.HalfL - 1.5f);
                var p = new Vector3(s.x, 0, -Dir * Field.HalfL + Dir * fromGoal);
                if (kicking && i == 4) p = new Vector3(0.6f, 0, -Dir * 0.5f);
                Players[i].Snap(p);
                Players[i].transform.localRotation = Quaternion.LookRotation(new Vector3(0, 0, Dir));
            }
        }

        /// <summary>Move every player relative to the ball. chaser = index of the player who presses the ball.</summary>
        public void UpdateFormation(Vector3 ball, bool attacking, bool frozen)
        {
            float ownGoalZ = -Dir * Field.HalfL;
            // how far the ball is up the pitch from our point of view (0 = our goal, 1 = their goal)
            float progress = Mathf.InverseLerp(ownGoalZ, -ownGoalZ, ball.z);
            int chaser = -1; float best = float.MaxValue;
            for (int i = 1; i < Players.Count; i++)
            {
                float d = (Players[i].transform.localPosition - ball).sqrMagnitude;
                if (d < best) { best = d; chaser = i; }
            }
            for (int i = 0; i < Players.Count; i++)
            {
                var s = Shape[i];
                Vector3 t;
                if (i == 0)
                {
                    float x = Mathf.Clamp(ball.x * 0.35f, -Field.GoalHalfW + 0.4f, Field.GoalHalfW - 0.4f);
                    t = new Vector3(x, 0, ownGoalZ + Dir * (0.8f + (1 - progress) * 1.2f));
                }
                else
                {
                    float push = Mathf.Lerp(-3f, 9f, progress) + (attacking ? 2f : -2f);
                    float fromGoal = Mathf.Clamp(s.y + push, 3f, Field.HalfL * 2 - 3f);
                    float x = s.x + ball.x * 0.45f;
                    t = new Vector3(Mathf.Clamp(x, -Field.HalfW + 1f, Field.HalfW - 1f), 0, ownGoalZ + Dir * fromGoal);
                    if (i == chaser && !frozen) t = ball - new Vector3(0, 0, Dir * 0.7f);
                }
                Players[i].Target = t;
            }
        }

        public PlayerView NearestTo(Vector3 p, bool includeKeeper = false)
        {
            PlayerView best = null; float bd = float.MaxValue;
            for (int i = includeKeeper ? 0 : 1; i < Players.Count; i++)
            {
                float d = (Players[i].transform.localPosition - p).sqrMagnitude;
                if (d < bd) { bd = d; best = Players[i]; }
            }
            return best;
        }

        public void CelebrateAt(Vector3 spot, float seconds)
        {
            for (int i = 1; i < Players.Count; i++)
            {
                Players[i].Target = spot + new Vector3((i - 2.5f) * 0.9f, 0, (i % 2) * 0.9f);
                Players[i].Celebrate(seconds);
            }
        }
    }

    /// <summary>The ball. Rolls with its motion and does a little hop on kicks.</summary>
    public class BallView : MonoBehaviour
    {
        const float Radius = 0.32f;
        Vector3 last;
        float hop;

        public static BallView Build(Transform parent)
        {
            var go = new GameObject("Ball");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<BallView>();
            var vis = new GameObject("Mesh");
            vis.transform.SetParent(go.transform, false);
            vis.AddComponent<MeshFilter>().sharedMesh = MeshKit.SoccerBall(Radius);
            var mr = vis.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Lit(Color.white, 0.55f);
            // the ball mesh carries black/white in vertex colours, so a vertex-colour material is used when available
            var vc = Resources.Load<Material>("Materials/M_VertexColor");
            if (vc != null) mr.sharedMaterial = vc;
            return b;
        }

        public void Hop(float height = 0.8f) { hop = Mathf.Max(hop, height); }

        public void SetPosition(Vector3 groundPos, float extraHeight = 0)
        {
            hop = Mathf.MoveTowards(hop, 0, Time.deltaTime * 2.2f);
            var p = groundPos + Vector3.up * (Radius + extraHeight + Mathf.Sin(Mathf.Clamp01(hop) * Mathf.PI * 0.5f) * hop);
            Vector3 d = p - last;
            d.y = 0;
            if (d.sqrMagnitude > 1e-6f)
            {
                var axis = Vector3.Cross(Vector3.up, d.normalized);
                transform.GetChild(0).Rotate(axis, d.magnitude / Radius * Mathf.Rad2Deg, Space.World);
            }
            transform.localPosition = p;
            last = p;
        }
    }
}
