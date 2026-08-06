// NativeWebSocket が使う Module.dynCall_* を Unity 6 の WebGL ビルドで補完する。
//
// Unity 6 の Emscripten は dynCall_* を出力しなくなり、NativeWebSocket 同梱の
// WebSocket.jspre が肩代わりを定義しようとするが、その中身が
// `if (typeof getWasmTableEntry !== "undefined")` で囲まれている。
// リリースビルドでは framework JS が minify されてこの名前が残らないため
// ガードごとスキップされ、onMessage の dynCall_viii などが未定義のまま残る
// （dynCall_vi だけは Unity 本体が内部用に export しているため偶然動く）。
//
// ここでは名前の存在に依存せず複数の経路でフォールバックして定義する。
// パッケージ側は `Module.dynCall_x = Module.dynCall_x || ...` と書いているので、
// 先にこちらが定義しても衝突しない。
(function () {
  function resolve(cb) {
    if (typeof getWasmTableEntry !== "undefined") return getWasmTableEntry(cb);
    if (typeof wasmTable !== "undefined") return wasmTable.get(cb);
    if (Module["wasmTable"]) return Module["wasmTable"].get(cb);
    return null;
  }

  function define(sig) {
    if (Module["dynCall_" + sig]) return;
    Module["dynCall_" + sig] = function () {
      var args = Array.prototype.slice.call(arguments);
      var cb = args.shift();
      var fn = resolve(cb);
      if (fn) return fn.apply(null, args);
      if (typeof Module["dynCall"] === "function") return Module["dynCall"](sig, cb, args);
      throw new Error("dynCall_" + sig + " could not be resolved");
    };
  }

  function init() {
    define("vi");
    define("vii");
    define("viii");
    define("viiii");
  }

  if (Module["ENVIRONMENT_IS_PTHREAD"]) {
    init();
  } else {
    Module["preRun"].push(init);
  }
})();
