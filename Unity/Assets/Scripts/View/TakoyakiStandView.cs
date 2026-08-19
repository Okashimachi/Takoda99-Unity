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

        [Tooltip("単語を打ち切った瞬間のひっくり返し演出に使う。MainStore/HandRoot を割り当てる。")]
        [SerializeField] private HandView hand;

        private TakoyakiSlotView[] slots; // 長さ24。行優先・左上原点

        /// <summary>いま対応中の客の注文個数（サーバー値）。</summary>
        private int orderCount;

        /// <summary>この注文で打ち終えた単語数。提供判定（orderCount との比較）にだけ使う。</summary>
        private int typedWordCount;

        /// <summary>打鍵速度から決まる「生地を流す穴の数」。</summary>
        private int occupiedCount;

        /// <summary>
        /// いま調理に使っている穴の index（0..occupiedCount-1 を巡回）。
        /// **客をまたいでも引き継ぐ**（BeginOrder では戻さない）。「使う穴を毎回変える」という
        /// 見た目の要求のためで、注文の進捗（<c>typedWordCount</c>）とは別物。
        /// </summary>
        private int activeSlotIndex;

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

            ResetSpeedTier();
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
        /// **使う穴の巡回位置（<c>activeSlotIndex</c>）はここでは戻さない**（客をまたいで毎回変える）。
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
            ResetSpeedTier();
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
            ResetSpeedTier();
            tray?.ResetTray();
            ClearAllSlots();
            ApplyAll();
        }

        /// <summary>
        /// 1打ぶんの反映（企画書 5番）。
        /// ミスしても**鉄板の見た目は変わらない**（出来は舟皿だけが表す）。ここでは率の材料として数えるだけ。
        /// 焼き上がり（6番）はここでは起こさない。単語を打ち切った瞬間にだけ切り替える
        /// （<see cref="OnWordCleared"/>）。打っている途中で Raw→Done が動くと、タイプの
        /// タイミングと見た目がずれて見えるため。
        /// </summary>
        /// <param name="isMiss">ミス打鍵なら true。</param>
        public void OnKeyTyped(bool isMiss)
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
        }

        /// <summary>
        /// 1単語を打ち切った。**タイプが完了した瞬間がそのまま Raw→Done の切り替え点**（企画書 6番）。
        /// 同時に、手をその穴まで動かしてひっくり返す演出を出し、続けて次にお題が使う穴（巡回で1つ先）
        /// まで手を運ぶ（お題が変わるたびに手の定位置も動く）。
        /// 玉は鉄板に残したまま、焼く穴を次へ進めるだけ。舟皿へ移すのは注文ぶんを
        /// 打ち終えたとき（<see cref="OnOrderServed"/>）。
        /// </summary>
        public void OnWordCleared()
        {
            var slot = ResolveActiveSlot();
            var completedRect = slot != null ? slot.SlotRect : null;

            if (slot != null)
            {
                slot.Cook();
            }

            AdvanceActiveSlot();
            var nextSlot = ResolveActiveSlot();
            hand?.PlayFlipReaction(completedRect, nextSlot != null ? nextSlot.SlotRect : null);

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

        /// <summary>
        /// いま調理に使っている穴。<c>activeSlotIndex</c>（occupiedCount の範囲を巡回する）で決まる。
        /// 生地を流していない（occupiedCount が0）ときは null。
        /// </summary>
        private TakoyakiSlotView ResolveActiveSlot()
        {
            if (slots == null || occupiedCount <= 0)
            {
                return null;
            }

            var index = activeSlotIndex % occupiedCount;
            return slots[index];
        }

        /// <summary>
        /// 使う穴を1つ先へ進める（企画書の要求：1つめ→2つめ→…→occupiedCount個目→また1つめ）。
        /// **提供待ち（Cooked）の穴には止まらない**——長い注文で生地の数を一巡してしまったとき、
        /// まだ舟皿へ運んでいない完成品を新しい生地で上書きしないため、空くまで探して進む。
        /// </summary>
        private void AdvanceActiveSlot()
        {
            if (occupiedCount <= 0)
            {
                return;
            }

            for (var offset = 1; offset <= occupiedCount; offset++)
            {
                var index = (activeSlotIndex + offset) % occupiedCount;
                if (slots[index] == null || slots[index].State != TakoyakiSlotState.Cooked)
                {
                    activeSlotIndex = index;
                    return;
                }
            }

            // occupiedCount ぶん全部が提供待ちで埋まっている（生地の数より注文が長い等の極端なケース）。
            // それでも先には進めておく。次にできることを ResolveActiveSlot が改めて判断する。
            activeSlotIndex = (activeSlotIndex + 1) % occupiedCount;
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

            // 打鍵が speedWindowKeys ぶん溜まるまでは下限（段階0）に留める。
            // 溜まる前の KPM は数打の間隔だけで決まるため、偶然の連打で異常値が出て
            // 試合開始直後にいきなり穴が全開放される事故が起きる（cooking-anim/01 §4.1）。
            var wanted = speedMeter.HasFullWindow
                ? TypingSpeedTierRule.ResolveTierIndex(settings.SpeedTiers, speedMeter.CalculateKpm(now))
                : 0;

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

        /// <summary>
        /// 速度段階を下限へ戻す。Awake・客の入れ替わりで呼ぶ。
        /// **下限＝<c>speedTiers</c> の先頭（index 0）が示す穴数**（仕様上は8）。
        /// </summary>
        private void ResetSpeedTier()
        {
            speedMeter?.Reset();
            currentTierIndex = 0;
            tierDropSince = -1d;
            occupiedCount = ResolveSlotCountForTier(0);
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
        /// **`Empty` の穴にだけ生地を足し、`Cooked`（提供待ち）の穴には一切触れない。**
        /// 巡回で使う穴が固定の連番ではなくなったため、「現在アクティブな1穴だけ除外する」という
        /// 以前の判定はできない。状態だけを見て判断すれば、動いている途中の穴を誤って
        /// 上書きすることはない（Batter・Cooked はどちらも「Empty ではない」ので素通りする）。
        /// </summary>
        private void ApplyAll()
        {
            if (slots == null || serving)
            {
                return;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    continue;
                }

                if (i < occupiedCount)
                {
                    // 先行して仕込む生地。空の穴にだけ、投入アニメーション付きで落とす
                    // （企画書 5番の見せ場。速度段階が上がって穴が増えた瞬間もここを通る）。
                    if (slots[i].State == TakoyakiSlotState.Empty)
                    {
                        slots[i].PourBatter();
                    }
                }
                else if (slots[i].State != TakoyakiSlotState.Cooked)
                {
                    // 速度段階の外。ただし提供待ちの完成品は消さない。
                    slots[i].SetState(TakoyakiSlotState.Empty);
                }
            }
        }
    }
}
