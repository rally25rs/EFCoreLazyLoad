; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
EFLL001 | EFCoreLazyLoad | Error | Type using [LazyLoad] must be a partial class
EFLL002 | EFCoreLazyLoad | Error | [LazyLoad] property must be partial
EFLL003 | EFCoreLazyLoad | Error | [LazyLoad] property already has an implementation
EFLL004 | EFCoreLazyLoad | Error | Lazy-loaded property must have a reference type
EFLL005 | EFCoreLazyLoad | Error | Lazy-loaded property must have a getter
EFLL006 | EFCoreLazyLoad | Error | Lazy-loaded property must be an instance, non-indexer property
EFLL007 | EFCoreLazyLoad | Error | Inherited ILazyLoader member is not accessible
EFLL008 | EFCoreLazyLoad | Error | [LazyLoad] requires C# 14 or later
EFLL009 | EFCoreLazyLoad | Error | Backing field type is not compatible with the lazy-loaded property
EFLL010 | EFCoreLazyLoad | Warning | Collection over a backing field will not be lazy-loaded
