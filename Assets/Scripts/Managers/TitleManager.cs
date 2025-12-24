using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    // スタートボタンが押されたら呼ばれる
    public void OnClickStart()
    {
        // ★変更：いきなりバトルではなく、メニュー画面へ移動
        SceneManager.LoadScene("MenuScene");
    }

    // デッキ編集ボタン（もしタイトル画面に残すならそのまま、メニューに移動するなら削除してもOK）
    public void OnClickDeckEdit()
    {
        SceneManager.LoadScene("DeckEditScene");
    }
}