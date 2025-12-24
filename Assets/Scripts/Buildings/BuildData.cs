using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewBuild", menuName = "Hakoniwa/BuildData")]
public class BuildData : ScriptableObject
{
    [Header("基本情報")]
    public string id;
    public string buildName;
    public Sprite icon;
    [TextArea(2, 4)] public string description;

    [Header("パラメータ")]
    public int cost;
    public int duration; // 持続ターン数

    [Header("能力")]
    // カードと同じ能力システムを流用します
    public List<CardAbility> abilities = new List<CardAbility>(); 
}