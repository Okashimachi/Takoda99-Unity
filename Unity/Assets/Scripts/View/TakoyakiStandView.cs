// 仕様書: Unity/docs/.sdd/match-view/03-takoyaki-stand-view.md
//         Unity/docs/.sdd/cooking-anim/01-cooking-animation.md
// たこ焼き台全体（6列×4行＝24穴）。root/MainStoreCanvas/Main/MainStore/Takoyakis にアタッチする。
//
// slots は Inspector で手動配線しない。Takoyakis 直下の4つの行オブジェクト
// （各6個の TakoyakiSlotView）を実行時に自動収集する。
//
// 打鍵の正誤判定はしない（Renderer 経由で TypingJudge の結果を受け取るだけ）。
// 評価・信用・順位に関わる値は一切算出しない。使う穴数と盛り付けの出来は**見た目の段階**であり、
// 送信もサーバーへの反映も行わない（rules/01 原則3）。

using System.Collections;
using System.Collections.Generic;
using Takoda99.View.Cooking;
using Takoda99.View.ValueObjects;
using UnityEngine;

namespace Takoda99.View
{
    /// <summary>たこ焼き台全体（6列×4行＝24穴）。root/.../MainStore/Takoyakis にアタッチする。</summary>
    public sealed class TakoyakiStandView : MonoBehaviour
    {
        [SerializeField] private CookingAnimationSettings settings;

        [Tooltip("完成した玉を舟皿へ飛ばす演出。MainStore/FlyLayer を割り当てる。")]
        [SerializeField] private FlyingTakoyakiAnimator flyingAnimator;

        [Tooltip("玉の着地先。MainStore/TrayRoot を割り当てる。")]
        [SerializeField] private TrayView tray;

        private TakoyakiSlotView[] slots; // 長さ24。行優先・左上原点

        /// <summary>いま対応中の客の注文個数（サーバー値）。</summary>
        private int orderCount;

        /// <summary>この注文で打ち終えた単語数。＝いま焼いている穴の index。</summary>
        private int typedWordCount;

        /// <summary>打鍵速度から決まる「生地を流す穴の数」。</summary>
        private int occupiedCount;

        /// <summary>この注文ぶんの正打数・ミス数。盛り付けの出来（ミス率）を決める材料。</summary>
        private int orderCorrectCount;
        private int orderMissCount;

        private TypingSpeedMeter speedMeter;

        /// <summary>盛り付け中か。完了するまで台と皿を作り替えない。</summary>
        private bool serving;

        /// <summary>盛り付け・提供の完了を待っている仕切り直し。</summary>
        private Coroutine pendingBegin;

        /// <summary>いま採用している速度段階。上げは即時、下げは猶予つき。</summary>
        private int currentTierIndex;

        /// <summary>低い段階が続き始めた時刻（秒）。負値なら「下げ待ち中でない」。</summary>
        private double tierDropSince = -1d;

        private void Awake()
        {
            CollectSlots();

            if (slots.Length != TakoyakiStandState.StandCapacity)
            {
                Debug.LogError(
                    $"{nameof(TakoyakiStandView)}: 子階層から集めた穴の数が {TakoyakiStandState.StandCapacity} ではありません（{slots.Length}個）。"
                    + " Takoyakis 直下に4つの行オブジェクト（各6個の TakoyakiSlotView）があるか確認してください。",
                    this);
            }

            if (settings == null)
            {
                Debug.LogError($"{nameof(TakoyakiStandView)}.{nameof(settings)} が未割り当てです。調理アニメーションは動きません。", this);
            }
            else
            {
                speedMeter = new TypingSpeedMeter(settings.SpeedWindowKeys);
            }

            foreach (var slot in slots)
            {
                slot.Bind(settings);
            }

            occupiedCount = ResolveSlotCountForTier(0);
        }

        /// <summary>
        /// Takoyakis の直接の子（行オブジェクト）を上から順に走査し、
        /// 各行の子に付いた TakoyakiSlotView を左から順に集める。行数・列数は問わない。
        /// </summary>
        private void CollectSlots()
        {
            var collected = new List<TakoyakiSlotView>(TakoyakiStandState.StandCapacity);

            foreach (Transform line in transform)
            {
                foreach (Transform slot in line)
                {
                    var view = slot.GetComponent<TakoyakiSlotView>();
                    if (view != null)
                    {
                        collected.Add(view);
                    }
                }
            }

            slots = collected.ToArray();
        }

        private void OnEnable()
        {
            ApplyAll();
        }

        private void Update()
        {
            // 打鍵が途切れても KPM は落ちる（TypingSpeedMeter が無音を分母に入れる）。
            // 段階の見直しは毎フレーム行うが、実際に穴を作り替えるのは値が変わったときだけ。
            UpdateSpeedTier();
        }

        // ── 上位から呼ぶ入口 ────────────────────────────────────────

        /// <summary>いま対応中の客の注文個数（＝この客に出すたこ焼きの個数）。</summary>
        public void SetOrderCount(int newOrderCount)
        {
            orderCount = newOrderCount < 0 ? 0 : newOrderCount;
        }

        /// <summary>いま対応中の客のノルマのうち、入力を終えた語数。</summary>
        public void SetTypedWordCount(int newTypedWordCount)
        {
            typedWordCount = newTypedWordCount < 0 ? 0 : newTypedWordCount;
        }

        /// <summary>
        /// 客が入れ替わった。台と皿を仕切り直す。
        /// 中断された注文のミスを次の客へ持ち越さない（打っていないミスで盛り付けを汚くしない）。
        /// </summary>
        public void BeginOrder(int newOrderCount)
        {
            // 盛り付け・提供演出の途中なら、それが終わるまで台と皿に触らない。
            // ここで即座に作り替えると、打ち終えたばかりの8個が皿に載らないまま消える。
            if (IsSettling())
            {
                if (pendingBegin != null)
                {
                    StopCoroutine(pendingBegin);
                }

                pendingBegin = StartCoroutine(BeginOrderWhenSettled(newOrderCount));
                return;
            }

            BeginOrderNow(newOrderCount);
        }

        private IEnumerator BeginOrderWhenSettled(int newOrderCount)
        {
            while (IsSettling())
            {
                yield return null;
            }

            pendingBegin = null;
            BeginOrderNow(newOrderCount);
        }

        private bool IsSettling() => serving || (tray != null && tray.IsServing);

        private void BeginOrderNow(int newOrderCount)
        {
            SetOrderCount(newOrderCount);
            ResetOrderCounters();
            speedMeter?.Reset();
            tray?.ResetTray();
            ClearAllSlots();
            ApplyAll();
        }

        /// <summary>対応中の客がいなくなった。台と皿を空にする。</summary>
        public void ClearOrder()
        {
            if (pendingBegin != null)
            {
                StopCoroutine(pendingBegin);
                pendingBegin = null;
            }

            serving = false;
            orderCount = 0;
            ResetOrderCounters();
            speedMeter?.Reset();
            tray?.ResetTray();
            ClearAllSlots();
            ApplyAll();
        }

        /// <summary>
        /// 1打ぶんの反映（企画書 5, 6番）。
        /// ミスしても**鉄板の見た目は変わらない**（出来は舟皿だけが表す）。ここでは率の材料として数えるだけ。
        /// </summary>
        /// <param name="isMiss">ミス打鍵なら true。</param>
        /// <param name="wordProgress">いま打っている単語の進捗（0..1）。ローマ字の打鍵数で測った値。</param>
        public void OnKeyTyped(bool isMiss, float wordProgress)
        {
            speedMeter?.Record(Time.unscaledTimeAsDouble);

            if (isMiss)
            {
                orderMissCount++;
            }
            else
            {
                orderCorrectCount++;
            }

            var slot = ResolveActiveSlot();
            if (slot == null)
            {
                return;
            }

            // 新しい単語の1文字目。まだ生地が無ければここで落とす（企画書 5番）。
            if (slot.State == TakoyakiSlotState.Empty)
            {
                slot.PourBatter();
            }

            // 単語の規定割合まで打てたら焼き上がりへ（企画書 6番）。
            if (settings != null && wordProgress >= settings.CookedProgressRatio)
            {
                slot.Cook();
            }
        }

        /// <summary>
        /// 1単語を打ち切った。**玉は鉄板に残したまま**、焼く穴を次へ進めるだけ。
        /// 舟皿へ移すのは注文ぶんを打ち終えたとき（<see cref="OnOrderServed"/>）。
        /// </summary>
        public void OnWordCleared()
        {
            var slot = ResolveActiveSlot();
            if (slot != null)
            {
                // 8割に届く前に打ち切った短い単語のため、ここで確実に done へ到達させる。
                slot.Cook();
            }

            typedWordCount++;

            // 注文ぶん打ち終えた。ここが一斉盛り付けの発火点。
            // Renderer も OnOrderServed を呼ぶが、serving フラグで二重発火しない。
            if (orderCount > 0 && typedWordCount >= orderCount)
            {
                OnOrderServed();
                return;
            }

            ApplyAll();
        }

        /// <summary>
        /// 注文ぶんを打ち終えた（企画書 8番の読み替え・9番）。
        /// 鉄板の完成品を一斉に舟皿へ盛り、打鍵ミス率で決めた出来で提供する。
        /// </summary>
        public void OnOrderServed()
        {
            if (serving)
            {
                return;
            }

            var quality = ResolveOrderQuality();

            if (flyingAnimator == null || tray == null)
            {
                ClearAllSlots();
                tray?.Serve(quality);
                ResetOrderCounters();
                return;
            }

            serving = true;
            StartCoroutine(ServeRoutine(quality));
        }

        // ── 内部 ────────────────────────────────────────────────────

        /// <summary>完成した玉を1個ずつ少しずらして飛ばし、全部着地したら提供へ進む。</summary>
        private IEnumerator ServeRoutine(TakoyakiQuality quality)
        {
            var landing = tray.ResolveLandingRect();
            var stagger = CookingAnimationSettings.ToSeconds(settings != null ? settings.FlyStaggerMs : 0);
            var flying = 0;

            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null || slot.State != TakoyakiSlotState.Cooked)
                {
                    continue;
                }

                var sprite = slot.CookedSprite;
                var from = slot.CookedRect;
                slot.TakeCooked();

                flying++;
                flyingAnimator.Fly(sprite, from, landing, () => flying--);

                if (stagger > 0f)
                {
                    yield return new WaitForSeconds(stagger);
                }
            }

            while (flying > 0)
            {
                yield return null;
            }

            // 全部が皿に乗った。ここで盛り付けの出来が確定する。
            tray.Serve(quality);

            ResetOrderCounters();
            serving = false;
            ApplyAll();
        }

        private void ResetOrderCounters()
        {
            typedWordCount = 0;
            orderCorrectCount = 0;
            orderMissCount = 0;
        }

        /// <summary>いま焼いている穴。生地を流している範囲を外れていたら null。</summary>
        private TakoyakiSlotView ResolveActiveSlot()
        {
            if (slots == null || typedWordCount < 0 || typedWordCount >= slots.Length)
            {
                return null;
            }

            return typedWordCount < occupiedCount ? slots[typedWordCount] : null;
        }

        /// <summary>この注文の打鍵ミス率から盛り付けの出来を決める（仕様書 §4.3）。</summary>
        private TakoyakiQuality ResolveOrderQuality()
        {
            if (settings == null)
            {
                return TakoyakiQuality.Clean;
            }

            return TakoyakiQualityRule.From(
                orderCorrectCount,
                orderMissCount,
                settings.TrayCleanMaxMissRatio,
                settings.TrayNormalMaxMissRatio);
        }

        /// <summary>
        /// 打鍵速度から使う穴数を決める（仕様書 §4.1）。
        /// 段階が上がるのは即時、下がるのは <c>speedTierDropHoldMs</c> 継続してから
        /// （打鍵の揺らぎで台がちらつかない）。
        /// </summary>
        private void UpdateSpeedTier()
        {
            if (settings == null || speedMeter == null)
            {
                return;
            }

            var now = Time.unscaledTimeAsDouble;
            var kpm = speedMeter.CalculateKpm(now);
            var wanted = TypingSpeedTierRule.ResolveTierIndex(settings.SpeedTiers, kpm);

            if (wanted > currentTierIndex)
            {
                currentTierIndex = wanted;
                tierDropSince = -1d;
            }
            else if (wanted < currentTierIndex)
            {
                if (tierDropSince < 0d)
                {
                    tierDropSince = now;
                }
                else if (now - tierDropSince >= CookingAnimationSettings.ToSeconds(settings.SpeedTierDropHoldMs))
                {
                    currentTierIndex = wanted;
                    tierDropSince = -1d;
                }
            }
            else
            {
                tierDropSince = -1d;
            }

            var newOccupied = ResolveSlotCountForTier(currentTierIndex);
            if (newOccupied == occupiedCount)
            {
                return;
            }

            occupiedCount = newOccupied;
            ApplyAll();
        }

        private int ResolveSlotCountForTier(int tierIndex)
        {
            if (settings == null)
            {
                return TakoyakiStandState.StandCapacity;
            }

            return TypingSpeedTierRule.ResolveSlotCount(
                settings.SpeedTiers,
                tierIndex,
                TakoyakiStandState.StandCapacity);
        }

        private void ClearAllSlots()
        {
            if (slots == null)
            {
                return;
            }

            foreach (var slot in slots)
            {
                if (slot != null)
                {
                    slot.SetState(TakoyakiSlotState.Empty);
                }
            }
        }

        /// <summary>
        /// 24穴すべてを見直す（差分更新はしない。24個なのでコストは無視できる）。
        /// **いま焼いている穴（<c>typedWordCount</c>）と、打ち終えて盛り付け待ちの穴は触らない。**
        /// ここを上書きすると、進行中のアニメーションが打鍵のたびに巻き戻り、
        /// 焼き上がった玉が提供前に消える。
        /// </summary>
        private void ApplyAll()
        {
            if (slots == null || serving)
            {
                return;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                // i < typedWordCount は打ち終えた玉（提供まで鉄板に残す）。
                // i == typedWordCount はいま焼いている穴。どちらも触らない。
                if (slots[i] == null || i <= typedWordCount)
                {
                    continue;
                }

                if (i < occupiedCount)
                {
                    // 先行して仕込む生地。空の穴に増えたぶんだけ、投入アニメーション付きで落とす
                    // （企画書 5番の見せ場。速度段階が上がって穴が増えた瞬間もここを通る）。
                    if (slots[i].State == TakoyakiSlotState.Empty)
                    {
                        slots[i].PourBatter();
                    }
                }
                else
                {
                    // 速度段階の外。使わない穴。
                    slots[i].SetState(TakoyakiSlotState.Empty);
                }
            }
        }
    }
}
