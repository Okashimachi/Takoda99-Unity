// netstandard2.1 には System.Runtime.CompilerServices.IsExternalInit が無く、
// record / init アクセサがコンパイルできないためのポリフィル。ロジックは持たない。

namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
