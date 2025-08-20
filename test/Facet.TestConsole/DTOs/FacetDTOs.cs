namespace Facet.TestConsole.DTOs;

[Facet(typeof(Data.User), "Password")]
public partial class DbUserDto { }

[Facet(typeof(Data.User), "Password", "CreatedAt")]
public partial class UpdateDbUserDto { }

[Facet(typeof(Data.User), "Password")]
public partial class PublicDbUserDto { }

[Facet(typeof(Data.User), "Id", "CreatedAt", "LastLoginAt")]
public partial class CreateDbUserDto { }

[Facet(typeof(Data.Product), "InternalNotes")]
public partial class DbProductDto { }

[Facet(typeof(Data.Product), "Id", "CreatedAt", "InternalNotes")]  
public partial class UpdateDbProductDto { }

[Facet(typeof(Data.Product), "InternalNotes", "CategoryId")]
public partial class PublicDbProductDto { }

[Facet(typeof(Data.Product), "Id", "CreatedAt")]
public partial class CreateDbProductDto { }

[Facet(typeof(Data.Category))]
public partial class DbCategoryDto { }

[Facet(typeof(Data.Category), "Id", "CreatedAt")]
public partial class UpdateDbCategoryDto { }

[Facet(typeof(Data.User), "Password", Kind = FacetKind.Record)]
public partial record DbUserRecord;

[Facet(typeof(Data.Product), "InternalNotes", "Description", Kind = FacetKind.RecordStruct)]  
public partial record struct DbProductSummary;

[Facet(typeof(Data.User), "Password", "Bio", "ProfilePictureUrl", "LastLoginAt", Kind = FacetKind.Struct)]
public partial struct DbUserSummary;