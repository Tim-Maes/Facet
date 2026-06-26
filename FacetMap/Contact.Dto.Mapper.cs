#region Usings

using Facet;
using Facet.Mapping;
using InControl.Backend.Core.EntityFramework.Model.MasterData.Contact;
using InControl.Common.Enum.MasterData.Address;
using System.Linq;
using InControl.Common.Dto.MasterData.Contact;

#endregion

// ReSharper disable once CheckNamespace
namespace InControl.Backend.Mapper;

[FacetMap(typeof(ContactEntity),typeof(ContactDto), Configuration = typeof(ContactDtoMapConfig), GenerateToSource = true)]
public static partial class ContactDtoMapper;

public class ContactDtoMapConfig : IFacetProjectionMapConfiguration<ContactEntity, ContactDto>
{
    #region IFacetProjectionMapConfiguration<ContactEntity,ContactDto> Members

    public static void ConfigureProjection(IFacetProjectionBuilder<ContactEntity, ContactDto> builder)
    {
        builder.Map(target => target.HasInvoiceAddress, source => source.Address.Any(x => (x.Type & AddressTypeFlag.Invoice) != 0));
    }

    #endregion
}