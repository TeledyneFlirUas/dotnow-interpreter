using System;
using System.Reflection;
using System.Reflection.Metadata;

namespace dotnow.Reflection
{
    internal sealed class CLRExceptionHandlingClause : ExceptionHandlingClause
    {
        // Internal
        internal readonly MetadataReferenceProvider metadataProvider = null;
        internal readonly ExceptionRegion region = default;

        // Private
        private readonly Lazy<Type> catchType;

        public override Type CatchType => catchType.Value;
        public override int TryOffset => region.TryOffset;
        public override int TryLength => region.TryLength;          // Was region.TryOffset (bug) - made every try range look like [offset, 2*offset)
        public override int HandlerOffset => region.HandlerOffset;
        public override int HandlerLength => region.HandlerLength;
        public override int FilterOffset => region.FilterOffset;
        public override ExceptionHandlingClauseOptions Flags => (ExceptionHandlingClauseOptions)region.Kind;

        // Constructor
        public CLRExceptionHandlingClause(MetadataReferenceProvider metadataProvider, ExceptionRegion region)
        {
            this.metadataProvider = metadataProvider;
            this.region = region;

            // Initialize handler type
            this.catchType = new(InitCatchType);
        }

        private Type InitCatchType()
        {
            // Only typed catch clauses carry a type. Finally/fault/filter regions have a nil CatchType handle,
            // and resolving a nil handle throws InvalidOperationException("Type handle is nil").
            if (region.Kind != ExceptionRegionKind.Catch || region.CatchType.IsNil == true)
                return null;

            // Just resolve the type
            return metadataProvider.ResolveMetadataType(region.CatchType);
        }
    }
}
