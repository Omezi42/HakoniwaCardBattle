using UnityEngine;
using Fusion;
using System.Collections.Generic;

public class NetworkPlayerController : NetworkBehaviour
{
    // Static list for easy access
    public static readonly List<NetworkPlayerController> Instances = new List<NetworkPlayerController>();

    [Networked] public PlayerRef Owner { get; set; }
    [Networked] public int JobId { get; set; }

    // Stats
    [Networked] public int CurrentHp { get; set; } = 20;
    [Networked] public int MaxHp { get; set; } = 20;
    [Networked] public int CurrentMana { get; set; } = 0;
    [Networked] public int MaxMana { get; set; } = 0;
    
    // Counts (Visual Sync)
    [Networked] public int HandCount { get; set; }
    [Networked] public int DeckCount { get; set; }
    [Networked] public int GraveCount { get; set; }

    // Events
    public static event System.Action<NetworkPlayerController> OnPlayerSpawned;
    public static event System.Action<NetworkPlayerController> OnPlayerDespawned;

    public override void Spawned()
    {
        Instances.Add(this);
        
        // ★FIX: We MUST use DontDestroyOnLoad if controllers are spawned in a lobby/room scene 
        // that transitions to the game scene. Otherwise they are destroyed on load.
        DontDestroyOnLoad(gameObject);
        
        Debug.Log($"[NetworkPlayerController] Spawned. Owner: {Owner}, ID: {Object.Id}, InputAuthority: {Object.InputAuthority}");
        
        OnPlayerSpawned?.Invoke(this);
        
        // Initial setup if Local Player
        if (Object.HasInputAuthority)
        {
            Debug.Log("[NetworkPlayerController] This is Local Player's Object.");
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Instances.Remove(this);
        OnPlayerDespawned?.Invoke(this);
    }
    
    public bool IsLocalPlayer => Object != null && Runner != null && Owner == Runner.LocalPlayer;

    // Helper to find specific player's controller
    public static NetworkPlayerController Get(PlayerRef player)
    {
        foreach(var pc in Instances)
        {
            // [Fix] Check both InputAuthority and [Networked] Owner as fallback
            if (pc.Object != null && pc.Object.InputAuthority == player) return pc;
            if (pc.Owner == player) return pc;
        }
        return null;
    }
}
