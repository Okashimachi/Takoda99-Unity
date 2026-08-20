// 仕様書: Unity/docs/.sdd/ranking-view/02-cull-countdown-panel.md
// 足切り秒読みパネル（常設UI）。予選の「予告時だけ出るポップアップ」を格上げしたもの。
//
// 残り時間を ClientState へ書き戻さない（毎フレームの Store 通知を作らない）。
// Rank と CutLineRank を比較して自分が危険かを判定しない（SelfAtRisk がサーバーから届く）。

using System.Collections.Generic;
using TMPro;
using Takoda99.Client.State;
using Takoda99.View.ValueObjects;
using UnityEngine;
using UnityEngine.UI;

namespace Takoda99.View.Ranking
{
    /// <summary>次の足切りまでの秒読みと、脱落予定の店を出す常設パネル。</summary>
    public sealed class CullCountdownPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text remainingText;
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text cutLineText;

        [Header("脱落予定の店")]
        [SerializeField] private RankingRowView rowPrefab;   // 01 と同じ行Prefabを再利用

        /// <summary>
        /// 行を生成する親。**このパネル専用の空の親を割り当てること**（02 §4.1）。
        /// <para>
        /// ★他のパネルの `RowsRoot` を指すと、そちらのパネルの中に行が湧いて出る。
        /// 未割り当て（null）なら行リストを出さない＝脱落確定は下位パネルの帯（`Doomed`）だけで表す。
        /// </para>
        /// </summary>
        [SerializeField] private RectTransform rowsRoot;

        [SerializeField] private int maxCutRows = 5;

        /// <summary>行を縦に積む間隔(px)。行Prefabの高さと揃える。</summary>
        [SerializeField] private float cutRowHeight = 29f;

        /// <summary>「ぎりぎり圏外」の判定に使う下位の件数。下位パネルの visibleCount と揃える。</summary>
        [SerializeField] private int bottomRangeCount = 30;

        /// <summary>maxCutRows に収まりきらない件数を添える欄。</summary>
        [SerializeField] private TMP_Text overflowText;

        [Header("淘汰アラート（02 §5）")]
        [SerializeField] private CanvasGroup alertOverlay;

        /// <summary>alertOverlay の Image。中央を丸く空けたビネットを流し込む先。</summary>
        [SerializeField] private Image alertOverlayImage;

        /// <summary>残り5秒の中央カウントダウン（02 §6）。同じ時計をここから押し込む。</summary>
        [SerializeField] private CullFinalCountdownView finalCountdown;

        [Header("アラートの見た目")]
        [Tooltip("ぎりぎり圏外の色（淡い黄〜橙）。")]
        [SerializeField] private Color cautionColor = new Color(1f, 0.72f, 0.28f);

        [Tooltip("淘汰圏内の色（赤）。")]
        [SerializeField] private Color dangerColor = new Color(0.95f, 0.15f, 0.1f);

        [Tooltip("ぎりぎり圏外の最大不透明度。軽く出すだけに留める。")]
        [SerializeField, Range(0f, 1f)] private float cautionMaxAlpha = 0.3f;

        [Tooltip("淘汰圏内の最大不透明度。")]
        [SerializeField, Range(0f, 1f)] private float dangerMaxAlpha = 0.75f;

        /// <summary>
        /// 明滅の周期（Hz）。**ゆっくりフェードイン・フェードアウトさせるための値であり、点滅させるものではない。**
        /// <see cref="MaxPulseHz"/> で必ず頭を押さえる。
        /// </summary>
        [SerializeField] private float pulseHz = 0.33f;

        [Tooltip("中央の素通し円の半径（画面の短辺の半分＝1.0）。ここは一切色を乗せない。")]
        [SerializeField, Range(0f, 1.5f)] private float clearRadius = 0.62f;

        [Tooltip("色が最大になる半径。clearRadius からここへ向けてなめらかに濃くなる。")]
        [SerializeField, Range(0f, 2f)] private float edgeRadius = 1.15f;

        /// <summary>
        /// 明滅速度の絶対上限（Hz）。**光過敏性発作（いわゆるポリゴンショック）を避けるための安全上限であり、
        /// 演出の都合で緩めてはいけない。** 一般に 3Hz 以上の点滅が危険とされるため、桁で余裕を取っている。
        /// Inspector で pulseHz に大きな値を入れてもここで頭打ちになる。
        /// </summary>
        private const float MaxPulseHz = 0.8f;

        /// <summary>段階が変わったときに色・濃さが飛ばないよう補間する速さ（1秒あたり）。</summary>
        private const float AlphaLerpPerSecond = 2.5f;

        /// <summary>
        /// 第4段階（20秒等間隔スケジュールの4番目、35→20人）の個別調整。
        /// この段階だけ、淘汰対象外の「ぎりぎり圏外」警告を13〜20位に絞る（企画指示）。
        /// </summary>
        private const int FourthStageIndex = 4;
        private const int FourthStageCautionRankMin = 13;
        private const int FourthStageCautionRankMax = 20;

        private CullWarning warning;
        private IReadOnlyDictionary<string, string> displayNames;
        private CullCountdownState current;
        private bool hasCurrent;

        /// <summary>アラートの判定に要る自店の状態。SetWarning で state から拾っておく。</summary>
        private bool selfAlive;
        private bool selfInBottomRange;

        /// <summary>実際に画面へ出している不透明度・色。目標値へ毎フレーム寄せる（段階の切り替わりで飛ばさない）。</summary>
        private float shownAlpha;
        private Color shownColor;
        private Sprite vignetteSprite;
        private float vignetteAspect;

        private RankingRowPool pool;
        private readonly HashSet<string> visibleIds = new HashSet<string>();
        private readonly List<string> cutRows = new List<string>();

        private void Awake()
        {
            pool = new RankingRowPool(rowPrefab, rowsRoot);
            WarnIfRowsRootShared();
            SetPanelVisible(false);

            shownColor = cautionColor;
            shownAlpha = 0f;
            if (alertOverlay != null)
            {
                alertOverlay.alpha = 0f;
            }
        }

        /// <summary>
        /// ★一度これで事故った。`rowsRoot` に**下位パネルの `RowsRoot`** が割り当てられていて、
        /// 脱落予定の行が下位パネルの中央に湧いて出た（行Prefabの authored 位置のまま重なるので、
        /// 5行が1行に見える）。専用の親は編集時には空であるはずなので、子がいたら疑う。
        /// </summary>
        private void WarnIfRowsRootShared()
        {
            if (rowsRoot != null && rowsRoot.childCount > 0)
            {
                Debug.LogWarning(
                    $"{nameof(CullCountdownPanelView)}: rowsRoot（{rowsRoot.name}）に既に子が {rowsRoot.childCount} 件あります。" +
                    "他のパネルの RowsRoot を共有していないか確認してください（脱落予定の行がそちらへ湧きます）。",
                    this);
            }
        }

        private void OnDestroy()
        {
            // 実行時に作ったテクスチャは自動で回収されない。シーンを出るときに捨てる。
            if (vignetteSprite != null)
            {
                var texture = vignetteSprite.texture;
                Destroy(vignetteSprite);
                if (texture != null)
                {
                    Destroy(texture);
                }

                vignetteSprite = null;
            }
        }

        /// <summary>受信値の差し替え。Renderer が state 変化のたびに呼ぶ。</summary>
        public void SetWarning(CullWarning next, ClientState state)
        {
            warning = next;
            displayNames = state?.DisplayNames;

            // アラートの判定材料を state から拾う。順位と CutLineRank は比較しない（02 §1）。
            // 「ぎりぎり圏外」は下位パネルの表示範囲に自店が入っているかで決める（value-objects/12 §4.2 の AtRisk と同じ根拠）。
            selfAlive = state != null
                && state.Alive
                && state.Phase != ClientPhase.Spectating
                && state.Phase != ClientPhase.Result
                && !state.MatchEnded;

            // 第4段階（35→20人）だけは、淘汰対象ではない人への「ぎりぎり圏外」警告を
            // 13〜20位に絞る（企画の個別調整。ranking-view/README §足切りスケジュール参照）。
            selfInBottomRange = next != null && next.StageIndex == FourthStageIndex
                ? state != null && state.Rank >= FourthStageCautionRankMin && state.Rank <= FourthStageCautionRankMax
                : state != null
                    && RankingRowsBuilder.IsInBottomRange(
                        state.Ranking, state.SelfStoreId, state.AliveCount, bottomRangeCount);

            // C5: 未受信の間はパネルを非表示にする（0秒と区別する）。
            if (warning == null)
            {
                SetPanelVisible(false);
                hasCurrent = false;
                pool?.ReleaseAll();
                return;
            }

            SetPanelVisible(true);
            ApplyCutRows();

            // C3: 新しい予告は即座に上書きする。補間中の値を優先しない（サーバー値が常に正）。
            hasCurrent = false;
            UpdateTexts();
        }

        /// <summary>
        /// 受信の瞬間だけ必要な演出の契機（IRenderer.OnCullWarning から）。
        ///
        /// 淘汰圏に入った瞬間のSEはここでは鳴らさない。**順位帯のSEは <see cref="SelfRankView"/> に一本化した**
        /// （上位入り・淘汰圏入り・ぎりぎり圏外入りを1箇所で判定しないと、同じ状況で二重に鳴る）。
        /// 秒読み1秒ごとのSEは <see cref="CullFinalCountdownView"/> の担当。
        /// </summary>
        public void OnWarningReceived(CullWarning received)
        {
            _ = received;
        }

        private void Update()
        {
            // アラートは warning が消えたフレームにも必ず通す（消し忘れると赤いまま画面に残る）。
            UpdateAlert();

            if (warning == null)
            {
                return;
            }

            // C1: Update() で数字だけ更新する。ClientState を触らない。
            UpdateTexts();
        }

        /// <summary>
        /// 画面端アラート（02 §5）。中央を丸く空けたビネットを、段階に応じた色でゆっくり明滅させる。
        /// **速い点滅にしない**（<see cref="MaxPulseHz"/>）。脱落後は <see cref="CullAlertState"/> が None を返し、消える。
        /// </summary>
        private void UpdateAlert()
        {
            var nowMs = (long)(Time.realtimeSinceStartupAsDouble * 1000d);
            var alert = CullAlertState.From(warning, nowMs, selfAlive, selfInBottomRange);

            // 中央カウントダウンはビネットと同じ段階・同じ時計で駆動する（02 §6）。
            // ビネットが未割り当てでもここは通す（別のGameObjectに載っているため）。
            if (finalCountdown != null)
            {
                var remainingMs = warning != null ? warning.RemainingMsAt(nowMs) : 0L;
                finalCountdown.SetState(alert.Tier, remainingMs);
            }

            if (alertOverlay == null)
            {
                return;
            }

            var targetColor = alert.Tier == CullAlertTier.Danger ? dangerColor : cautionColor;
            var targetAlpha = 0f;

            if (alert.Tier != CullAlertTier.None)
            {
                var maxAlpha = alert.Tier == CullAlertTier.Danger ? dangerMaxAlpha : cautionMaxAlpha;

                // 残りが少ないほど濃くする。窓に入った瞬間から見えるよう下駄を履かせる。
                var depth = maxAlpha * Mathf.Lerp(0.45f, 1f, alert.Progress);

                // ゆっくりした呼吸。sin なので山も谷もなめらかで、フェードイン・フェードアウトになる。
                var hz = Mathf.Min(Mathf.Abs(pulseHz), MaxPulseHz);
                var breath = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * hz * Mathf.PI * 2f);

                // 赤（淘汰圏内）は谷でも消しきらない。消えると「危険が去った」と誤読されるため。
                var floor = alert.Tier == CullAlertTier.Danger ? 0.35f : 0f;
                targetAlpha = depth * Mathf.Lerp(floor, 1f, breath);
            }

            // 段階が切り替わった瞬間に色と濃さが飛ばないよう寄せる（圏外へ抜けたときも滑らかに消える）。
            var step = AlphaLerpPerSecond * Time.unscaledDeltaTime;
            shownAlpha = Mathf.MoveTowards(shownAlpha, targetAlpha, step);
            shownColor = Color.Lerp(shownColor, targetColor, Mathf.Clamp01(step));

            alertOverlay.alpha = shownAlpha;

            if (alertOverlayImage != null)
            {
                EnsureVignette();

                // Image の色は常に不透明で持つ。可視・不可視は CanvasGroup.alpha だけで決める
                // （両方で alpha を持つと掛け算になり、片方が 0 のとき何も出ない事故になる）。
                alertOverlayImage.color = new Color(shownColor.r, shownColor.g, shownColor.b, 1f);
            }
        }

        /// <summary>
        /// 中央を丸く空けたビネットのスプライトを用意する（02 §5）。
        /// 画面比率が変わると円が歪むため、比率が変わったら作り直す。
        /// </summary>
        private void EnsureVignette()
        {
            var rect = alertOverlayImage.rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            var aspect = rect.width / rect.height;
            if (vignetteSprite != null && Mathf.Abs(aspect - vignetteAspect) < 0.01f)
            {
                return;
            }

            vignetteAspect = aspect;
            vignetteSprite = BuildVignetteSprite(aspect, clearRadius, edgeRadius);
            alertOverlayImage.sprite = vignetteSprite;
            alertOverlayImage.type = Image.Type.Simple;
        }

        /// <summary>
        /// 中央が透明・外周が不透明な放射グラデーションを作る。
        /// 半径は**画面の短辺の半分**を 1.0 とする。こうすると縦画面でも横画面でも、
        /// 中央の素通し部分が画面上で正しく「円」になる（テクスチャを引き伸ばしても歪まない）。
        /// </summary>
        private static Sprite BuildVignetteSprite(float aspect, float clearRadius, float edgeRadius)
        {
            const int size = 256;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            // 短辺を基準にするため、長い側の距離をその比率ぶん伸ばして測る。
            var scaleX = aspect >= 1f ? aspect : 1f;
            var scaleY = aspect >= 1f ? 1f : 1f / aspect;

            var inner = Mathf.Max(0f, clearRadius);
            var outer = Mathf.Max(inner + 0.001f, edgeRadius);

            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                var dy = ((y + 0.5f) / size - 0.5f) * 2f * scaleY;
                for (var x = 0; x < size; x++)
                {
                    var dx = ((x + 0.5f) / size - 0.5f) * 2f * scaleX;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);

                    // SmoothStep で境目を柔らかくする（硬い輪郭は「枠」に見えて演出にならない）。
                    var t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, outer, r));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(t * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
        }

        private void UpdateTexts()
        {
            // Time.time を使わない（タイムスケールの影響を受けるため）。Renderer と同じ式に揃える。
            var nowMs = (long)(Time.realtimeSinceStartupAsDouble * 1000d);
            var state = CullCountdownState.From(warning, nowMs);

            // C2: 表示秒が変わったフレームだけ TMP.text に代入する。
            if (hasCurrent && current.Equals(state))
            {
                return;
            }

            current = state;
            hasCurrent = true;

            if (remainingText != null)
            {
                remainingText.text = state.RemainingText;
            }

            if (stageText != null)
            {
                stageText.text = state.StageText;
            }

            if (cutLineText != null)
            {
                cutLineText.text = state.CutLineText;
            }
        }

        private void ApplyCutRows()
        {
            if (pool == null || warning == null)
            {
                return;
            }

            var ids = warning.CutStoreIds;
            cutRows.Clear();
            visibleIds.Clear();

            var shown = ids.Count < maxCutRows ? ids.Count : maxCutRows;
            for (var i = 0; i < shown; i++)
            {
                cutRows.Add(ids[i]);
                visibleIds.Add(ids[i]);
            }

            pool.ReleaseAllExcept(visibleIds);

            for (var i = 0; i < cutRows.Count; i++)
            {
                var storeId = cutRows[i];
                var row = pool.Acquire(storeId);
                if (row == null)
                {
                    continue;
                }

                row.SetNameOnly(storeId, ResolveName(storeId));
                row.transform.SetSiblingIndex(i);

                // ★位置を自分で決める。SetSiblingIndex だけでは LayoutGroup が無い親で
                // 全行が Prefab の authored 位置（＝親の中央）に重なる。
                if (row.transform is RectTransform rect)
                {
                    rect.anchoredPosition = new Vector2(0f, -i * cutRowHeight);
                }
            }

            // サーバーの送信件数が maxCutRows より多い可能性がある。多い分は件数で添える。
            if (overflowText != null)
            {
                var rest = ids.Count - shown;
                overflowText.text = rest > 0 ? "他" + rest + "店" : string.Empty;
            }
        }

        /// <summary>解決できなければ storeId をそのまま出す（空欄にしない）。</summary>
        private string ResolveName(string storeId)
        {
            if (displayNames != null && displayNames.TryGetValue(storeId, out var n) && !string.IsNullOrEmpty(n))
            {
                return n;
            }

            return storeId;
        }

        public void SetPanelVisible(bool visible)
        {
            if (panelRoot != null && panelRoot.activeSelf != visible)
            {
                panelRoot.SetActive(visible);
            }
        }
    }
}
