using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class UnitMover : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Transform originalParent;
    private CanvasGroup canvasGroup;
    private UnitView unitView;

    public CardData sourceData;

    // ���ύX�F�t���O��2�ɕ����܂���
    public bool canAttack = false; // �U���ł���H
    public bool canMove = false;   // �ړ��ł���H

    // ���ǉ��F�\�̓t���O
    public bool hasTaunt = false;   // ���i�������j
    public bool hasStealth = false; // �����i�I�΂�Ȃ��j

    public bool isPlayerUnit = true;
    public int attackPower;
    public int health;
    public string scriptKey;
    public int maxHealth;

    public bool hasHaste = false; 
    public bool hasQuick = false;

    private bool isAnimating = false; // アニメーション中フラグ
    private Vector3 dragStartPos;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        unitView = GetComponent<UnitView>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    // Initialize���\�b�h���C��
    public void Initialize(CardData data, bool isPlayer)
    {
        attackPower = data.attack;
        health = data.health;

        sourceData = data;

        // ���ǉ��F�f�[�^��ۑ�
        maxHealth = data.health; // ����HP���ő�l�Ƃ��ċL��
        scriptKey = data.scriptKey; // �X�L�������L���i�^�[���I�����p�j

        isPlayerUnit = isPlayer;

        originalParent = transform.parent;

        // scriptKey�����Ĕ\�͂��Z�b�g�I
        switch (data.scriptKey)
        {
            case "PASSIVE_QUICK": // �����i�f���������Ȃǁj
                // ���������𖳌����I
                canAttack = true;
                canMove = true;
                break;

            case "TAUNT_ROW": // ���i���s��q���Ȃǁj
                hasTaunt = true;
                break;

            case "STEALTH": // �����i�e�̈ÎE�҂Ȃǁj
                hasStealth = true;
                // �������ɂ��Ă������o
                GetComponent<CanvasGroup>().alpha = 0.5f;
                break;
        }

        if (!isPlayerUnit)
        {
            // 敵はGameManager(AI)が管理するので、ここではとりあえずtrueにしておく
            // (AIターン開始時にリセットされるため)
            canAttack = false;
            canMove = false;
            canvasGroup.blocksRaycasts = true;
        }
        else
        {
            // プレイヤーユニット
            // ★変更：速攻(Haste)なら最初から動ける。それ以外は召喚酔い(false)
            if (hasHaste)
            {
                canAttack = true;
                canMove = true;
                GetComponent<UnityEngine.UI.Image>().color = Color.white;
            }
            else
            {
                canAttack = false;
                canMove = false;
                GetComponent<UnityEngine.UI.Image>().color = Color.gray;
            }
        }
        // ★修正：アビリティリストを見てパッシブを設定
        foreach(var ability in data.abilities)
        {
            if (ability.trigger == EffectTrigger.PASSIVE)
            {
                if (ability.effect == EffectType.TAUNT) hasTaunt = true;
                if (ability.effect == EffectType.STEALTH) { hasStealth = true; GetComponent<CanvasGroup>().alpha = 0.5f; }
                if (ability.effect == EffectType.QUICK) hasQuick = true; // 疾風
                if (ability.effect == EffectType.HASTE) hasHaste = true; // 速攻
            }
        }
        
        // 移動不可のデフォルト設定（さっきの修正）
        if (isPlayer && !canAttack && !canMove) 
        {
             GetComponent<UnityEngine.UI.Image>().color = Color.gray;
        }
        // ★追加：見た目の更新
        if (unitView != null)
        {
            unitView.RefreshStatusIcons(hasTaunt, hasStealth);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 何かをドラッグ中なら表示しない（pointerDragがnullでない＝ドラッグ中）
        if (eventData.pointerDrag != null) return;

        if (sourceData != null)
        {
            GameManager.instance.ShowUnitDetail(sourceData);
        }
    }

    // ★追加：マウスが出た時
    public void OnPointerExit(PointerEventData eventData)
    {
        GameManager.instance.OnClickCloseDetail();
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isAnimating) return;
        GameManager.instance.OnClickCloseDetail();
        if (!isPlayerUnit) return;
        if (!canAttack && !canMove) return;

        originalParent = transform.parent;
        
        // ★修正：親を変える処理（SetParent）を削除しました
        // transform.SetParent(transform.root); 
        
        // ドロップ検知のためにレイキャストは無効化する（これは必須）
        canvasGroup.blocksRaycasts = false;

        // 始点を今の位置に設定
        dragStartPos = transform.position;

        // 矢印表示（始点は自分の中心）
        GameManager.instance.ShowArrow(dragStartPos);
        GameManager.instance.SetArrowColor(Color.gray);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isPlayerUnit) return;
        if (!canAttack && !canMove) return;

        // ★修正：位置を更新する処理を削除しました
        // transform.position = eventData.position; 

        // 矢印の更新（始点は固定、終点はマウス位置）
        GameManager.instance.UpdateArrow(dragStartPos, eventData.position);

        // 色判定ロジック（そのまま）
        UpdateArrowColor(eventData);
    }
    // UnitMover.cs の UpdateArrowColor メソッドのみ書き換え

    void UpdateArrowColor(PointerEventData eventData)
    {
        GameObject hoverObj = eventData.pointerCurrentRaycast.gameObject;
        Color targetColor = Color.gray;
        string labelText = "";
        bool showLabel = false;

        if (hoverObj != null)
        {
            // ★修正：GetComponentInParent を使うことで、子要素(Textなど)に乗っても親を検知できる
            UnitMover targetUnit = hoverObj.GetComponentInParent<UnitMover>();
            Leader targetLeader = hoverObj.GetComponentInParent<Leader>();
            
            // --- パターンA：攻撃対象（赤色） ---
            if (canAttack)
            {
                if (targetUnit != null && !targetUnit.isPlayerUnit)
                {
                    if (GameManager.instance.CanAttackUnit(this, targetUnit))
                    {
                        targetColor = Color.red;
                        labelText = "攻撃"; // ★攻撃ラベル
                        showLabel = true;
                    }
                }
                // EnemyInfoという名前判定ではなく、Leaderコンポーネントが敵かどうかで判定推奨
                // （簡易的に名前判定を残すなら targetLeader.gameObject.name をチェック）
                else if (targetLeader != null) 
                {
                     // 親オブジェクトの名前を確認するか、もしくは「自分じゃないリーダー」なら敵とみなす
                     if (targetLeader.transform.parent.name == "EnemyBoard" || targetLeader.name == "EnemyInfo")
                     {
                         if (GameManager.instance.CanAttackLeader(this))
                         {
                             targetColor = Color.red;
                             labelText = "攻撃"; // ★攻撃ラベル
                             showLabel = true;
                         }
                     }
                }
            }

            // --- パターンB：移動場所（黄色） ---
            // ★修正：ここも InParent
            DropPlace slot = hoverObj.GetComponentInParent<DropPlace>();
            
            if (canMove && slot != null && !slot.isEnemySlot)
            {
                if (slot.transform.childCount == 0)
                {
                    SlotInfo mySlot = originalParent.GetComponent<SlotInfo>();
                    SlotInfo targetSlot = slot.GetComponent<SlotInfo>();
                    
                    if (mySlot != null && targetSlot != null)
                    {
                        int dist = Mathf.Abs(mySlot.x - targetSlot.x) + Mathf.Abs(mySlot.y - targetSlot.y);
                        if (dist == 1)
                        {
                            targetColor = Color.yellow;
                            labelText = "移動"; // ★移動ラベル
                            showLabel = true;
                        }
                    }
                }
            }
        }

        GameManager.instance.SetArrowColor(targetColor);
        GameManager.instance.SetArrowLabel(labelText, showLabel);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 矢印を隠す
        GameManager.instance.HideArrow();

        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

        // ★修正：親を戻す処理も不要になったので削除（またはコメントアウト）
        /*
        if (originalParent != null)
        {
            transform.SetParent(originalParent);
            transform.localPosition = Vector3.zero;
        }
        */
        
        // もし位置が微妙にズレていた時のために、念のため座標リセットだけ入れても良いです
        transform.localPosition = Vector3.zero;
    }

    public void Attack(Leader target, bool force = false)
    {
        if (!canAttack && !force) return;
        
        // アニメーション開始！
        StartCoroutine(TackleAnimation(target.transform, () => 
        {
            // ヒットした瞬間に実行する処理
            GameManager.instance.PlaySE(GameManager.instance.seAttack);
            target.TakeDamage(attackPower);
            
            ConsumeAttack();
        }));
    }

    // UnitMover.cs �� AttackUnit ���\�b�h

    public void AttackUnit(UnitMover enemy)
    {
        if (!canAttack) return;

        // アニメーション開始！
        StartCoroutine(TackleAnimation(enemy.transform, () => 
        {
            // ヒットした瞬間に実行する処理（ダメージ計算）
            int finalDamage = this.attackPower;
            int enemyDamage = enemy.attackPower;

            // 正面のボーナス計算
            SlotInfo mySlot = null;
            if (originalParent != null) mySlot = originalParent.GetComponent<SlotInfo>();

            SlotInfo enemySlot = null;
            if (enemy.transform.parent != null) enemySlot = enemy.transform.parent.GetComponent<SlotInfo>();

            if (mySlot != null && enemySlot != null)
            {
                if (mySlot.y == enemySlot.y)
                {
                    finalDamage += 1;
                    enemyDamage += 1;
                    Debug.Log("正面衝突ボーナス！ +1ダメージ");
                }
            }

            // ダメージ適用
            enemy.TakeDamage(finalDamage);
            this.TakeDamage(enemyDamage);
            
            ConsumeAttack();
        }));
    }

    public void TakeDamage(int damage)
    {
        GameManager.instance.SpawnDamageText(transform.position, damage);
        health -= damage;
        if (unitView != null) unitView.healthText.text = health.ToString();
        if (health <= 0) Destroy(gameObject);
        if (damage > 0) GameManager.instance.PlaySE(GameManager.instance.seDamage);
    }

    // ★追加：突撃アニメーション
    private System.Collections.IEnumerator TackleAnimation(Transform target, System.Action onHitLogic)
    {
        isAnimating = true;
        transform.SetParent(transform.root);
        // 1. 準備
        Vector3 startPos = transform.position;
        Vector3 targetPos = target.position;
        
        // 敵の手前まで移動する（完全に重なると見栄えが悪いので少し手前）
        // ※簡易的にターゲットの位置そのままでもOKですが、微調整するとより良いです
        
        // 2. 行き（突撃！）
        float duration = 0.15f; // 0.15秒で突っ込む
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            // Lerpで滑らかに移動
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null; // 1フレーム待つ
        }
        transform.position = targetPos; // 念のため位置合わせ

        // 3. ヒット！（ダメージ処理などの実行）
        onHitLogic?.Invoke();

        // （ここで画面揺らしなどを入れるとさらにGood）
        yield return new WaitForSeconds(0.05f); // ほんの一瞬止める（ヒットストップ感）

        // 4. 帰り（元の場所へ）
        elapsed = 0f;
        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(targetPos, startPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 5. 終了処理
        if (originalParent != null)
        {
            transform.SetParent(originalParent);
            transform.localPosition = Vector3.zero;
        }
        
        // 行動権消費（色をグレーにするなど）
        ConsumeAction();
        
        isAnimating = false;
    }

    public void ConsumeAction() 
    {
        // 強制的に全終了させる場合（建設など）
        canMove = false;
        canAttack = false;
        UpdateColor();
    }

    public void ConsumeMove()
    {
        canMove = false;

        // ★疾風(Quick)を持っていないなら、攻撃権も失う
        if (!hasQuick)
        {
            canAttack = false;
        }

        UpdateColor();
    }

    // 攻撃が終わったときに呼ぶ
    public void ConsumeAttack()
    {
        canAttack = false;

        // ★疾風(Quick)を持っていないなら、移動権も失う
        if (!hasQuick)
        {
            canMove = false;
        }

        UpdateColor();
    }

    void UpdateColor()
    {
        // どちらもできないならグレー、どちらかできるなら白
        if (!canMove && !canAttack)
        {
            GetComponent<UnityEngine.UI.Image>().color = Color.gray;
        }
        else
        {
            GetComponent<UnityEngine.UI.Image>().color = Color.white;
        }
    }

// UnitMover.cs の OnDrop 内

    public void OnDrop(PointerEventData eventData)
    {
        // パターンA：ユニット同士のバトル（既存）
        UnitMover attacker = eventData.pointerDrag.GetComponent<UnitMover>();
        if (attacker != null && attacker.canAttack)
        {
            if (this.isPlayerUnit != attacker.isPlayerUnit)
            {
                if (GameManager.instance.CanAttackUnit(attacker, this)) attacker.AttackUnit(this);
            }
            return; // 処理終了
        }
    }
    // ���ǉ��F�񕜏����p�i�ő�l�𒴂��Ȃ��悤�Ɂj
    public void Heal(int amount)
    {
        health += amount;
        if (health > maxHealth) health = maxHealth;
        if (unitView != null) unitView.healthText.text = health.ToString();
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        // �h���b�O���łȂ���Ώڍׂ�\��
        if (!eventData.dragging)
        {
            GameManager.instance.ShowUnitDetail(sourceData);
        }
    }

    public void MoveToSlot(Transform targetSlot)
    {
        StartCoroutine(MoveAnimation(targetSlot));
    }

    public void PlaySummonAnimation()
    {
        StartCoroutine(SummonAnimationCoroutine());
    }
    private System.Collections.IEnumerator SummonAnimationCoroutine()
    {
        isAnimating = true;

        Vector3 originalScale = transform.localScale;
        Vector3 landPos = transform.localPosition;
        Vector3 startPos = landPos + new Vector3(0, 50f, 0); // 50ピクセル上から

        // 初期状態セット
        transform.localPosition = startPos;
        transform.localScale = originalScale * 1.2f; // ちょっと大きく
        GetComponent<CanvasGroup>().alpha = 0f;      // 最初は透明

        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            // イージング（勢いよく着地）
            t = t * t * (3f - 2f * t); 

            transform.localPosition = Vector3.Lerp(startPos, landPos, t);
            transform.localScale = Vector3.Lerp(originalScale * 1.2f, originalScale, t);
            GetComponent<CanvasGroup>().alpha = Mathf.Lerp(0f, 1f, t * 2); // 早めに不透明に

            elapsed += Time.deltaTime;
            yield return null;
        }

        // ズレ補正
        transform.localPosition = landPos;
        transform.localScale = originalScale;
        GetComponent<CanvasGroup>().alpha = 1f;

        isAnimating = false;
    }
    // ★追加：移動アニメーションコルーチン
    private System.Collections.IEnumerator MoveAnimation(Transform targetSlot)
    {
        isAnimating = true;
        transform.SetParent(transform.root); 
        
        Vector3 startPos = transform.position;
        Vector3 endPos = targetSlot.position;
        float duration = 0.2f; 
        float elapsed = 0f;

        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = endPos;

        transform.SetParent(targetSlot);
        transform.localPosition = Vector3.zero;
        originalParent = targetSlot; 

        // ★削除：砂煙エフェクト
        // GameManager.instance.PlayDustEffect(transform.position); 
        
        ConsumeMove();
        isAnimating = false;
    }
}