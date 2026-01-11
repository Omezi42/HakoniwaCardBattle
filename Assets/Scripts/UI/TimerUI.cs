using UnityEngine;
using TMPro;
using Fusion;

public class TimerUI : MonoBehaviour
{
    public TextMeshProUGUI timerText;
    private GameStateController gameState;

    void Start()
    {
        if (timerText == null) timerText = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        if (gameState == null)
        {
            gameState = FindObjectOfType<GameStateController>();
        }

        if (gameState != null && gameState.Object != null && gameState.Object.IsValid)
        {
            if (timerText != null)
            {
                // Round up
                int time = Mathf.CeilToInt(gameState.TurnTimer);
                if (time < 0) time = 0;
                timerText.text = time.ToString();
                
                // Color change for urgency
                if (time <= 10) timerText.color = Color.red;
                else timerText.color = Color.white;
            }
        }
    }
}
