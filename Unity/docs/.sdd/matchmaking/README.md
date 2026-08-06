# matchmaking — 試合前（マッチング画面）の仕様

**試合が始まる前**の画面と通信の仕様を置く。試合中の画面（[02-main-store-view.md](../match-view/02-main-store-view.md) 以降）とは接続の同じセッション上にあるが、**扱うメッセージも画面も別物**のため分冊する。

> **現状、Unity 側は試合画面しか実装できていない。** ここは未実装領域の仕様を先に確定させておくためのディレクトリで、[README §4](../README.md) の「実装より必ず仕様書が先」に従う。

## 1. なぜ試合前の画面が必須か

**本番は match モードで動く。接続したら即 `MatchStart` が来るとは限らない。**

サーバーは待機列に `minPlayers` が集まるまで試合を開始せず、その間 `MatchmakingStatus` を配信し続ける（[Takoda99-Server `docs/client-integration.md`](https://github.com/Okashimachi/Takoda99-Server/blob/main/docs/client-integration.md) §3.1）。待機画面が無いと、プレイヤーは無応答の黒画面を見ることになる。

## 2. ファイル構成

| # | ファイル | 内容 | 状態 |
|---|---|---|---|
| 01 | [01-matchmaking-flow.md](./01-matchmaking-flow.md) | 接続 → `MatchmakingJoin` → 待機 → `MatchStart` までの流れと画面 | ✅ |
| 02 | [02-display-name.md](./02-display-name.md) | 表示名の入力・送信と、他店（98店）の表示名の取得 | ✅ |

## 3. このディレクトリが扱うメッセージ

| メッセージ | 向き | 扱う場所 |
|---|---|---|
| `MatchmakingJoin` | C2S | [01](./01-matchmaking-flow.md) §4 / [02](./02-display-name.md) |
| `MatchmakingLeave` | C2S | [01](./01-matchmaking-flow.md) §4 |
| `MatchmakingStatus` | S2C | [01](./01-matchmaking-flow.md) §5 |
| `MatchStart` | S2C | [01](./01-matchmaking-flow.md) §6（受信後は試合画面へ引き渡す） |

## 4. 上流の参照先

| 対象 | 正典 |
|---|---|
| メッセージの**型** | [Takoda99-Proto](https://github.com/Okashimachi/Takoda99-Proto) `proto/messages.go`（**Go が正典**。C# ミラーは §5 の注意を参照） |
| **いつ・どの頻度で**届くか | [Takoda99-Server `docs/client-integration.md`](https://github.com/Okashimachi/Takoda99-Server/blob/main/docs/client-integration.md) |
| 画面遷移の設計 | [Takoda99-Client-Docs](https://github.com/Okashimachi/Takoda99-Client-Docs) |

## 5. ⚠ C# ミラーの追従漏れについて（重要）

**`MatchmakingJoin` は Go 正典に `displayName` を持つが、C# ミラーには存在しない。**

| 言語 | v0.3.0 時点 |
|---|---|
| Go（正典） | `DisplayName string \`json:"displayName,omitempty"\`` |
| C# | **フィールド無し**（`public sealed class MatchmakingJoin { }`） |

Proto 側のコミット `d567a98`（2026-08-04）が Go のみを変更し、後続の v0.3.0 で拾われなかったことによる。

> TS ミラーにも同じ追従漏れがあるが、**このリポジトリは C# しか使わないため対象外**とする。

**Go 正典が正しい。** サーバーは `displayName` を読む実装になっている。

- **このリポジトリで C# ミラーを直さない**（[docs/rules/01-責務と絶対原則.md](../../../../docs/rules/01-責務と絶対原則.md) 絶対原則7。契約の変更は Proto 側で人間承認）
- **上流 Proto への修正依頼が前提**。それまでこのモジュールは実装に着手しない
- 詳細と暫定策は [02-display-name.md](./02-display-name.md) §2
