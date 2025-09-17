## Release 2.9.0

### New Rules

| Rule ID | Category    | Severity | Notes                                                              |
|---------|-------------|----------|--------------------------------------------------------------------|
| FAC001  | Usage       | Error    | ToFacet target type must be annotated with [Facet]                 |
| FAC002  | Usage       | Error    | BackTo facet type must be annotated with [Facet]                   |
| FAC003  | Usage       | Error    | BackTo object must be a facet type                                 |
| FAC004  | Performance | Info     | Consider using ToFacet<TSource, TTarget> for better performance    |
| FAC005  | Performance | Info     | Consider using BackTo<TFacet, TFacetSource> for better performance |