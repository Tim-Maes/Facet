using Facet.Tests.TestModels;
using Facet.Tests.Utilities;

namespace Facet.Tests.UnitTests.Features;

public class BackToRequiredFieldsTests
{
    [Fact]
    public void BackTo_ShouldWork_WithExcludedRequiredFields()
    {
        // Arrange
        var eventLog = new EventLog
        {
            Id = "test-event",
            EventType = "TestEvent",
            Timestamp = DateTime.UtcNow,
            Message = "Test message",
            UserId = "user123",
            Source = "TestSource" // This required field will be excluded from the DTO
        };

        var facet = eventLog.ToFacet<EventLog, EventLogDto>();

        // Act
        var result = facet.BackTo<EventLog>();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("test-event");
        result.EventType.Should().Be("TestEvent");
        result.Timestamp.Should().Be(eventLog.Timestamp);
        result.Message.Should().Be("Test message");
        result.UserId.Should().Be("user123");
        result.Source.Should().Be(string.Empty);
    }

    [Fact]
    public void BackTo_ShouldProvideDefaultValues_ForExcludedRequiredFields()
    {
        // Arrange
        var originalEventLog = new EventLog
        {
            Id = "event-123",
            EventType = "UserLogin",
            Timestamp = DateTime.UtcNow,
            Message = "User logged in successfully",
            UserId = "user-456",
            Source = "WebApp" // This required field will be excluded in the DTO
        };

        var eventLogDto = originalEventLog.ToFacet<EventLog, EventLogDto>();

        // Act
        var mappedEventLog = eventLogDto.BackTo<EventLog>();

        // Assert
        mappedEventLog.Should().NotBeNull();
        mappedEventLog.Id.Should().Be("event-123");
        mappedEventLog.EventType.Should().Be("UserLogin");
        mappedEventLog.Timestamp.Should().Be(originalEventLog.Timestamp);
        mappedEventLog.Message.Should().Be("User logged in successfully");
        mappedEventLog.UserId.Should().Be("user-456");
        
        mappedEventLog.Source.Should().Be(string.Empty); // String default value
    }
}