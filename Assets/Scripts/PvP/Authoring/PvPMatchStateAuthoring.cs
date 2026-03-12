using Unity.Entities;
using UnityEngine;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Authoring component for PvP match state singleton.
    /// Place on a singleton entity in arena subscene.
    /// </summary>
    [AddComponentMenu("DIG/PvP/Match State")]
    public class PvPMatchStateAuthoring : MonoBehaviour
    {
        [Tooltip("Default match duration in seconds.")]
        [Min(60f)] public float DefaultMatchDuration = 600f;

        [Tooltip("Enable overtime on tied scores.")]
        public bool OvertimeEnabled = true;

        public class Baker : Baker<PvPMatchStateAuthoring>
        {
            public override void Bake(PvPMatchStateAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PvPMatchState
                {
                    Phase = PvPMatchPhase.WaitingForPlayers,
                    GameMode = PvPGameMode.TeamDeathmatch,
                    MapId = 0,
                    OvertimeEnabled = authoring.OvertimeEnabled ? (byte)1 : (byte)0,
                    Timer = 0f,
                    MatchDuration = authoring.DefaultMatchDuration,
                    MaxScore = 50,
                    TeamScore0 = 0,
                    TeamScore1 = 0,
                    TeamScore2 = 0,
                    TeamScore3 = 0
                });
            }
        }
    }
}
