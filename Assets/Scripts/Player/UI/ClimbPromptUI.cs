using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;

public class ClimbPromptUI : MonoBehaviour
{
    public Text promptText;
    EntityManager _em;
    EntityQuery _localPlayerQuery;

    void Awake()
    {
        _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        _localPlayerQuery = _em.CreateEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(PlayerTag), typeof(Player.Components.PlayerInputComponent) }
        });

        if (promptText) promptText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (_localPlayerQuery.IsEmptyIgnoreFilter) return;

        using var players = _localPlayerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        bool shown = false;
        foreach (var ent in players)
        {
            if (_em.HasComponent(ent, typeof(Player.Components.FreeClimbCandidate)))
            {
                if (promptText)
                {
                    promptText.text = "Press Space to Climb";
                    promptText.gameObject.SetActive(true);
                }
                shown = true;
                break;
            }
        }

        if (!shown && promptText) promptText.gameObject.SetActive(false);
    }
}
