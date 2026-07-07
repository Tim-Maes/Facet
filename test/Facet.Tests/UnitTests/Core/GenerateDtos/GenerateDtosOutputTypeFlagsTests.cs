using Facet.Tests.TestModels;
using System.Reflection;

namespace Facet.Tests.UnitTests.Core.GenerateDtos;

/// <summary>
/// Tests for flags-combined <see cref="OutputType"/> values on <c>[GenerateDtos]</c>:
/// one attribute expanding to multiple output kinds (e.g. an Interface + PartialClass pair).
/// </summary>
public class GenerateDtosOutputTypeFlagsTests
{
    private static readonly Assembly TestAssembly = Assembly.GetAssembly(typeof(TestOutputTypeFlagsEntity))!;

    [Fact]
    public void OutputTypeFlags_SingleAttribute_EmitsInterfaceAndPartialClassPair()
    {
        var createInterface = TestAssembly.GetType("Facet.Tests.TestModels.ICreateTestOutputTypeFlagsEntityRequest");
        var createClass = TestAssembly.GetType("Facet.Tests.TestModels.CreateTestOutputTypeFlagsEntityRequest");
        var updateInterface = TestAssembly.GetType("Facet.Tests.TestModels.IUpdateTestOutputTypeFlagsEntityRequest");
        var updateClass = TestAssembly.GetType("Facet.Tests.TestModels.UpdateTestOutputTypeFlagsEntityRequest");

        createInterface.Should().NotBeNull("Interface | PartialClass should emit the Interface output");
        createInterface!.IsInterface.Should().BeTrue();
        createClass.Should().NotBeNull("Interface | PartialClass should emit the PartialClass output");
        createClass!.IsClass.Should().BeTrue();
        updateInterface.Should().NotBeNull();
        updateClass.Should().NotBeNull();
    }

    [Fact]
    public void OutputTypeFlags_PartialClass_ImplementsSiblingInterfaceFromSameAttribute()
    {
        var createInterface = TestAssembly.GetType("Facet.Tests.TestModels.ICreateTestOutputTypeFlagsEntityRequest");
        var createClass = TestAssembly.GetType("Facet.Tests.TestModels.CreateTestOutputTypeFlagsEntityRequest");

        createInterface.Should().NotBeNull();
        createClass.Should().NotBeNull();
        createInterface!.IsAssignableFrom(createClass!).Should().BeTrue(
            "the PartialClass output from the same attribute should declare the generated interface as a base");
    }

    [Fact]
    public void OutputTypeFlags_CreateDto_StillExcludesId()
    {
        var createClass = TestAssembly.GetType("Facet.Tests.TestModels.CreateTestOutputTypeFlagsEntityRequest");

        createClass.Should().NotBeNull();
        createClass!.GetProperty("Id").Should().BeNull("Create DTOs exclude Id regardless of output expansion");
        createClass.GetProperty("Name").Should().NotBeNull();
    }

    [Fact]
    public void OutputTypeFlags_Record_ImplementsSiblingInterface()
    {
        var updateInterface = TestAssembly.GetType("Facet.Tests.TestModels.IUpdateTestRecordInterfacePairEntityRequest");
        var updateRecord = TestAssembly.GetType("Facet.Tests.TestModels.UpdateTestRecordInterfacePairEntityRequest");

        updateInterface.Should().NotBeNull();
        updateRecord.Should().NotBeNull();
        updateRecord!.IsClass.Should().BeTrue("records are reference types");
        updateRecord.GetMethod("<Clone>$").Should().NotBeNull("the generated type should be a record");
        updateInterface!.IsAssignableFrom(updateRecord).Should().BeTrue(
            "a flags-combined Record output should declare the sibling interface as a base");
    }

    [Fact]
    public void OutputTypeFlags_RecordStruct_ImplementsSiblingInterface()
    {
        var updateInterface = TestAssembly.GetType("Facet.Tests.TestModels.IUpdateTestStructInterfacePairEntityRequest");
        var updateStruct = TestAssembly.GetType("Facet.Tests.TestModels.UpdateTestStructInterfacePairEntityRequest");

        updateInterface.Should().NotBeNull();
        updateStruct.Should().NotBeNull();
        updateStruct!.IsValueType.Should().BeTrue("record structs are value types");
        updateInterface!.IsAssignableFrom(updateStruct).Should().BeTrue(
            "a flags-combined RecordStruct output should declare the sibling interface as a base");
    }

    [Fact]
    public void PartialModifier_Record_EmitsExtensiblePartialRecord()
    {
        // Partial-ness itself is proven at compile time: TestModels contains a hand-written
        // 'partial record UpdateTestPartialRecordEntityRequest' half adding DisplayLabel,
        // which only compiles if the generated declaration is partial.
        var updateRecord = TestAssembly.GetType("Facet.Tests.TestModels.UpdateTestPartialRecordEntityRequest");

        updateRecord.Should().NotBeNull("Record | Partial should emit the record");
        updateRecord!.GetMethod("<Clone>$").Should().NotBeNull("the generated type should be a record");
        updateRecord.GetProperty("DisplayLabel").Should().NotBeNull("the hand-written partial half should merge in");

        var sourceCtor = updateRecord.GetConstructor(new[] { typeof(TestPartialRecordEntity) });
        sourceCtor.Should().NotBeNull("Partial keeps the generated source-copy constructor");

        var copyCtor = updateRecord.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null, new[] { updateRecord }, modifiers: null);
        copyCtor.Should().NotBeNull("records carry a compiler-generated copy constructor");

        updateRecord.GetMethod("ToSource").Should().BeNull("Partial omits generator-owned mapping members");
        updateRecord.GetMethod("BackTo").Should().BeNull();
        updateRecord.GetProperty("Projection").Should().BeNull();
    }

    [Fact]
    public void PartialModifier_ComposesWithInterfaceAndRecordStruct()
    {
        // Interface | RecordStruct | Partial from ONE attribute: a partial record struct
        // implementing a partial interface. The interface's partial-ness is proven at
        // compile time by the hand-written partial interface half in TestModels.
        var updateInterface = TestAssembly.GetType("Facet.Tests.TestModels.IUpdateModTestPartialStructInterfaceEntityRequest");
        var updateStruct = TestAssembly.GetType("Facet.Tests.TestModels.UpdateModTestPartialStructInterfaceEntityRequest");

        updateInterface.Should().NotBeNull("the Interface kind should be emitted");
        updateInterface!.IsInterface.Should().BeTrue();
        updateStruct.Should().NotBeNull("the RecordStruct kind should be emitted");
        updateStruct!.IsValueType.Should().BeTrue("record structs are value types");
        updateInterface.IsAssignableFrom(updateStruct).Should().BeTrue(
            "the concrete kind should implement the sibling interface from the same attribute");
    }

    [Fact]
    public void OutputType_EnumValues_ArePowersOfTwo()
    {
        // Guards the [Flags] contract: every kind and the Partial modifier is a distinct
        // single bit, so combinations never alias another member (the pre-flags enum had
        // Interface | PartialClass == PartialClass). PartialClass itself is deliberately
        // NOT a single bit — it is the back-compat alias for Class | Partial.
        var flags = new[]
        {
            OutputType.Class, OutputType.Record, OutputType.Struct,
            OutputType.RecordStruct, OutputType.Interface, OutputType.Partial,
        };

        ((int)OutputType.None).Should().Be(0);
        foreach (var flag in flags)
        {
            var value = (int)flag;
            (value != 0 && (value & (value - 1)) == 0).Should().BeTrue($"{flag} must be a single bit");
        }

        flags.Select(f => (int)f).Should().OnlyHaveUniqueItems();

        OutputType.PartialClass.Should().Be(OutputType.Class | OutputType.Partial,
            "PartialClass is the back-compat alias for the Class kind with the Partial modifier");
    }
}
