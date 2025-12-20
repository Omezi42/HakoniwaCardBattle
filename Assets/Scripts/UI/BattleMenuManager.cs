using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Fusion;

public class BattleMenuManager : MonoBehaviour
{
    [Header("UIパーツ")]
    public GameObject menuPanel; // メニュー画面全体
    public Button menuButton;    // 画面左上のハンバーガーボタン
    
    [Header("メニュー内ボタン")]
    public Button closeButton;   // 閉じる
    public Button retireButton;  // リタイア
    public Button logButton;     // ログ（未実装なら無効化）
    public Button settingsButton;// 設定（未実装なら無効化）

    [Header("設定パネル")]
    public GameObject settingsPanel;
    public Slider bgmSlider;
    public Slider seSlider;
    public Button closeSettingsButton;

    // 音量管理用（本来はAudioManagerなどを作るのが良いですが、簡易的に）
    private AudioSource bgmSource;
    private AudioSource seSource; // GameManagerにあるやつ

    void Start()
    {
        // 初期化
        if (menuPanel != null) menuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // ボタン登録
        if (menuButton != null) menuButton.onClick.AddListener(OpenMenu);
        if (closeButton != null) closeButton.onClick.AddListener(CloseMenu);
        if (retireButton != null) retireButton.onClick.AddListener(OnClickRetire);
        
        // ★修正：ログボタンの設定
        if (logButton != null) 
        {
            logButton.onClick.RemoveAllListeners();
            logButton.onClick.AddListener(() => 
            {
                // メニューを閉じてログを開く
                if (menuPanel != null) menuPanel.SetActive(false);
                if (BattleLogManager.instance != null)
                {
                    BattleLogManager.instance.ToggleLogPanel();
                }
            });
        }

        // 設定ボタンの動作変更（設定パネルを開く）
        if (settingsButton != null) 
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(OpenSettings);
        }

        if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(CloseSettings);

        // 音量スライダーの初期化
        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.AddListener(SetBGMVolume);
            bgmSlider.value = PlayerPrefs.GetFloat("BGM_Volume", 0.5f);
        }
        if (seSlider != null)
        {
            seSlider.onValueChanged.AddListener(SetSEVolume);
            seSlider.value = PlayerPrefs.GetFloat("SE_Volume", 0.5f);
        }

        // ソース取得
        GameObject bgmObj = GameObject.Find("BGM Player");
        if (bgmObj) bgmSource = bgmObj.GetComponent<AudioSource>();
        
        // 初期音量適用
        SetBGMVolume(bgmSlider != null ? bgmSlider.value : 0.5f);
        SetSEVolume(seSlider != null ? seSlider.value : 0.5f);
    }

    public void OpenMenu()
    {
        if (menuPanel != null) menuPanel.SetActive(true);
    }

    public void CloseMenu()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
    }

    public void OnClickRetire()
    {
        // リタイア処理（負け扱い）
        CloseMenu();

        // ★Online Check
        var gameState = FindObjectOfType<GameStateController>();
        if (gameState != null && gameState.Object != null && gameState.Object.IsValid)
        {
             gameState.RPC_Resign(gameState.Runner.LocalPlayer);
             return; 
        }

        if (GameManager.instance != null)
        {
            GameManager.instance.GameEnd(false); // プレイヤー敗北
        }
    }

    public void OpenSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (menuPanel != null) menuPanel.SetActive(false); // メインメニューは隠す
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true); // メインメニューに戻る
    }

    public void SetBGMVolume(float value)
    {
        if (bgmSource != null) bgmSource.volume = value;
        PlayerPrefs.SetFloat("BGM_Volume", value);
    }

    public void SetSEVolume(float value)
    {
        // GameManagerのAudioSource（SE用）を調整
        if (GameManager.instance != null)
        {
            var source = GameManager.instance.GetComponent<AudioSource>();
            if (source != null) source.volume = value;
        }
        PlayerPrefs.SetFloat("SE_Volume", value);
    }
}