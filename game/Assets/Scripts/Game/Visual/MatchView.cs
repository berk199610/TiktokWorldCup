using DTK.Sim;
using UnityEngine;

namespace DTK.Game
{
    /// <summary>
    /// Turns the simulation state into the picture: ball, players, stands. It never decides anything,
    /// it only reads TournamentSim/MatchSim and reacts to SimEvents.
    /// </summary>
    public class MatchView : MonoBehaviour
    {
        public Stadium Stadium { get; private set; }
        public TeamView[] Teams { get; private set; }
        public BallView Ball { get; private set; }
        public Vector3 BallPos { get; private set; }
        public MatchSim Match => tournament?.Match;

        TournamentSim tournament;
        MatchSim boundMatch;
        float smoothZ, wanderT;
        float goalAnimT = -1, saveAnimT = -1, postAnimT = -1;
        int animTeam;
        Vector3 animFrom;
        float lateral;

        public static MatchView Build(Transform parent, TournamentSim t)
        {
            var go = new GameObject("MatchView");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<MatchView>();
            v.tournament = t;
            v.Stadium = Stadium.Build(go.transform);
            var actors = new GameObject("Actors").transform;
            actors.SetParent(go.transform, false);
            v.Teams = new[] { new TeamView(actors, 0), new TeamView(actors, 1) };
            v.Ball = BallView.Build(actors);
            v.ApplyKits("TR", "VN");
            return v;
        }

        public void ApplyKits(string home, string away)
        {
            Flags.MatchKits(home, away, out var hk, out var ak);
            Color gkA = new Color(1f, 0.85f, 0.1f), gkB = new Color(0.2f, 0.85f, 0.35f);
            if (Flags.Distance(gkA, hk[0]) < 0.4f || Flags.Distance(gkA, ak[0]) < 0.4f) gkA = new Color(0.55f, 0.25f, 0.85f);
            if (Flags.Distance(gkB, hk[0]) < 0.4f || Flags.Distance(gkB, ak[0]) < 0.4f) gkB = new Color(0.1f, 0.75f, 0.85f);
            Teams[0].SetKit(hk, gkA);
            Teams[1].SetKit(ak, gkB);
            // team 0's fans sit behind team 0's goal (near the camera), team 1's behind the far goal
            Stadium.NearStand.SetColors(hk[0], hk[1], Flags.Get(home));
            Stadium.FarStand.SetColors(ak[0], ak[1], Flags.Get(away));
        }

        /// <summary>World position of the head of the player who represents this team right now (for name tags).</summary>
        public Vector3 TagAnchor(int team, bool keeper = false)
        {
            if (team < 0 || team > 1) return BallPos + Vector3.up * 2.5f;
            var p = keeper ? Teams[team].Keeper : Teams[team].NearestTo(BallPos);
            return p.Head.position + Vector3.up * 0.55f;
        }

        public void OnEvent(SimEvent e)
        {
            var m = Match;
            switch (e.kind)
            {
                case SimEventKind.MatchStarted:
                    if (m != null) BindMatch(m);
                    break;
                case SimEventKind.Kickoff:
                    if (m != null && boundMatch != m) BindMatch(m);
                    goalAnimT = saveAnimT = postAnimT = -1;
                    Teams[0].ResetToKickoff(e.team == 0);
                    Teams[1].ResetToKickoff(e.team == 1);
                    smoothZ = 0; lateral = 0;
                    break;
                case SimEventKind.NameTag:
                    if (e.team >= 0 && e.team <= 1 && e.style != "bar")
                    {
                        var p = Teams[e.team].NearestTo(BallPos);
                        if (e.style == "calim") p.Hop(); else p.Kick();
                        Ball.Hop(e.style == "calim" ? 0.4f : 0.25f);
                    }
                    break;
                case SimEventKind.ChanceResolved:
                    animTeam = e.team;
                    animFrom = BallPos;
                    var r = (ChanceResult)e.value;
                    Teams[e.team].NearestTo(BallPos)?.Kick();
                    if (r == ChanceResult.Goal) goalAnimT = 0;
                    else if (r == ChanceResult.Save) { saveAnimT = 0; Teams[1 - e.team].Keeper.Dive(lateral >= 0 ? 1 : -1); }
                    else postAnimT = 0;
                    break;
                case SimEventKind.Goal:
                    {
                        float dir = Field.AttackDir(e.team);
                        var corner = new Vector3((lateral >= 0 ? 1 : -1) * (Field.HalfW - 1.5f), 0, dir * (Field.HalfL - 1.5f));
                        Teams[e.team].CelebrateAt(corner, 5f);
                        (e.team == 0 ? Stadium.NearStand : Stadium.FarStand).Burst(5f);
                        break;
                    }
                case SimEventKind.Choreo:
                    (e.team == 0 ? Stadium.NearStand : Stadium.FarStand).ShowChoreo(e.duration);
                    break;
                case SimEventKind.Cinematic:
                    if (e.tier >= 5) Stadium.SetDramatic(true);
                    break;
                case SimEventKind.ChanceWindow:
                    Stadium.SetDramatic(false);
                    break;
                case SimEventKind.Champion:
                    Stadium.Trophy.gameObject.SetActive(true);
                    Stadium.Trophy.localPosition = new Vector3(0, 0, 0);
                    break;
                case SimEventKind.TournamentPhase:
                    if (e.text == TournamentPhase.Registration.ToString())
                    {
                        Stadium.Trophy.gameObject.SetActive(false);
                        Stadium.SetDramatic(false);
                    }
                    break;
            }
        }

        void BindMatch(MatchSim m)
        {
            boundMatch = m;
            ApplyKits(m.Teams[0].code, m.Teams[1].code);
            Teams[0].ResetToKickoff(m.KickoffTeam == 0);
            Teams[1].ResetToKickoff(m.KickoffTeam == 1);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var m = Match;
            if (m == null || tournament.Phase != TournamentPhase.Match)
            {
                // between matches: players stand in shape, ball on the spot, crowd calm
                Ball.SetPosition(Vector3.zero);
                BallPos = Vector3.zero;
                Teams[0].UpdateFormation(Vector3.zero, false, true);
                Teams[1].UpdateFormation(Vector3.zero, false, true);
                bool ceremony = tournament.Phase == TournamentPhase.Ceremony;
                Stadium.NearStand.SetExcitement(ceremony ? 0.9f : 0.15f);
                Stadium.FarStand.SetExcitement(ceremony ? 0.9f : 0.15f);
                if (ceremony && Stadium.Trophy.gameObject.activeSelf) Stadium.Trophy.Rotate(Vector3.up, 30f * dt, Space.World);
                return;
            }
            if (boundMatch != m) BindMatch(m);

            // --- ball along the pitch: follow the sim smoothly (it never teleports, DESIGN §6.1.4)
            float simZ = Field.Z(m.BallPos);
            smoothZ = Mathf.Lerp(smoothZ, simZ, 1 - Mathf.Exp(-dt * 8f));
            wanderT += dt;
            bool chance = m.Phase == MatchPhase.Chance;
            float wander = Mathf.Sin(wanderT * 0.31f) * 6.5f + Mathf.Sin(wanderT * 0.83f + 1.3f) * 2.5f;
            float nearGoal = Mathf.InverseLerp(12f, Field.HalfL, Mathf.Abs(smoothZ));
            float targetX = chance ? Mathf.Sign(wander) * 1.2f : Mathf.Lerp(wander, wander * 0.25f, nearGoal);
            lateral = Mathf.Lerp(lateral, targetX, 1 - Mathf.Exp(-dt * 1.5f));
            Vector3 ground = new Vector3(lateral, 0, smoothZ);
            float extraY = 0;

            if (goalAnimT >= 0)
            {
                goalAnimT += dt;
                float k = Mathf.Clamp01(goalAnimT / 0.7f);
                float dir = Field.AttackDir(animTeam);
                var into = new Vector3(Mathf.Clamp(animFrom.x, -Field.GoalHalfW + 0.6f, Field.GoalHalfW - 0.6f), 0, dir * (Field.HalfL + Field.GoalDepth * 0.7f));
                ground = Vector3.Lerp(new Vector3(animFrom.x, 0, animFrom.z), into, k);
                extraY = Mathf.Sin(k * Mathf.PI) * 0.6f + k * 0.6f;
            }
            else if (postAnimT >= 0)
            {
                postAnimT += dt;
                float dir = Field.AttackDir(animTeam);
                var post = new Vector3(Field.GoalHalfW * (lateral >= 0 ? 1 : -1), 0, dir * Field.HalfL);
                if (postAnimT < 0.45f) ground = Vector3.Lerp(new Vector3(animFrom.x, 0, animFrom.z), post, postAnimT / 0.45f);
                else { postAnimT = -1; Ball.Hop(1.2f); }
                extraY = postAnimT >= 0 ? postAnimT * 1.6f : 0;
            }
            else if (saveAnimT >= 0)
            {
                saveAnimT += dt;
                if (saveAnimT > 0.5f) { saveAnimT = -1; Ball.Hop(1.5f); }
            }
            if (m.Phase == MatchPhase.GoalCelebration && goalAnimT < 0)
            {
                float dir = Field.AttackDir(m.LastGoalTeam < 0 ? 0 : m.LastGoalTeam);
                ground = new Vector3(0, 0, dir * (Field.HalfL + Field.GoalDepth * 0.7f));
            }
            Ball.SetPosition(ground, extraY);
            BallPos = ground;

            // --- players
            bool frozen = chance || m.Phase == MatchPhase.Kickoff || m.Phase == MatchPhase.GoalCelebration;
            int attacking = m.BallPos > 0 ? 0 : 1;
            if (chance && m.Chance != null) attacking = m.Chance.attacker;
            if (m.Phase != MatchPhase.GoalCelebration)
            {
                Teams[0].UpdateFormation(ground, attacking == 0, frozen);
                Teams[1].UpdateFormation(ground, attacking == 1, frozen);
                if (chance && m.Chance != null)
                {
                    // shooter stands behind the ball, keeper sets
                    var shooter = Teams[m.Chance.attacker].NearestTo(ground);
                    shooter.Target = ground - new Vector3(0, 0, Field.AttackDir(m.Chance.attacker) * 0.9f);
                }
            }
            // --- stands react to their team's recent power (DESIGN §8 "Ses" idea, here visually)
            float share = (float)(m.Chance != null ? m.Chance.ShotShare : 0.5);
            Stadium.NearStand.SetExcitement(chance ? (m.Chance.attacker == 0 ? share : 1 - share) * 0.9f : 0.2f);
            Stadium.FarStand.SetExcitement(chance ? (m.Chance.attacker == 1 ? share : 1 - share) * 0.9f : 0.2f);
        }
    }
}
