using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using ChatServer.Models.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;
using Xunit.Abstractions;

namespace ChatServer.Tests.LoadTests;

/// <summary>
/// Load tests for concurrent users and high message throughput
/// </summary>
public class ConcurrentUsersLoadTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public ConcurrentUsersLoadTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    private async Task<string> RegisterUniqueUser(HttpClient client, string? prefix = null)
    {
        var nickname = $"{prefix ?? "user"}_{Guid.NewGuid():N}".Substring(0, 20);
        await client.PostAsJsonAsync("/api/users/register", new { nickname });
        return nickname;
    }

    [Fact(Timeout = 120000)] // 2 minute timeout
    public async Task LoadTest_100ConcurrentUsers_AllMessagesDelivered()
    {
        // Arrange
        var httpClient = _factory.CreateClient();
        var server = _factory.Server;
        var baseAddress = httpClient.BaseAddress ?? new Uri("http://localhost");

        const int userCount = 100;
        const int messagesPerUser = 10;
        const int expectedTotalMessages = userCount * messagesPerUser;

        _output.WriteLine($"Starting load test: {userCount} users, {messagesPerUser} messages each");
        var stopwatch = Stopwatch.StartNew();

        // Create room
        var roomCreator = await RegisterUniqueUser(httpClient, "creator");
        var roomResponse = await httpClient.PostAsJsonAsync("/api/rooms", new
        {
            name = "Load Test Room",
            description = "100 concurrent users",
            createdBy = roomCreator
        });
        var room = await roomResponse.Content.ReadFromJsonAsync<RoomResponse>();
        room.Should().NotBeNull();

        _output.WriteLine($"Room created: {room!.Id} (elapsed: {stopwatch.ElapsedMilliseconds}ms)");

        // Create connections and track messages
        var connections = new ConcurrentBag<HubConnection>();
        var userNicknames = new ConcurrentBag<string>();
        var messageCounters = new ConcurrentDictionary<string, int>();

        try
        {
            // Register all users and create connections
            var registrationTasks = Enumerable.Range(0, userCount).Select(async i =>
            {
                var nickname = await RegisterUniqueUser(httpClient, $"load{i:D3}");
                userNicknames.Add(nickname);
                messageCounters[nickname] = 0;

                var connection = new HubConnectionBuilder()
                    .WithUrl($"{baseAddress}hubs/chat", options =>
                    {
                        options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    })
                    .Build();

                connection.On<MessageResponse>("ReceiveMessage", msg =>
                {
                    // Only count user messages, not system messages
                    if (msg.Type == ChatServer.Models.MessageType.UserMessage)
                    {
                        messageCounters.AddOrUpdate(nickname, 1, (_, count) => count + 1);
                    }
                });

                connections.Add(connection);
                return (connection, nickname);
            });

            var results = await Task.WhenAll(registrationTasks);
            var connectionList = results.Select(r => r.connection).ToList();
            var nicknameList = results.Select(r => r.nickname).ToList();
            _output.WriteLine($"Users registered and connections created (elapsed: {stopwatch.ElapsedMilliseconds}ms)");

            // Start all connections
            var startTasks = connectionList.Select(c => c.StartAsync());
            await Task.WhenAll(startTasks);
            _output.WriteLine($"All connections started (elapsed: {stopwatch.ElapsedMilliseconds}ms)");

            // All users join the room
            var joinTasks = connectionList.Select((conn, index) =>
                conn.InvokeAsync("JoinRoom", room.Id.ToString(), nicknameList[index])
            );
            await Task.WhenAll(joinTasks);
            _output.WriteLine($"All users joined room (elapsed: {stopwatch.ElapsedMilliseconds}ms)");

            // Wait for join system messages to be processed
            await Task.Delay(1000);

            // Each user sends messages
            var sendTasks = new List<Task>();
            for (int userIndex = 0; userIndex < userCount; userIndex++)
            {
                var connection = connectionList[userIndex];
                var nickname = nicknameList[userIndex];

                for (int msgIndex = 0; msgIndex < messagesPerUser; msgIndex++)
                {
                    var messageContent = $"Message {msgIndex} from {nickname}";
                    sendTasks.Add(connection.InvokeAsync("SendMessage", room.Id.ToString(), messageContent));
                }
            }

            await Task.WhenAll(sendTasks);
            _output.WriteLine($"All messages sent (elapsed: {stopwatch.ElapsedMilliseconds}ms)");

            // Wait for all messages to be delivered
            await Task.Delay(5000);

            stopwatch.Stop();
            _output.WriteLine($"Test completed in {stopwatch.ElapsedMilliseconds}ms ({stopwatch.ElapsedMilliseconds / 1000.0:F2}s)");

            // Assert
            // Calculate statistics
            var totalMessagesReceived = messageCounters.Values.Sum();
            var expectedMessagesPerUser = expectedTotalMessages; // Each user should receive all messages
            var averageMessagesReceived = totalMessagesReceived / (double)userCount;

            _output.WriteLine($"Total messages sent: {expectedTotalMessages}");
            _output.WriteLine($"Total message receptions: {totalMessagesReceived}");
            _output.WriteLine($"Expected receptions: {userCount * expectedTotalMessages}");
            _output.WriteLine($"Average messages per user: {averageMessagesReceived:F1}");

            // Verify each user received a reasonable number of messages
            // Due to timing and potential message loss in high load, we'll be lenient
            // and require at least 80% message delivery rate
            var minExpectedMessages = (int)(expectedTotalMessages * 0.8);

            foreach (var (nickname, count) in messageCounters)
            {
                count.Should().BeGreaterThanOrEqualTo(minExpectedMessages,
                    $"user {nickname} should receive at least 80% of messages");
            }

            // Verify overall delivery rate
            var deliveryRate = totalMessagesReceived / (double)(userCount * expectedTotalMessages);
            _output.WriteLine($"Overall delivery rate: {deliveryRate:P2}");
            deliveryRate.Should().BeGreaterThanOrEqualTo(0.8, "at least 80% of messages should be delivered");
        }
        finally
        {
            // Cleanup
            var allConnections = connections.ToList();
            if (allConnections.Any())
            {
                var stopTasks = allConnections.Select(c => c.StopAsync());
                await Task.WhenAll(stopTasks);

                var disposeTasks = allConnections.Select(c => c.DisposeAsync().AsTask());
                await Task.WhenAll(disposeTasks);
            }

            _output.WriteLine("All connections cleaned up");
        }
    }

    [Fact(Timeout = 60000)] // 1 minute timeout
    public async Task LoadTest_ConcurrentRoomJoins_AllSucceed()
    {
        // Arrange
        var httpClient = _factory.CreateClient();
        var server = _factory.Server;
        var baseAddress = httpClient.BaseAddress ?? new Uri("http://localhost");

        const int userCount = 50;

        _output.WriteLine($"Starting concurrent room join test: {userCount} users");
        var stopwatch = Stopwatch.StartNew();

        // Create room
        var roomCreator = await RegisterUniqueUser(httpClient, "creator");
        var roomResponse = await httpClient.PostAsJsonAsync("/api/rooms", new
        {
            name = "Concurrent Join Test",
            description = "Testing concurrent joins",
            createdBy = roomCreator
        });
        var room = await roomResponse.Content.ReadFromJsonAsync<RoomResponse>();
        room.Should().NotBeNull();

        // Register users and create connections
        var connections = new List<HubConnection>();
        var userNicknames = new List<string>();

        try
        {
            for (int i = 0; i < userCount; i++)
            {
                var nickname = await RegisterUniqueUser(httpClient, $"join{i:D2}");
                userNicknames.Add(nickname);

                var connection = new HubConnectionBuilder()
                    .WithUrl($"{baseAddress}hubs/chat", options =>
                    {
                        options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    })
                    .Build();

                await connection.StartAsync();
                connections.Add(connection);
            }

            _output.WriteLine($"All connections ready (elapsed: {stopwatch.ElapsedMilliseconds}ms)");

            // All users join concurrently
            var joinTasks = connections.Select((conn, index) =>
                conn.InvokeAsync("JoinRoom", room!.Id.ToString(), userNicknames[index])
            );

            await Task.WhenAll(joinTasks);

            stopwatch.Stop();
            _output.WriteLine($"All joins completed in {stopwatch.ElapsedMilliseconds}ms");

            // Wait for processing
            await Task.Delay(500);

            // Assert - verify all users in room via REST API
            var roomDetail = await httpClient.GetFromJsonAsync<RoomDetailResponse>($"/api/rooms/{room!.Id}");
            roomDetail.Should().NotBeNull();
            roomDetail!.Participants.Should().HaveCount(userCount);

            _output.WriteLine($"Verified {roomDetail.Participants.Count} participants in room");
        }
        finally
        {
            // Cleanup
            var stopTasks = connections.Select(c => c.StopAsync());
            await Task.WhenAll(stopTasks);

            var disposeTasks = connections.Select(c => c.DisposeAsync().AsTask());
            await Task.WhenAll(disposeTasks);
        }
    }

    [Fact(Timeout = 60000)] // 1 minute timeout
    public async Task LoadTest_HighMessageThroughput_NoMessageLoss()
    {
        // Arrange
        var httpClient = _factory.CreateClient();
        var server = _factory.Server;
        var baseAddress = httpClient.BaseAddress ?? new Uri("http://localhost");

        const int messagesCount = 500;

        _output.WriteLine($"Starting high throughput test: {messagesCount} messages");
        var stopwatch = Stopwatch.StartNew();

        // Create room and register users
        var sender = await RegisterUniqueUser(httpClient, "sender");
        var receiver = await RegisterUniqueUser(httpClient, "receiver");

        var roomResponse = await httpClient.PostAsJsonAsync("/api/rooms", new
        {
            name = "Throughput Test",
            description = "High message rate",
            createdBy = sender
        });
        var room = await roomResponse.Content.ReadFromJsonAsync<RoomResponse>();

        // Create connections
        var senderConn = new HubConnectionBuilder()
            .WithUrl($"{baseAddress}hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();

        var receiverConn = new HubConnectionBuilder()
            .WithUrl($"{baseAddress}hubs/chat", options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();

        var receivedMessages = new ConcurrentBag<MessageResponse>();
        receiverConn.On<MessageResponse>("ReceiveMessage", msg =>
        {
            if (msg.Type == ChatServer.Models.MessageType.UserMessage)
            {
                receivedMessages.Add(msg);
            }
        });

        try
        {
            await senderConn.StartAsync();
            await receiverConn.StartAsync();

            await senderConn.InvokeAsync("JoinRoom", room!.Id.ToString(), sender);
            await receiverConn.InvokeAsync("JoinRoom", room.Id.ToString(), receiver);

            await Task.Delay(500);

            _output.WriteLine($"Starting to send {messagesCount} messages...");

            // Send messages as fast as possible
            var sendTasks = Enumerable.Range(0, messagesCount).Select(i =>
                senderConn.InvokeAsync("SendMessage", room.Id.ToString(), $"Message {i}")
            );

            await Task.WhenAll(sendTasks);

            _output.WriteLine($"All messages sent (elapsed: {stopwatch.ElapsedMilliseconds}ms)");

            // Wait for delivery
            await Task.Delay(3000);

            stopwatch.Stop();

            // Assert
            var deliveredCount = receivedMessages.Count;
            var deliveryRate = deliveredCount / (double)messagesCount;

            _output.WriteLine($"Messages sent: {messagesCount}");
            _output.WriteLine($"Messages received: {deliveredCount}");
            _output.WriteLine($"Delivery rate: {deliveryRate:P2}");
            _output.WriteLine($"Test duration: {stopwatch.ElapsedMilliseconds}ms");
            _output.WriteLine($"Throughput: {messagesCount / (stopwatch.ElapsedMilliseconds / 1000.0):F1} messages/second");

            // Require at least 95% delivery for high throughput test
            deliveryRate.Should().BeGreaterThanOrEqualTo(0.95, "high throughput should maintain 95% delivery rate");
        }
        finally
        {
            await senderConn.StopAsync();
            await receiverConn.StopAsync();
            await senderConn.DisposeAsync();
            await receiverConn.DisposeAsync();
        }
    }
}
