using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace dotnow.Runtime.CIL
{
    [Flags]
    internal enum ExceptionHandlerKind
    {
        Clause = 0,
        Filter = 1,
        Finally = 2,
        Fault = 4
    }

    internal readonly struct CILExceptionHandlerInfo
    {
        // Public
        public readonly Type ExceptionType;
        public readonly ExceptionHandlerKind HandlerKind;
        public readonly int TryOffset;
        public readonly int TryLength;
        public readonly int HandlerOffset;
        public readonly int HandlerLength;

        // Constructor
        public CILExceptionHandlerInfo(ExceptionHandlingClause clause)
        {
            this.HandlerKind = (ExceptionHandlerKind)clause.Flags;
            this.TryOffset = clause.TryOffset;
            this.TryLength = clause.TryLength;
            this.HandlerOffset = clause.HandlerOffset;
            this.HandlerLength = clause.HandlerLength;

            // CatchType is only defined for typed catch clauses. Reading it for finally/fault/filter clauses
            // throws on some ExceptionHandlingClause implementations (nil metadata handle).
            this.ExceptionType = this.HandlerKind == ExceptionHandlerKind.Clause
                ? clause.CatchType
                : null;
        }

        // Methods
        /// <summary>
        /// True if the given instruction offset lies inside this clause's protected (try) region.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCaught(int pc)
        {
            return pc >= TryOffset && pc < TryOffset + TryLength;
        }

        /// <summary>
        /// True if the given instruction offset lies inside this clause's handler region.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsInHandler(int pc)
        {
            return pc >= HandlerOffset && pc < HandlerOffset + HandlerLength;
        }

        public override string ToString()
        {
            return $"{HandlerKind} try[{TryOffset},{TryOffset + TryLength}) handler[{HandlerOffset},{HandlerOffset + HandlerLength}) {ExceptionType}";
        }
    }
}
