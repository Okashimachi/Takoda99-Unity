namespace System.Runtime.CompilerServices;

/// <summary>
/// netstandard2.1 には C# 9 の `init` アクセサが要求するこの型が無いためのポリフィル。
/// コンパイラがこの型の存在だけを見るため、空実装で足りる。
/// </summary>
internal static class IsExternalInit
{
}
